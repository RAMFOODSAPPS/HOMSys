# a11102.SCX — how the legacy SO encoding form works

Derived by reading `extracted/a11102.txt` (the decoded form source) and the
staged PRGs in `prg/`. Everything here is cited to a procedure name so it can be
re-checked against the source.

Extraction note: `.SCX` records store `OBJNAME`/`CLASS`/`PARENT`/`PROPERTIES`/
`METHODS` as **memo fields with binary 4-byte little-endian block pointers**, and
the `.SCT` block header is 4-byte big-endian type + 4-byte big-endian length.
See `../../../scratchpad` extractor. Two traps cost real time: treating the memo
pointer as ASCII digits yields plausible-looking truncated output, and in
PowerShell 5.1 `-shl` on a `[byte]` truncates to 8 bits, so every length silently
collapsed to its low byte (229 instead of 41,701). Cast to `[int]` before shifting.

## Form shape

`Form1` — "Data Entry of Sales Orders", 800x550, 25 objects.

| Control | Role |
|---|---|
| `txtcustkey` / `txtcustname` | encode customer (`Valid`, `DblClick`) |
| `txtPonum` / `txtPodate` | encode PO# (`Valid`) |
| `txtInvrem` | remarks, 100 chars |
| `Grid1` | encode prodno — 6 columns, bound to cursor `tempowkd` |
| `cmdSave` | the whole save transaction |
| `cmdmax` | GetMax — **disabled**, see below |
| `txtOrno` / `txtChkdate` / `txtOramt` | O.R. details, `Visible = .F.`, enabled only when customer `term = 0` (cash) |

Grid columns: `cprodno` (inputmask 9999), description (`proddesc + packsize`,
read-only), `qtycs`, `qtypc`, then `getmaxcs` / `getmaxpc` both read-only and
disabled. Red text = `pricelist = .f.`; blue row = quantity changed by GetMax.

## Working cursors

`tempowkh` (header, 1 row) and `tempowkd` (detail) are scratch cursors. Nothing
touches the real tables until `cmdSave.Click`.

## Encode customer — `validcust`

Reads **`cust4win`**, not `CUSTDIR`. This matters: `CUSTDIR` is only the picker
list (`do form valform with "CUSTOMER","CUSTDIR"`, and that line is commented
out). All validation reads `cust4win`.

1. `seek mcustkey1` on order `custkey`.
2. If `blockinv` → refuse. Offers override via H.O. authorization code
   (`do form itcodes with "Add Sales Order", "008"`); a non-empty return
   unblocks.
3. If `tin` is empty → hard refuse, no override.
4. Returns `mblocktoinv`.

`addnew` then pulls the customer context: `ccode` (with `ieffdate` cutover to
`oldccode`), `ckey = substr(custkey,3,5)`, `whseno`, `term`, `csman`, delivery
address (`deladdrln1/2` override `addrln1/2`), `offshore`, `ex_branch`, `tpc`,
`subd`, `vatid`, `czone`, `consomax2`. Cash customers (`term = 0`) enable the
O.R. fields. There is a branch-specific rule: `whseno = 45` customers ordering
from another branch require a row in `aceluzon.dbf` unless `ccode = 3`.

`dchkaging` separately checks collection/invoice aging against `arof4win` +
`vshdr`, and exempts chains whose `zonemast.cdesc` is one of
PUREGOLD / SUPERVALUE / PRINCE / ROBINSONS.

## Encode PO# — `checkponum2`

Runs during save. For each header row with a non-empty `ponum`, seeks `pofiles`
on order `ponum`; **if not found**, appends `ponum, podate, sono, orderdate,
custkey, cusname, sysdate, transdate`.

Note it only logs the first use of a PO number — it does **not** reject
duplicates. PO uniqueness is not enforced here.

## Encode prodno — `Grid1` + `updateproddesc`

`updateproddesc` relates `tempowkd.cprodno` into `prod4win` and fills blank
`proddesc`, `packsize`, `pieces`. `delblocksku` removes SKUs present in
`blocksku` (skipped for customers whose `ckey` starts with `"0"`) and shows form
`block101` listing what was dropped.

## GetMax — disabled

`thisform.cmdmax.enabled = .f.` is the only assignment to that property, so the
button is permanently off. In `cmdSave.Click` the lines that would apply its
result are commented out and dated:

```
*repl all qtycs with getmaxcs   && acastillano 05/18/2024
*repl all qtypc with getmaxpc
```

The `"Perform GETMAX first."` guard is commented out too. **Quantities save
exactly as encoded.**

