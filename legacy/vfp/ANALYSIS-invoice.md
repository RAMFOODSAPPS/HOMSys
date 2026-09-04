# invoice.SCX — Part 1 findings (SQL refresh + manual Refresh button)

Derived by decoding the staged `legacy\vfp\invoice.SCX`/`.SCT` with a one-off
Python parser (`C:\claude\decode_invoice.py` — VFP6 headless `.prg` execution
was unreliable in this session, so the decode went through raw DBF/FPT
parsing instead of `E:\Vfp98\vfp6.exe`). Output: `C:\claude\output\invoice_decode.txt`
(138 records). Everything below is cited to a `PROCEDURE` name so it can be
re-checked against that file.

## `optn_init1` is not a control — it's a mode-dispatch method

`Form1` has one real mode switch: `Optiongroup1` (8 buttons). Its `Click`
does a `DO CASE` on `this.value` and calls one of eight identically-shaped
form methods:

| value | method | label |
|---|---|---|
| 1 | `optn_init1` | PROCESS ORDERS |
| 2 | `optn_init2` | PRINT FCCOS |
| 3 | `optn_init3` | PREVIEW INVOICE |
| 4 | `optn_init4` | CONFIRM CLEAN ORDERS |
| 5 | `optn_init5` | PRINT PICKLIST |
| 6 | `optn_init6` | CONFIRM PICKLIST |
| 7 | `optn_init7` | FORWARD TO INVOICE (old) |
| 8 | `optn_init8` | PRINT INVOICE (old) |

So "active option is `optn_init1`" = **`Optiongroup1.value = 1`, the PROCESS
ORDERS mode** — the screen's default/entry mode, where orders coming out of
SO encoding first land for invoicing. This is the mode the Refresh button
should be gated on, matching what the user described.

There's a matching `optn_unload1..8` set, called on mode exit (cleanup —
release/close scratch cursors for that mode).

## `Form1.Init` already re-runs the current mode on open

```
thisform.dselected = thisform.optiongroup1.value
... (bmsvar lookups, prod4win weight backfill, etc.)
thisform.optiongroup1.click     && line 14227 — re-dispatches to optn_init1..8
thisform.refresh
```

`Init` doesn't call `optn_init1` directly — it re-fires `Optiongroup1.Click`,
which re-runs whichever `optn_initN` matches the option group's *current*
value. Since PROCESS ORDERS (value 1) is the default, opening the form
normally re-runs `optn_init1` already. This is the natural hook point for the
new SQL refresh: either drop it into `optn_init1` itself (so it fires both on
open and every time the user re-selects PROCESS ORDERS), or add one explicit
call right after `thisform.optiongroup1.click` in `Init`, gated on
`thisform.optiongroup1.value = 1`. The former is less code and matches how
this form already treats "switch to mode N" as "rebuild mode N's data."

## Where `oowkhdr`/`oowkdet` are opened and read — `optn_init1`

Table opens (all via `openfile()` — actually named `CLOSE.PRG` internally,
see below — `SHARED`, not exclusive):

```
oowkdis, tparprod, appfccos, branchw, prod4win, cust4win, zone, soedith,
soeditd, soeditk, picklog, deal4mas, deal4win, tparcust, dusunblk, soamtlog,
custbal, disctrap, sman4win, stkouth, stkoutd, latepay, zonechan, zone2,
tpsales, zonemast, avail4, avail5, avail6
use &pub_dinvb alias invblock in 0 shared     && special-cased, not via openfile()
```

Then:

```
sele oowkdet
set order docno

sele oowkhdr
set order docno

tmph = newname("dbf")
sele oowkhdr
copy to &tmph for (status $ ",1,B" .or. (status = "1" .and. offshore) ;
   .or. (status = "1" .and. trtype = 2)) .and. .not. expectdel

sele templist
appe from &tmph
erase &tmph

m = thisform.dcheckdeals()     && validates deals on the selected s.o.
... index templist on docno ...
thisform.definegrid
```

Key points:

