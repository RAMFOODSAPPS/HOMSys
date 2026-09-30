# CLAUDE.md — HOMSYS-MAIN

Guidance for Claude Code when working in this subsystem.

## Overview

**HOMSys** (Head Office Monitoring System) is a web replacement for parts of the
VFP6 BMS. Unlike the rest of `C:\CLAUDE`, this is not a VFP/PowerShell automation
workspace — it is a .NET + Angular application.

```
HOMSYS-MAIN\
  dotnet\     .NET 10 backend (clean architecture) + Angular 19 frontend
  legacy\     read-only snapshots of the BMS system being replaced
```

Root is reserved for further deployments; **Python** ones are planned (the
SQL → DBF bridge).

## dotnet\

| Path | Purpose |
|---|---|
| `backend\HOMSys.Domain` | POCO entities, no dependencies |
| `backend\HOMSys.Application` | DTOs, service classes, repository interfaces |
| `backend\HOMSys.Infrastructure` | EF Core `AppDbContext`, repositories, migrations, DBF import |
| `backend\HOMSys.API` | Controllers, JWT auth, `Program.cs` |
| `frontend\homsys-web` | Angular 19 standalone + PrimeNG — see its own `CLAUDE.md` |

Backend runs on `http://localhost:5200`, frontend on `http://localhost:4200` (`angular.json`; CORS also allows 4400).
Migrations auto-apply on API startup (`db.Database.Migrate()` in `Program.cs`).

### Conventions — follow these exactly

- Primary-constructor DI: `public class FooService(IFooRepository repo)`
- Services return `(dto, error)` tuples; controllers translate to
  `{ success, data }` / `{ success, message }` envelopes
- `CurrentUser` from `http.HttpContext?.User?.FindFirstValue(ClaimTypes.Name)`
- Register repos + services in `Infrastructure\DependencyInjection.cs`
- The `Site` slice is the cleanest reference implementation

Adding a feature page requires **seven** registrations (see the frontend
`CLAUDE.md`): `ROUTE_META` in `tab-bar.service.ts`, sidebar, modulebar, home
launcher card, a route in `app.routes.ts` with `data: { permission: '...' }`, a
seeded `Permission` + `RolePermission` in `AppDbContext.OnModelCreating` (+
migration), and an `AddPolicy` line in `Program.cs`.

## legacy\

Read-only reference material for the BMS system being replaced. **Nothing here is
live and nothing should ever be written back to its source.**

- `legacy\vfp\ANALYSIS.md` — **how the legacy SO encoding form works. Read this
  before touching the sales order module.**
