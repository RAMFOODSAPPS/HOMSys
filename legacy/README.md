# legacy/ — reference material, NOT ours

Read-only snapshots of the existing BMS sales-order encoding system, staged here
so HOMSys development has something local to work against.

**Nothing in this folder is live, and nothing here should ever be written back
to its source.** These are point-in-time copies. The real tables keep changing.

Copied **2026-08-18**.

## vfp/ — the form being replaced

Source: `\\itworks-pc\source\bms`

| File | Bytes | Source mtime |
|---|---|---|
| `a11102.SCX` | 3,976 | 2026-07-18 14:23 |
| `a11102.SCT` | 125,545 | 2026-07-18 14:23 |
| `a11102a.SCX` | 2,123 | 2025-11-24 17:34 |
| `a11102a.SCT` | 25,640 | 2025-11-24 17:34 |
| `invoice.SCX` | 16,075 | 2026-08-05 11:53 |
| `invoice.SCT` | 951,809 | 2026-08-05 11:53 |

`invoice.SCX` is the BMS Invoicing screen — the module immediately downstream
of SO encoding. Staged 2026-08-19 to plan a manual/`Init`-time refresh of
local `oowkhdr`/`oowkdet` from the HOMSys SQL Server (realtime push was ruled
out as not worth the infrastructure cost). Not yet decoded into `extracted/` —
do that before touching `optn_init1` or `Init`.

`a11102.SCX` is the SO encoding screen from the flow chart. A VFP `.SCX` is
itself a DBF and the `.SCT` is its memo file — **the pair is required**, an
`.SCX` alone is unreadable. Nearly all of the form's actual code lives in the
`.SCT`.

Note `a11102.SCX` was modified 2026-07-18, so the encoding rules are still
actively maintained upstream. Re-pull before relying on behavioural details.

### extracted/ and prg/

`vfp/extracted/a11102.txt` and `a11102a.txt` are the decoded form sources —
object tree, properties and full method bodies, produced by parsing the `.SCX`
DBF and its `.SCT` memo blocks. Generated artifacts; re-derivable from the
`.SCX`/`.SCT` pair.

`vfp/prg/` holds the 20 external procedures the form calls: `docnum` `hconvert`
`qtyloose` `chklimit` `upmaxstat` `audtrail` `recalc16` `recalc1b` `armatch3`
`renfile` `newname` `openfile` `delfile` `timeuse` `depcs` `getexe` `saveuser`
`datauser` `eom` `closedbf`. Source `\\itworks-pc\source\bms`. `messageb` and
`fdate` were not found as standalone `.prg`.

**`vfp/ANALYSIS.md` is the writeup of how the form actually works** — read that
before designing anything.

### Known behaviour

The **Get Max button is disabled**: `thisform.cmdmax.enabled = .f.` is the only
assignment to that property in the form, so it is permanently off, not
conditionally toggled. "GetMax" here means *max order limit* enforcement, not
next-number assignment — see grid columns `getmaxcs` / `getmaxpc`, captions
"CS After GetMax" / "PC After GetMax", also `.enabled=.f.`.

Open question: the button is dead, but `do chklimit with .t.,"TEMPOWKD",...`
still runs in the save path and `DORDERLIMIT2` still writes `audtrail` rows.
Limit *enforcement* may therefore still execute even though the UI affordance is
gone. Confirm before deciding whether HOMSys reproduces it.

## dbf/ — the tables

Source: `\\Acastillano\setup\ADC\BMSRAM`

All 17 tables `a11102.SCX` touches. **No `.FPT` memo files exist for any of
them**, so `.DBF` + `.CDX` is a complete copy.

Write targets of the SO save transaction:

| Table | Records | .DBF bytes | Role |
|---|---|---|---|
| `oowkhdr` | 29,027 | 57,013,740 | SO header (139 fields) |
| `oowkdet` | 329,051 | 178,677,037 | SO detail |
| `offwkhdr` | 11,731 | 22,962,248 | header mirror, picklist-with-OOS |
| `offwkdet` | 133,302 | 71,452,185 | detail mirror |
| `POFILES` | 9,753 | 1,102,642 | customer-PO log → HOMSys POLOG |
| `MOE` | 0 | 1,128 | manual order exception |

Read during encode and validation:

| Table | Records | .DBF bytes | Role |
|---|---|---|---|
| `cust4win` | 149,626 | 364,792,549 | **real customer master** — all validation |
| `sale4win` | 537,457 | 471,890,423 | sales history, order-limit checks |
| `arof4win` | 466,689 | 267,414,982 | AR open items, aging check |
| `MAXORDER` | 90,457 | 40,616,258 | per customer+SKU order ceilings |
| `vshdr` | 48,406 | 33,837,883 | invoice headers, aging check |
| `PROD4WIN` | 734 | 818,367 | product master → HOMSys Products |
| `CUSTDIR` | 3,003 | 934,614 | **picker list only**, not validation |
| `avail4` | 272 | 69,144 | inventory availability |
| `sysparam` | 8 | 29,272 | branch config, `transdate`, anti-tamper |
| `ZONEMAST` | 114 | 15,241 | zone → chain description |
| `BLOCKSKU` | 0 | 488 | SKUs blocked from ordering |

`dmaxstat` appears in the source but has no file — it is built at runtime.

Note `CUSTDIR` is **not** the customer master despite the flow chart. The form
validates against `cust4win`; `CUSTDIR` only backs the lookup picker. See
`vfp/ANALYSIS.md`.

Source share's newest write was 2026-08-04, two weeks before this copy, so it
behaves as a snapshot area rather than a hot live share.

### Key fields

`POFILES` — `PONUM C(15)`, `PODATE D`, `SONO N(8)`, `ORDERDATE D`,
`CUSTKEY C(7)`, `CUSNAME C(50)`. This is the flow chart's "PONUM": it ties a
customer PO number to the SO number.

`CUSTDIR` — 13 fields: `WHSENO`, `CKEY`, `CUSNAME`, `SALESMAN`, `ADDRLN1/2`,
`CLASSCODE`, `CUSTKEY`, `BLOCKINV`, `DELAREA`, `DELTIME`, `FREIGHTCS`.

`PROD4WIN` — 128 fields; `PRODNO N(4)`, `CPRODNO C(4)`, `PRODDESC C(75)`,
`PIECES`, `QTYPERPC`, `UM C(3)`, `PGR`, pricing columns.

`oowkhdr` — **139 fields**, 1,964 bytes/record. Carries far more than order
entry: downstream workflow state lives here too (`INVNO`, `RSRNO`, `PICKNO`,
`PICKTDATE`, `TRANSMIT`, `STATUS`, `CRAUTHOR`, `DRNO`, `ORNO`, `BIRNO`,
`SERVEDATE`, `FCCRDATE`, `PASSDATE`, ...). The Python bridge should write only
the order-entry subset and leave the rest BMS-owned; that boundary still needs
to be defined explicitly.

## Re-pulling

Both sources are read-only to us. Open with shared access when copying —
production DBFs are frequently locked and a plain read will fail:

```powershell
$in = [IO.File]::Open($src,'Open','Read','ReadWrite')
```

Do not use `Get-FileHash` against the source to verify a copy; it fails on
locked production DBFs even when the copy itself succeeded. Verify structurally
instead: byte length, then DBF header self-consistency
(`headerLen + recs*recLen + 1 == fileLength`).