- **`oowkhdr` is the read source for the grid.** The filter is: `status` is
  blank/`"1"`/`"B"`, OR `status="1"` with `offshore`, OR `status="1"` with
  `trtype=2` — and always `.not. expectdel`. Only matching rows get copied
  into a scratch DBF and appended into `templist` (the cursor `Init` created
  and the grid is bound to).
- **`oowkdet` is opened and ordered here but not read directly into
  `templist`** — it's read per-document later (processing/allocation methods
  such as `process1`, `allocate`, `applydwk1/2`) once the user picks a row.
- Both are opened **SHARED**. A refresh routine that wants to `ZAP` and
  reload them can't just do that while the form has them open SHARED — `ZAP`
  needs exclusive use. Two options for Part 2:
  1. A keyed upsert (`SCAN`/`SEEK` + `REPLACE` for existing `docno`s,
     `APPEND BLANK` + `REPLACE` for new ones) — works fine against a SHARED
     open, no reopen needed. Matches how this form already treats these
     tables (never exclusive).
  2. Close and reopen exclusively to `ZAP` + bulk-reload — simpler code, but
     needs a lock-retry loop (same spin-wait shape as `docnum.prg`'s
     `GETSAVE`, per `[[reference_..]]`/`ANALYSIS.md`) and is riskier if this
     DBF is ever opened by more than the current session on that branch.

  Given the "manual Refresh button, plus auto on Init" requirement, **(1) is
  the safer default** — it never contends for an exclusive lock, so it can't
  itself cause the "file in use" failures this session already hit once when
  pulling `invoice.SCX`/`.SCT` from source.

## No existing SQL Server connectivity in this form

Grepped the full decode for `SQLCONNECT`, `SQLSTRINGCONNECT`, `SQLEXEC`,
`SQLDISCONNECT` — zero matches anywhere in `invoice.SCX`. Confirms this form
has no existing path to a SQL Server; the refresh routine needs to add
VFP-native SQL connectivity from scratch (`SQLSTRINGCONNECT` against the same
target `HOMSys.API`'s `appsettings.json` points at, then `SQLEXEC` to pull
`SalesOrders`/`SalesOrderLines`).

## `openfile()` is actually `CLOSE.PRG` (filename/header mismatch)

`legacy\vfp\prg\openfile.prg` — its own header comment says
`* program name : CLOSE.PRG`, `udf program to close open file`, but the body
is an idempotent *open*-if-not-open helper:

```
param malias
mused=.f.
if !used("&malias")
   use &malias in 0 share
   mused=.t.
endif
sele &malias
return mused
```

i.e. `openfile("oowkhdr")` = "make sure `oowkhdr` is open `SHARED` in some
work area, select it, tell me if I'm the one who opened it." This is a
pre-existing copy/rename artifact in BMS, not something introduced here —
noted so nobody "fixes" the header comment thinking it's a staging mistake.

## What's still open going into Part 2

- Exact `SalesOrders`/`SalesOrderLines` → `oowkhdr`/`oowkdet` field mapping
  for the columns the refresh needs to touch — cross-reference against
  `ANALYSIS.md`'s "`oowkhdr` has 139 fields, most of them downstream workflow
  state" note. The refresh must only touch the **ENCODE-OWNED** region
  (mirrors what HOMSys's `SalesOrder`/`SalesOrderLine` already track) and
  leave BMS-owned workflow fields (`INVNO`, `RSRNO`, `PICKNO`, `STATUS`,
  `CRAUTHOR`, `DRNO`, `SERVEDATE`, ...) untouched — same boundary the SO
  encoding form already respects.
- Scope of the pull: newest-first by `docno`/timestamp, or a "since last
  refresh" watermark? `optn_init1`'s own filter (`status`/`offshore`/`trtype`)
  suggests the refresh should pull *at least* whatever HOMSys has that isn't
  yet in that same status set locally, but the exact incremental condition
  needs a decision once the SQL-side shape (`SalesOrder.Status` equivalent)
  is confirmed.
- Whether `pub_dinvb` (the `invblock` alias target, opened via a raw `use`
  rather than `openfile()`) needs any refresh-time handling — currently
  unrelated to `oowkhdr`/`oowkdet`, flagged only because it's the one table
  in `optn_init1` opened outside the shared `openfile()` convention.