- `legacy\vfp\` — `a11102.SCX` plus the 8 forms it calls, `prg\` (30 procedures),
  `extracted\` (decoded object trees and method bodies)
- `legacy\dbf\` — 17 DBF tables, byte-verified snapshots
- `legacy\README.md` — provenance: source UNC path and copy date per file

Sources: `\\itworks-pc\source\bms` (forms/PRGs) and
`\\Acastillano\setup\ADC\BMSRAM` (tables).

## Sales Order module

HOMSys's **first transactional document module** — everything else is an admin
CRUD master, so it does not fit the existing page pattern exactly.

Replicates the legacy `a11102.SCX` encode flow: customer → PO# → prodno → save.

Key facts that are easy to get wrong:

- **Customer master is `cust4win`, not `CUSTDIR`.** `CUSTDIR` is only the legacy
  lookup picker and is not imported.
- **`SoNo` stays NULL.** HOMSys assigns `SoId` (its own identity). The real BMS
  sales order number comes from `docnum("SO",0,"GETSAVE")` — a spin-locked
  counter in `docnum.dbf` — and is written back by the Python bridge. HOMSys
  never touches `docnum.dbf`.
- **A duplicate PO number warns, it does not block.** The legacy
  `txtPonum.Valid` shows an OK-only messagebox then a bare `RETURN` (which is
  `.T.` in VFP), so the value is kept. Never turn this into a blocking
  validation.
- **Quantity normalisation** is plain divmod, from `hconvert.prg`:
  `total = qtyPc + qtyCs*pieces`, then `qtyCs, qtyPc = divmod(total, pieces)`.
  Guard `pieces = 0` — the legacy code divides by zero there.
- `SalesOrder`/`SalesOrderLine` mirror `oowkhdr`/`oowkdet` and are split into
  **ENCODE-OWNED** and **BMS-OWNED** regions. Never write a BMS-owned column
  from HOMSys.

Deliberately **not** implemented: order limits (`chklimit`/`maxorder`), AR aging,
`blockinv`/TIN gates, blocked-SKU removal, GetMax (dead in the legacy form since
2024-05-18). `SalesOrderLine.Price/Amt/NetAmt` remain BMS-owned/NULL, written
only by the future Python bridge.

**LP w/ VAT display** (Parts 2–3 of
`C:\Users\RDEGUZMAN\.claude\plans\can-you-see-this-jaunty-puffin.md`, done):
`PriceCalculationService` (`HOMSys.Application\Services`) computes
`(BasePrice + zone.ADD_ON + zone2.ADD_ON) × 1.12` per the Pricing Adjustment
subsystem's own formula, exposed read-only at `GET /api/pricing/quote?
cProdNo=&custKey=` (`PricingController`). The pricing branch is resolved per
customer from their `CustomerZone` row's `Branch` (not `Customer.WhseNo` —
that BMSRAM field does **not** correspond to the `wh` numbering in
`config-*.json`; nearly every live customer's `WhseNo` falls outside it, so
resolving branch that way silently defaulted almost everyone to `hon`, caught
by live spot-check 2026-08-19). See "Pricing masters import" below for the
corrected two-lookup design. The encode grid
(`sales-order-page.component.ts`) calls this on product lookup and on
customer lookup, and shows the result in a "LP w/ VAT" column — purely
display-only, never written to `SalesOrderLine.Price/Amt/NetAmt` or sent in
the save payload.

### Invoice cancellation (a1174) and RFC returns (c1110k2 RSR → RFC)

Both screens are patched in `legacy\vfp\` to call
`run_bridge.bat <marker> <dir> <so#> <action> [invno]` (`%5` = invno). Every
event goes through the offline outbox.

| Screen | Action | What BMS does | HOMSys result |
|---|---|---|---|
| `a1174.SCX` Invoice Cancellation (`CmdCancel.Click`) | `CANCELINV` | logs `DOCCANCEL.DBF` (`canceld.prg`), then **deletes** `oowkhdr`/`oowkdet` (or `oocuhdr`/`oocudet`) | **Cancelled**, which is void and terminal |
| `c1110k2.SCX` **toRFC** (`CmdToRfc.Click` → `updaterfc`): approved RSR (Request for Stock Returns) → RFC | `RFCPOST` (so# `0`, `"D"+DTOS(sysparam.transdate)`) | creates an **auto-posted** RFC per RSR (`POSTED = sysparam.transdate`, `REFNO1`=invoice, `REFTYPE3="CRSR"`/`REFNO3`=RSR#, `REMARKS2`=RSR remarks); the order record **stays** | **Full RFC** or **Invoiced with RFC** for every invoice with an RFC posted on/after that date |

**Cancel (a1174).** `POST bridge/invoice-cancel?branch=` →
`CancelInvoiceAsync` sets `CancelledInvNo`/`InvCancelDate`/`Remarks`/`CancelledBy`
and `WorkflowStatus = "Cancelled"`. The order is matched on SO#, else on
InvNo/CancelledInvNo. **The invoice details are kept**: `InvNo`/`InvDate`/`InvAmt`,
OOS lines and delivery fields, at the user's request (2026-09-28). Missing details
are filled in from `DOCCANCEL` `DOCDATE`/`AMOUNT`. Analytics excludes Cancelled
(`Live` const in `AnalyticsCatalog`). Delivery status shows `"C"` → "Cancelled".

**RFC.** Both RFC kinds stamp the **same header**: `REFTYPE1="INVOICE"`,
`REFNO1`=inv, `REFTYPE2="RSR"`. So full vs partial is decided **by quantities**,
never by header fields. (An earlier reconcile treated any RSR RFC as a cancel,
which would have cancelled 60 real invoices in the test data. Fixed.)
- **Bridge.** `read_rfcs` (`scan_fields` over `imtr_hdr` + `imtr_det`) collects
  **posted** RFCs only (`STATUS="2"`). It posts per-invoice snapshots as a batch to
  `POST bridge/invoice-rfcs?branch=`, which always returns 200 `{matched}`.
  Outbox events: `rfc {so_no, inv_no}` (RFCINV) and `rfc_scan {rfc_nos, since}`
  (RFCPOST). **Only RFCs not uploaded before are sent.** The bridge keeps
  `homsys_outbox\rfc_sent.json` (`{inv: [rfc#...]}`, in the shared folder, written
  under the drain lock). An invoice with nothing new isn't sent at all. RFCs are
  recorded only for invoices HOMSys reports as matched (`matchedInvNos`). If the
  list is missing, it's seeded from HOMSys's own `RfcNos`. The RFC gap check
  (every bridge run) and the reconcile compare against HOMSys's `RfcNos`, send
  only the missing RFCs, and record them, which heals drift such as a DB restore.
  If `imtr_hdr`/`imtr_det` is missing, the sync is skipped (no outbox block).
- **Server.** `SyncRfcsAsync` **merges**: it adds new RFCs, rewrites an existing
  one only if its content differs, and **never deletes** (a posted RFC can't be
  cancelled in BMS). It returns the invoices that matched. It then recomputes
  the status over all the order's RFCs:
  - **Full RFC** when returned pieces ≥ invoiced pieces for every invoiced SKU.
    Invoiced = the INV CS snapshot (`OosSyncLines`), falling back to the encoded
    quantities. Pieces come from `SalesOrderLine.Pieces`, since `imtr_det.PIECES`
    is often 0.
  - **Invoiced with RFC** otherwise.
  - No RFCs → reverts to Invoiced.
  - Precedence: Cancelled > Full RFC > Invoiced with RFC.
  - An invoice (re)sync never resets Cancelled / Full RFC / Invoiced with RFC
    (`ConfirmInvoiceAsync` only fills a missing InvNo/date/amount). Full RFC is
    excluded from reconcile candidates. A posted RFC can't be cancelled in BMS,
    so Full RFC never needs to revert. Delivery status shows `"R"` → "Returned".
  - `GetForRfcSyncAsync` accepts an SO# match only if that order's
    InvNo/CancelledInvNo agrees with the RFC's invoice (or InvNo is still
    null); otherwise it matches by InvNo.