The machinery still exists and still runs elsewhere: `checklimit` →
`dorderlimit2` → `updatedmaxstat`, plus external `chklimit.prg` / `upmaxstat.prg`
(the form comment says `acastillano 08/20/2019 - transfer to prg`). It reads
`maxorder` keyed `custkey+cprodno`, falling back to `cust4win.aliaskey`, then to
literal `"ALL"+space(4)+cprodno`. Chain-wide limits aggregate across
`cust4win.consomax2` via `getfccoschain`. Multi-month limits sum historical sales
from monthly `SL` files via `renfile()`. Every branch writes `audtrail` rows
tagged `CHECKLIMIT(n)` / `DORDERLIMIT2(n)`.

**Open question for HOMSys:** the button is dead, but does the enforcement path
still execute on save? Worth confirming against a live trace before deciding
whether HOMSys reproduces any of it.

## SAVE — `cmdSave.Click`

Preconditions:

1. At least one `tempowkd` row with a non-empty `cprodno`, and `tempowkh` not
   empty.
2. **Anti-tamper:** if `sysparam.sysdate <> ctod(dtoc(thisform.dlogdatetime))`
   it disables both Save and GetMax and refuses —
   *"YOU ARE UNABLE TO SAVE S.O. AS COMPUTER DATE HAS BEEN TWEAKED."*
   Same concern the TimeServer subsystem addresses.
3. Confirmation messagebox.

Then:

```
msono = docnum("SO", 0, "GETSAVE")      && SO number assigned HERE, at save
```

Header: `docno = msono`, `orderdate = sysparam.transdate`, `expectdel = .f.`,
`ponum`, `podate`, `invrem`, `sotymstart = dlogdatetime`.

Detail: `docno = msono`; rows with empty `cprodno` deleted; then denormalized
from `prod4win` — `proddesc`, `packsize`, `pricelist`, `taxrate`, `supplier`,
`csupplier`, `pieces`. Then quantity normalization:

```
mval  = qtypc + (qtycs * pieces)
qtycs = hconvert(mval, pieces, 1)     && int(mval / pieces)
qtypc = hconvert(mval, pieces, 2)     && mval - int(mval/pieces)*pieces
```

i.e. plain `divmod(total_pieces, pieces)`. Divides by zero if `pieces = 0`.

`checkponum2` runs. Then timing (`sotymend`, `soelapsed = timeuse(3, start, end)`)
and the writes:

| Target | From |
|---|---|
| `oowkhdr` | `temphdr` |
| `offwkhdr` | `temphdr` — *"used for printing picklist with OOS"* |
| `oowkdet` | `tempdet` |
| `offwkdet` | `tempdet` |
| `pofiles` | via `checkponum2` |
| `moe` | `sono` renumbered from placeholder to `msono` |
| audit | `audtrail("BMS-Order Booking","SO",msono,...)` |

**The flow chart shows two write targets; there are really four**, because
`offwkhdr`/`offwkdet` receive identical copies.

## Constraints this puts on HOMSys + the Python bridge

1. **SO numbering.** `docnum.prg` GETSAVE: spin-lock `rlock()` in an unbounded
   `do while .t.` loop, `lastnum+1`, unlock. HOMSys cannot invent SO numbers
   without colliding with BMS. Either the bridge calls the same `docnum.dbf`
   under the same locking discipline, or HOMSys gets a reserved range. This is
   the single biggest design decision.
2. **`oowkhdr` has 139 fields**, most of them downstream workflow state
   (`INVNO`, `RSRNO`, `PICKNO`, `TRANSMIT`, `STATUS`, `CRAUTHOR`, `DRNO`,
   `SERVEDATE`, ...). The bridge writes only the order-entry subset; the rest
   stays BMS-owned. That boundary needs writing down explicitly.
3. **Four tables, not two** — `offwkhdr`/`offwkdet` must stay in sync or
   picklist printing breaks.
4. **`pofiles` row** must be written for each non-empty PO number.
5. Quantity normalization and the `prod4win` denormalization must be reproduced
   exactly, or BMS reports disagree with HOMSys.
6. `a11102.SCX` was modified 2026-07-18 — the rules are still moving. Re-pull
   and re-diff before locking any design.

## External procedures (staged in `prg/`)

`docnum` `hconvert` `qtyloose` `chklimit` `upmaxstat` `audtrail` `recalc16`
`recalc1b` `armatch3` `renfile` `newname` `openfile` `delfile` `timeuse` `depcs`
`getexe` `saveuser` `datauser` `eom` `closedbf`

Not found as standalone `.prg`: `messageb`, `fdate` (likely in a procedure
library or the main program file).
