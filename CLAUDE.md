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

Backend runs on `http://localhost:5200`, frontend on `http://localhost:4400`.
Migrations auto-apply on API startup (`db.Database.Migrate()` in `Program.cs`).

### Conventions — follow these exactly

- Primary-constructor DI: `public class FooService(IFooRepository repo)`
- Services return `(dto, error)` tuples; controllers translate to
  `{ success, data }` / `{ success, message }` envelopes
- `CurrentUser` from `http.HttpContext?.User?.FindFirstValue(ClaimTypes.Name)`
- Register repos + services in `Infrastructure\DependencyInjection.cs`
- The `Site` slice is the cleanest reference implementation

Adding a feature page requires **four** registrations (see the frontend
`CLAUDE.md`): `ROUTE_META` in `tab-bar.service.ts`, a sidebar entry, a route in
`app.routes.ts` with `data: { permission: '...' }`, and a seeded `Permission` +
`RolePermission` in `AppDbContext.OnModelCreating`.

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