- **Stored data.** Raw `imtr_det` fields: `SpAmt` (selling amount **ex-VAT**), `Tax`
  (12% VAT), `Amt` (**cost**). **"RFC Amount" = `SpAmt + Tax`** (VAT-inclusive like
  `InvAmt`; verified on RFC 87139572: 1,599.10 + 191.89), `DiscAmt1/2`, `RetCode`, `RsNo`, line `Remarks`. Header:
  `REMARKS`/`REMARKS2`, user, dates. RFC'd invoices stay **Live** in analytics
  (gross), with new `rfcAmt` / `netInvAmt` measures and a `hasRfc` flag.
- **UI.** Status tags; an **RFC Returns** section in the SO view (below Delivery /
  Invoice Cancellation); an **RFC Qty** column next to INV CS (cases + loose
  pieces). The Sales Orders list has **RFC** (count, clickable → dialog of that
  invoice's RFC numbers → clicking one opens View — SO#) and **RFC Amt** columns
  (`SalesOrderDto.RfcCount`/`RfcAmt`; `GetAllAsync` includes `Rfcs.Lines`).
- **Out of scope:** RGW auto-RFCs (`cmdautorfc`, invoice in `REFNO2`) and
  unposted RFCs.

### Offline resilience (bridge outbox)

A branch can lose internet while BMS keeps running on its LAN share. Rule:
**VFP never waits on HOMSys, and no BMS event is lost. HOMSys catches up late.**

- **Outbox.** Every single-SO action (`PROCESS`→lock+oos, `DEALLOCATE`,
  default→invoice+oos, `DELIVERED`, `CANCELINV`, `RFCINV`/`RFCPOST`) is first written as one
  JSON file to `<bms data folder>\homsys_outbox\` (`watcher\outbox.py`). It's a
  shared LAN folder, so any PC can deliver the event. The marker is touched
  **right after**, so VFP resumes in about 1 s even offline (the old flow could
  freeze 10–15 s). Delivery is oldest-first under `.drain.lock`. A 404 (not a
  HOMSys order) is dropped; any other 4xx is parked in `homsys_outbox\failed\`;
  a network error, timeout, 5xx or 401 keeps the file and stops the drain.
  Delivery is at-least-once, and every endpoint is idempotent.
  **Processed backlog is never re-sent**: `homsys_outbox\sent_events.json` keeps a
  fingerprint of the last content uploaded per key (`confirm:<so>`,
  `state:<so>` for lock/deallocate, `invoice:<so>`, `oos:<so>`, `delivery:<so>`,
  `cancel:<inv>`). An event whose content matches is skipped (`send_once`). A
  failure or a 404 is never recorded. Reconcile posts with `force=True` (it acts
  on HOMSys's own state) and records what it sends. RFCs use `rfc_sent.json`.
  **Strict FIFO**: file names start with a timestamp, strictly increasing
  within a process (`_next_stamp`), because Windows `time_ns` repeats within a
  tick. One drainer at a time. The folder is re-listed after every event, and
  the drain stops at the first network failure, so nothing jumps ahead. A new
  order's SoNo confirm is a `confirm` outbox event, queued the moment
  `APPENDED` is seen (it works offline), so it always precedes that order's
  later events. The reconcile runs only inside the drain lock, once the queue
  is empty. Only cross-PC clock skew can reorder events recorded on
  different PCs within the same few seconds. lock, deallocate,
  invoice, delivery and cancel re-read BMS when delivered. oos carries its
  oowkdet snapshot from event time, because BMS's deallocate later wipes it.
- **by-sono endpoints.** `bridge/by-sono/{soNo}/lock|deallocate|invoice|oos-status?branch=`
  replaced the old per-workstation ledger lookups (`%LOCALAPPDATA%`), which
  silently skipped events fired from a PC other than the one that downloaded the
  order. The ledger is now only used for the SO#-confirm step (only the
  claiming PC can confirm). The soId routes remain for bridges not yet upgraded.
- **Drain task.** `SalesOrderBridge.exe --drain [datadir]`, registered on one
  always-on PC per branch by `watcher\register-bridge-drain-task.ps1`, runs
  every 5 min: ledger confirm, outbox drain, heartbeat, reconcile.
  `bms-client.ps1` is **not** involved; it only delivers the `homsys_*` keys
  into `C:\fox\client\config.json`.
- **Reconciliation sweep** (at most every 30 min, `.last_reconcile`). It pulls
  `bridge/reconcile-candidates` (in-BMS, non-Cancelled orders from the last 60
  days) and compares them with BMS: `oocuhdr`+`oowkhdr` STATUS/INVNO, VSHDR,
  DOCCANCEL, and the RFC in `imtr_hdr`. It re-posts cancel, invoice,
  lock/deallocate (STATUS outside `{"", "1", "B"}` counts as processed) and
  delivery gaps. This catches events that were never captured.
  `dbf.reader.scan_fields` keeps it cheap: ~0.4 s for 45k vshdr rows.
- **Heartbeat and stale guard.** Every bridge run that reaches HOMSys writes
  `SyncLog` section `SoBridge:{branch}` (`bridge/heartbeat`; no new table).
  `SalesOrderService.UpdateAsync` refuses to edit an order already in BMS when
  that branch's heartbeat is older than `SalesOrderBridgeService.StaleAfter`
  (15 min). The reason: a Processed lock may be sitting undelivered in the
  outbox. If a branch has no heartbeat row at all (not upgraded yet), the edit
  is allowed. The Sales Orders page shows a warning banner for stale branches
  (`GET /api/salesorders/bridge-status`).

### One SO# per order (download claim)

On 2026-09-29, SoId 2043 was downloaded twice, as 88265761 and 88265762. Its first confirm sat undelivered in the outbox, and `/pending` still listed it. Two guards now prevent that:

1. **Server claim.** `process_order` calls `POST bridge/{soId}/claim?branch=` `{claimedBy: COMPUTERNAME}` *before* it takes a SO# from `docnum.dbf`.
   - It is one atomic `UPDATE … WHERE SoNo IS NULL AND (BridgeClaimedBy IS NULL OR = me)`, so only one PC ever wins. A **409** response means skip the order.
   - `/pending?claimant=` hides orders claimed by other PCs.
   - `UpdateAsync` refuses edits while an order is claimed and not yet confirmed.
   - The list shows such orders as "downloading…".
   - **A claim never expires.** If an order is stuck with a claimed PC that no longer exists, first check that branch's BMS for the order. Then clear it by hand: `UPDATE SalesOrders SET BridgeClaimedAt = NULL, BridgeClaimedBy = NULL WHERE SoId = …`.
2. **Bridge `_unclaimed`.** This covers old claims and lost state. It skips any order whose confirm is still queued in the shared outbox. If this PC's ledger says the order is already claimed and appended, it re-queues that confirm (forced) instead of taking a new SO#.

### Offshore Encoder (HON / LKA-HO → For Branch)

This automates the old UTIL18 TCODE 31 (`download7`) → emailed `B#######.ZIP` → UTIL19 `uploadoowk` flow.
- **Encode.** Permission 15 `offshore-encode` is seeded with the **Offshore Encoder** role (8, 13, 15). Holders must pick **For Branch**. The options are Sites flagged `AcceptsOffshoreOrders` (the Sites page toggle), from `GET salesorders/offshore-branches`.
- **Fields.** `SalesOrder.OriginBranch` is the encoder's branch and never changes. `ForBranch` is the target. `Branch` is still "which bridge owns it": it flips to `ForBranch` on upload. Because of that flip, every `(SoNo, Branch)` by-sono, reconcile and heartbeat path follows the order unchanged. The list, GetById and analytics `SalesScope` show an order to `Branch` **or** `OriginBranch`.
- **Origin.** `write_order` stages For-Branch orders with **`OFFSHORE=.T.`**, the same as a11102 forces at bcode 28/88. That flag skips allocation, keeps the order **out of the origin's Print Picklist** (`optn_init5` requires `.not. offshore`) and routes it to Confirm Clean Orders.
  - `offshore_scan` runs on every bulk run: invoice form **Init/Refresh** via `drunbridge(0)`, and the 5-minute drain as catch-up. It uploads an `offshore-awaiting` SO# once live oowkhdr meets `ready_for_handoff`: status 4/E/F with offshore. That is the `optn_init4` Confirm Clean Orders list plus F, meaning Process, FCCOS and verify are done, with `optn_init5`'s exclusions (For Allocation / DUS PROCESSED / BEYOND HARD LIMIT).
  - Each upload is queued as outbox `offshore_upload`, carrying the full text-type oowkhdr/oowkdet/oowkdis rows with `ORIGQTY = QTY`. The scan also stages `homsys_offshore_sent\<so#>\`.
  - In the same Refresh click, `pMarkOffshoreSent` sets the origin copy to **E "To ADC-Picklist"**, as UTIL18 did. It stays visible in Confirm Clean Orders, as in legacy (user decision), so staff must not confirm it again.
- **Server.** `offshore-upload` stores `OffshoreTransfer` (JSON rows), sets `Branch = ForBranch`, and sets status **Transferring**. It returns **409** if the target already has that SO#; the outbox parks the event and `OffshoreError` shows on the order.
- **Target.**
  - `offshore_receive` stages `offshore-inbound` into `homsys_offshore_in\<so#>\`. The stage is built in `.tmp` and renamed into place.
  - `pAppendOffshore` (UTIL19 rules) appends it as **5 "PickListed", PICKNO 0, `OFFSHORE=.F.`**, keeping the origin SO#. That puts it in the target's Print Picklist. It also writes the offwk* mirrors and an appfccos row. An SO# already in oowkhdr or appfccos is flagged `DUP` instead.
  - The bridge queues `offshore_received` and the order becomes **Transferred**. It keeps a `REPORTED.txt` folder until HOMSys stops listing the order.
  - Reconcile skips offshore orders until they are received.
- **Not carried:** oowkhdr's four T (datetime) columns (encode-speed stamps) and tparprod.

### Reference data import — removed

The BMSRAM pull-sync (`ReferenceDataImporter`, `import-reference-data` CLI
verb, `ReferenceController`, and the Legacy Monitoring page's "Reference
Data" section) was removed 2026-09-05 — the architecture is push-only
(HOMSys → BMS via `SalesOrderBridge.exe`), not pull. `Customer`/`Product`
tables and their repositories are unchanged and still power the Sales Order
module; only the automated/manual sync-from-BMSRAM mechanism is gone, so
nothing currently populates new `Customer`/`Product` rows going forward —
`PricingDataImporter`'s product-pricing import is update-only (matched on
existing `ProdNo`, never inserts).

### Pricing masters import

Separate importer, separate source tree — reads **HO's own production drive
(`F:\`) directly**, across every branch found on disk, not one branch's
staged copy:

```powershell
dotnet run --project backend\HOMSys.API -- import-HoMaster-data [root]
```

Default root `F:\` (expects `\PMDM`, `\AUTOPROG\ADDON\{branch}`,
`\AUTOPROG\CUSTOMER\{branch}` under it — read-only, never written to, per
this repo's own golden rule). Updates existing `Product` rows (matched on
`ProdNo`) with `NewPrice`/`PriceFrom`/`OldPrice1`/`Srp` from
`PMDM\PROD4WIN.DBF`; diffs `PriceHistory` from `PMDM\PRCHST.DBF`
(no branch scope — national); diffs `ZoneAddOn`/`Zone2AddOn`
**per branch**, looping over every folder found under
`AUTOPROG\ADDON\{branch}\ZONE.DBF`/`ZONE2.DBF` — branches are **discovered
from disk, not hardcoded**, so a new branch folder is picked up on the next
run with no code change. Likewise `CustomerZone` (new table) is
truncated+reloaded per branch from `AUTOPROG\CUSTOMER\{branch}\CUST4WIN.DBF`
— the pricing-lookup source of truth for CZone, since `Customer.CZone` (from
the BMSRAM share via `ReferenceDataImporter`) is a single install's copy and
can lag branch-side updates. `ZONEMAST.DBF` is deliberately not imported —
it's only needed for the write-path (defaulting a brand-new zone row), not
the price-lookup read-path this feature needs.

**Branch resolution for a quote** (`PriceCalculationService`) starts from the
customer's own `CustomerZone` row (`CustKey` is branch-unique in practice),
which carries both the real branch tag and `CZone` in one place — ground
truth read straight from `F:\AUTOPROG\CUSTOMER\{branch}\CUST4WIN.DBF`. Only
if no `CustomerZone` row exists yet does it fall back to the BMSRAM-sourced
`Customer.CZone` and the `hon` default branch.

The `ADDON` folder name and the `CUSTOMER` folder name don't line up 1:1 —
six branches (Dagupan/`DAG`, Isabela/`ISA`, Legazpi/`LEG`, Lucena/`LUC`,
Mexico/`mxs`, Naga/`NAG`) keep their own `CUST4WIN.DBF` but have no `ADDON`
folder of their own, pricing instead off `hon`'s `ZONE`/`ZONE2`. A small
static `CustomerBranchToPricingFolder` dictionary in the service maps just
those six to `"hon"`; every other branch name is used as-is (`GetValueOrDefault`
falls back to the `CustomerZone` row's own `Branch` unchanged). Verified
2026-08-19 against a real `DAG` customer (`2100226`, `CZone=2105`) resolving
correctly through `hon`'s zone tables with its own distinct `CZone`.

**Change-detection** (CLI path only — `import-HoMaster-data` /
`PricingDataImporter`, the on-prem full-table importer described above; the
watcher's own path is separate, see "Keeping it running" below): two layers,
not one global gate.

1. *File-level gate* — `ImportAllAsync` stats the two national PMDM files plus
   every discovered branch's `ZONE`/`ZONE2`/`CUST4WIN` files individually and
   compares each one's `LastWriteTimeUtc` against a per-file marker stored in
   `%ProgramData%\HOMSys\.last-pricing-import.json` (not on `F:\` — that drive
   is read-only), written after the previous successful run. A file whose
   mtime hasn't moved is skipped entirely — its import method isn't even
   called. If nothing changed anywhere, it logs "No changes since last sync —
   skipping." and returns immediately without opening a DB connection. A
   branch's active-list membership changing (added/removed from
   `ActiveBranches`) also forces a re-check of that branch's files and
   triggers the stale-branch purge.

2. *Row-level diff* — for a file whose mtime *did* change, the corresponding
   `Import*Async` method no longer truncates+reloads the whole table. It loads
   existing DB rows for that branch/table into a dictionary keyed by a stable
   identity — `RecNo` (the DBF's physical record number, matching VFP's own
   `RECNO()`) for `ZoneAddOn`/`Zone2AddOn`/`CustomerBranchZone`/`PriceHistory`,
   since their natural keys can legitimately repeat (duplicate `EFF_DATE`
   rows — see the `Zone2 Active-Row Tiebreak` note elsewhere; confirmed for
   `PRCHST.DBF` too — e.g. `ProdNo=4805` has two rows both dated `9/1/2013`),
   or the true natural key (`CategoryCode`) for `ProductCategory` — then
   diffs against the freshly-read DBF rows: unmatched DBF rows insert,
   matched rows update only if a field actually differs, and DB rows left
   unmatched (no longer in the DBF) get deleted. Only genuinely changed
   rows are written to SQL — e.g. a single
   edited row in `hon`'s 105k-row `ZONE.DBF` now writes one row, not 105k.
   `ImportProductPricesAsync` (`Product` pricing columns) was already a proper
   diff via EF change-tracking and needed no rework.
   Existing rows from before `RecNo` existed all default to `RecNo=0` — the
   first diff for a branch after this shipped self-heals by purging those and
   letting every DBF row insert fresh with its real `RecNo` (a one-time full
   rewrite per branch/table, logged as "First diff since RecNo backfill").
   Every sync after that is truly incremental.

**Keeping it running**: `watcher\legacy_master_watcher.py` — a single-shot
Python script (no loop of its own) that reads and parses `F:\`'s pricing
DBFs **itself** and POSTs once to each configured sync endpoint before
exiting; Task Scheduler owns the recurrence, not the script. This exists
because Azure App Service can't reach `F:\` — the read has to happen on a
machine that can, which means the exe, not the API. It uses
`watcher\dbf_reader.py`, a byte-for-byte Python port of
`Infrastructure\Data\Dbf\DbfReader.cs` (same header/field-descriptor
offsets, same generic text-then-typed-parse behavior, same 1-based
physical-slot `RecNo` numbering that skips deleted records without
renumbering), so its `RecNo` values line up exactly with what's already in
SQL.

Each run parses the full current state of every pricing DBF (national files
under `PMDM\`, per-branch `ZONE`/`ZONE2`/`CUST4WIN` — branches are
discovered from disk, same folder-walk convention as `PricingDataImporter`,
no hardcoded branch list needed since the server enforces `ActiveBranches`
when applying the delta) and diffs it against its own last-synced snapshot
at `%LOCALAPPDATA%\HOMSys\pricing-snapshot.json`, keyed the same way as the
SQL tables (`ProdNo` / `CategoryCode` / `(CProdNo,Zone)` / `RecNo` /
`(Branch,RecNo)`). Only genuinely changed rows go in the POST body to
`/api/masters/sync`; a quiet run sends an empty body. The snapshot is only
overwritten after a successful POST. Customers/Products are no longer synced
from BMSRAM at all (see "Reference data import — removed" above).

Server-side, `PricingController.Sync` now binds a `PricingSyncDeltaRequest`
(`HOMSys.Application\DTOs\Pricing`) and hands it to `PricingDeltaImporter`
(`HOMSys.Infrastructure\Data`, sibling to `PricingDataImporter`, not a
replacement — the CLI path above still uses the older full-table importer).
`PricingDeltaImporter` queries only the keys present in the payload, not
the whole table, and enforces `PricingDataImporter.ActiveBranches` as the
per-branch allowlist. Auth is a static API key (`X-Api-Key` header, checked
against `HeadlessApiKey` in `appsettings.json`/env override) rather than
JWT — the watcher is a headless service, not a logged-in user, so `/sync`
is `[AllowAnonymous]` at the MVC level but gated by the key check inside
the action.

Packaged as `watcher\dist\LegacyMasterWatcher.exe` via PyInstaller
(`pyinstaller --onefile --name LegacyMasterWatcher --console
legacy_master_watcher.py`, run from `watcher\`) so it's directly runnable
from Task Scheduler with no Python install on the target VM.
`watcher\register-task.ps1` registers it (5-min repeating trigger + at-logon
trigger, matching this repo's own schtasks convention — see
`reference_schtasks_onlogon_no_repetition` memory: a logon-only trigger is
dormant until next logon, always paired with a clock trigger). Config is via
a plain `config.json` next to the exe (copy `config.example.json` and fill
it in — no env vars, no `setx`, no reboot needed): the API key, and per
entry a URL plus an optional `path` — the **local** root this exe reads
pricing DBFs from directly (defaults to `F:\`), never sent to the server.
Logs to `%LOCALAPPDATA%\HOMSys\legacy_master_watcher.log`.

`AppDbContext`'s `CommandTimeout` is 120s (`DependencyInjection.cs`) — the
default 30s was observed failing mid-batch on the ~105k-row `ZoneAddOn`
truncate+reload under load.

## Working with DBF files

`Infrastructure\Data\Dbf\DbfReader.cs` handles reads. Two traps already paid for:

- Open with `FileShare.ReadWrite`. Production DBFs are frequently locked and a
  plain read throws "being used by another process".
- If parsing DBFs in **PowerShell**, `-shl` on a `[byte]` truncates to 8 bits in
  PS 5.1 — cast to `[int]` first, or every multi-byte length silently collapses
  to its low byte.

Verify a DBF copy structurally, not with `Get-FileHash` (which fails on locked
production files): byte length, then
`headerLen + recs*recLen + 1 == fileLength`.

## Data Analytics module

User-customizable reports + dashboards (replaced the fixed Sales Analytics page on 2026-09-24; `/sales-order-analytics` redirects to `/home`, which embeds the seeded **Sales Overview** system dashboard). SQL-only — it analyses whatever HOMSysDb holds.

- **Semantic layer:** `Infrastructure\Analytics\AnalyticsCatalog.cs` — a code-defined dataset registry (`Ds`/`Fld` records). 10 datasets: `sales_orders`, `sales_lines` (pre-aggregated per SoId+CProdNo, OOS/fill rate, est. value at the ORDER-DATE list price), `po_logs`, `customers`, `products`, `price_history`, `zone_addons`, `zone2_addons` (both "in force today"), `users`, `user_sessions`. **Adding a dataset for a new SQL table = one more `Ds` entry**; the UI is driven by `GET /api/analytics/meta`.
- **Traps neutralised in the catalog:** curated fields only (BMS-owned NULL columns hidden); LEFT JOIN only on unique keys, every soft join is `OUTER APPLY TOP 1` (Customers.CustKey is not unique — a plain join doubles ~36% of orders); conformed field keys (`branch`, `custKey`, `cProdNo`, `orderDate`…) so cross-filtering is key equality; dates before 2000 bucket to "(invalid date)".
- **Engine:** `AnalyticsSqlBuilder.cs` — whitelist + typed `SqlParameter`s only (no client text in SQL), `GROUP BY GROUPING SETS` for totals/subtotals, Asia/Manila buckets (`DATEADD(hour, 8, …)`; `AnalyticsService.TodayPh()` — never `DateTime.Today`), top-N + exact "(Others)", zero-fill, previous-period KPI compare. Runs on its own `SqlConnection` (`ConnectionStrings:Analytics` if set — recommended read-only login — else `DefaultConnection`), 30 s timeout, caps 5,000 rows UI / 100,000 Excel.
- **Branch RLS is injected server-side from the JWT `branch` claim on every query/values/export/drill** (sales = encoder's `SalesOrders.Branch`, same as the Sales Orders list; folder datasets = pricing folder + hon `Cuwhsenos`). A dataset must be `National` or declare a `Scope` (startup assert). Shared items always run with the VIEWER's scope.
- **Persistence:** one table `SavedReports` (Kind report|dashboard, DefinitionJson, OwnerUserId, IsSystem, SharedRoleIds CSV). Dashboards embed copies of widget specs. Specs are validated on save.
- **Permissions:** `data-analytics` (13; build/view/share own) and `data-analytics-admin` (14; publish system templates, edit any shared item). Migration `AddDataAnalytics` granted 13 to every role holding `sales-orders`; grant COO/ASM on the Authorization page.
- **RFC widgets (Sales Overview):** migration `AddRfcWidgetsToSalesOverview` **appends** 7 widgets (`rfc1`–`rfc7`) to the system dashboard's widget array: RFC Amount, Net Invoiced, Orders with RFC and Full RFC Orders KPIs; RFC Amount by Branch; Top Customers by RFC Amount; and an Orders with RFC Returns table. It's idempotent (skipped if `rfc1` exists) and never replaces the definition, so admin edits survive. `Down` removes only `rfc1`–`rfc7`.
- **Money:** only `SalesOrders.InvAmt` (invoiced), `OosSyncLines.NetAmt` (line, since 2026-09-01; reconciles to InvAmt) and `ReceivedAmt` are real; `estAmt` is an estimate and labelled so.
