#!/usr/bin/env python3
"""Single-shot HOMSys -> BMS Sales Order write-back bridge, fired
synchronously (fire-and-forget + polled) from invoice.SCX's Form1.Init
in live VFP6 sessions — NOT Task Scheduler. VFP launches this exe via
native RUN /N7 (non-blocking) and polls for the marker file this script
touches on exit, capped at ~15s; it opens Invoice Processing regardless
of whether this run finished, timed out, or errored. Concurrent launches
from different workstations are safe without a single-instance guard:
the docnum.dbf record lock and the header locks around table appends
already serialize the real writes.

Usage: SalesOrderBridge.exe <marker-file-path> [bms-directory] [so-no]
The single-SO path also syncs the live oowkdet snapshot for the HOMSys OOS
report on every call -- both from recover_one (invoice.SCX's existing
drunbridge(soNo) at Forward-to-Invoice / printinvoice2) and from lock_order
(drunbridge(soNo, "PROCESS"), the moment BMS processes the order) -- no
separate FoxPro edit or call site needed. See sync_oos() below.
The marker file is touched (empty) on exit no matter how main() ends,
so VFP's poll loop never waits the full timeout after a fast run.
bms-directory should be VFP's own SYS(5)+SYS(2003) at call time (the
directory this BMS session is actually running from right now) — it
overrides HOMSYS_DBF_ROOT, which is a static per-machine fallback for
manual/standalone runs outside VFP.

Finds HOMSys Sales Orders with SoNo IS NULL (not yet pushed to BMS),
claims a real SO number from docnum.dbf under BMS's own spin-lock
discipline, and stages oowkhdr/oowkdet + a POFILES row (if PoNum is set)
into a per-order scratch DBF folder under <dbf_root>\homsys_queue\<so_no>\.

This script never writes the live oowkhdr/oowkdet/pofiles tables itself
and never confirms an order back to HOMSys at staging time -- a method
embedded directly in invoice.SCX reads each staged folder and does the
actual live-table writes via native APPEND BLANK + GATHER (keeping every
open .CDX tag in sync, which a raw Python byte-append never did -- SET
ORDER TO docno would not show Python-appended rows). invoice.SCX leaves its
trace of that in the staged stg_oowkhdr.dbf's own APPENDED field rather
than the live table. Confirmation to HOMSys happens on THIS script's next
run, once its recovery pass sees that field set -- and it deletes the
queue folder itself right after confirming. offwkhdr/offwkdet (picklist
mirrors) and audtrail are explicitly OUT OF SCOPE for this bridge, per
direct user instruction.

Config comes from C:\fox\client\config.json — the same per-branch file
BMS_auto_Distribution's bms-client.ps1 already deploys, so no separate
per-machine setup step. Keys read from it (alongside its existing
tenant_id/branch_id/destination/etc.):
  homsys_api_url    base URL, e.g. "http://homsys-host:5200"
  homsys_api_key    must match HeadlessApiKey (shared with the legacy
                    master watcher's pricing sync — one API key for both)
  destination       reused as-is — same folder bms-client.ps1 already
                    installs BMS into; contains oowkhdr.dbf, oowkdet.dbf,
                    pofiles.dbf, docnum.dbf. Only used here as a manual-run
                    fallback anyway — invoice.SCX normally passes the live
                    directory as argv[2]. Must be the branch's actual live
                    BMS path, not a stale/local test folder.
  homsys_branch     must exactly match this branch's Users.BranchCode value
                    in HOMSys SQL (NOT branch_id — that's the separate BMS
                    distribution code and the two are not always equal,
                    e.g. CDC's branch_id is "CDC" but Users.BranchCode is
                    "CDC-B") — /pending is filtered to this branch's orders

Falls back to environment variables (HOMSYS_BRIDGE_API_URL,
HOMSYS_BRIDGE_API_KEY, HOMSYS_DBF_ROOT, HOMSYS_BRANCH) for any value
missing from config.json, so a machine not yet migrated still works.

Logs to %LOCALAPPDATA%\\HOMSys\\salesorder_bridge.log. Ledger at
%LOCALAPPDATA%\\HOMSys\\bridge-ledger.jsonl is the crash-recovery
durability point: a claimed SO number is appended there before any
further DBF write, so a mid-run crash never re-claims or loses a number.

FIELD_MAP below is this bridge's single point of truth for oowkhdr/
oowkdet column names. It reflects ANALYSIS.md's field-level notes on
cmdSave.Click but has NOT been byte-verified against a live header dump
yet — per the approved plan's verification step 1, confirm every name in
FIELD_MAP against the real table headers (dbf.reader.DbfTable(path).fields)
on staged copies before this script ever points at a live share.
"""
from __future__ import annotations

import json
import logging
import os
import shutil
import sys
from datetime import date, datetime

import requests

from dbf.docnum import claim_number
from dbf.reader import DbfTable, get_bool, get_date, get_decimal, get_int
from dbf.stage import APPENDED_FIELD, OOWKHDR_RESYNC_FIELDS, build_resync_stage_tables, build_stage_tables
from dbf.writer import DbfWriter

CONFIG_JSON_PATH = r"C:\fox\client\config.json"

LOG_DIR = os.path.join(os.environ.get("LOCALAPPDATA", "."), "HOMSys")
os.makedirs(LOG_DIR, exist_ok=True)
LOG_FILE = os.path.join(LOG_DIR, "salesorder_bridge.log")
LEDGER_FILE = os.path.join(LOG_DIR, "bridge-ledger.jsonl")

HTTP_TIMEOUT = 5  # bounded so this exe can't outlast VFP's ~15s poll window
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s %(message)s",
    handlers=[logging.FileHandler(LOG_FILE)],
)
log = logging.getLogger("salesorder_bridge")

# HOMSys order/line field name -> oowkhdr/oowkdet DBF field name.
# See module docstring: unverified against a live header, confirm first.
HEADER_FIELD_MAP = {
    "DocNo": "DOCNO",
    "CustKey": "CUSTKEY",
    "CusName": "CUSNAME",
    "CKey": "CKEY",
    "OrderDate": "ORDERDATE",
    "PoNum": "PONUM",
    "PoDate": "PODATE",
    "InvRem": "INVREM",
    "CCode": "CCODE",
    "WhseNo": "WHSENO",
    "ShipToLn1": "SHIPTOLN1",
    "ShipToLn2": "SHIPTOLN2",
    "Term": "TERM",
    "TermDays": "TERMDAYS",
    "Salesman": "SALESMAN",
    "CsMan": "CSMAN",
    "ServeWh": "SERVEWH",
    "DelWhse": "DELWHSE",
}
DETAIL_FIELD_MAP = {
    "DocNo": "DOCNO",
    "ProdNo": "PRODNO",
    "CProdNo": "CPRODNO",
    "ProdDesc": "PRODDESC",
    "PackSize": "PACKSIZE",
    "QtyCs": "QTYCS",
    "QtyPc": "QTYPC",
    "Pieces": "PIECES",
    "Um": "UM",
    "Supplier": "SUPPLIER",
    "CSupplier": "CSUPPLIER",
    "PriceList": "PRICELIST",
    "TaxRate": "TAXRATE",
}

# Constants the legacy cmdSave.Click always writes regardless of order data
# (a11102.txt lines 784-794, 2290-2330). Not sourced from the HOMSys order.
HEADER_CONSTANTS = {
    "STATUS": "1",
    "STATDESC": "Entered",
    "ALLOCATE": True,
    "WITHVAT": True,
    "EXPECTDEL": False,
}


def _load_config_json() -> dict:
    try:
        with open(CONFIG_JSON_PATH, "r", encoding="utf-8-sig") as fh:
            return json.load(fh)
    except (OSError, json.JSONDecodeError):
        return {}


class Config:
    def __init__(self):
        cfg_json = _load_config_json()
        self.api_url = (cfg_json.get("homsys_api_url") or os.environ.get("HOMSYS_BRIDGE_API_URL", "")).rstrip("/")
        self.api_key = cfg_json.get("homsys_api_key") or os.environ.get("HOMSYS_BRIDGE_API_KEY", "")
        # VFP passes its own live SYS(5)+SYS(2003) as argv[2] -- the directory
        # the running BMS session is actually in right now. Preferred over
        # config.json/HOMSYS_DBF_ROOT (which are per-machine static config and
        # can drift from wherever this branch's BMSRAM share is really
        # mapped/mounted). Those remain only for manual/standalone runs
        # outside VFP.
        dbf_root_arg = sys.argv[2] if len(sys.argv) > 2 else ""
        self.dbf_root = dbf_root_arg or cfg_json.get("destination") or os.environ.get("HOMSYS_DBF_ROOT", "")
        self.branch = cfg_json.get("homsys_branch") or os.environ.get("HOMSYS_BRANCH", "")

    def valid(self) -> bool:
        return bool(self.api_url and self.api_key and self.dbf_root and self.branch)

    def path(self, filename: str) -> str:
        return os.path.join(self.dbf_root, filename)


def _headers(cfg: Config) -> dict:
    return {"X-Api-Key": cfg.api_key}


def _pascalize(obj):
    """API JSON comes back camelCase (ASP.NET Core's default System.Text.Json
    policy, unset in Program.cs) but the DTO's real property names -- which
    every key literal in this script matches against -- are PascalCase.
    Recased here, once, right after the HTTP boundary, so the rest of the
    script can keep using the DTO's actual names.
    """
    if isinstance(obj, dict):
        return {k[:1].upper() + k[1:]: _pascalize(v) for k, v in obj.items()}
    if isinstance(obj, list):
        return [_pascalize(v) for v in obj]
    return obj


def fetch_pending(cfg: Config) -> list[dict]:
    resp = requests.get(
        f"{cfg.api_url}/api/salesorders/bridge/pending",
        params={"branch": cfg.branch},
        headers=_headers(cfg), timeout=HTTP_TIMEOUT)
    resp.raise_for_status()
    return _pascalize(resp.json().get("data", []))


def fetch_resync_pending(cfg: Config) -> list[dict]:
    """Orders already pushed to BMS (SoNo assigned) but edited again in
    HOMSys since a deallocation -- SalesOrder.NeedsResync. Same shape as
    fetch_pending's /pending, but these already carry SoNo/DocNo.
    """
    resp = requests.get(
        f"{cfg.api_url}/api/salesorders/bridge/resync-pending",
        params={"branch": cfg.branch},
        headers=_headers(cfg), timeout=HTTP_TIMEOUT)
    resp.raise_for_status()
    return _pascalize(resp.json().get("data", []))


def post_confirm(cfg: Config, so_id: int, so_no: int, doc_no: int) -> None:
    resp = requests.post(
        f"{cfg.api_url}/api/salesorders/bridge/{so_id}/confirm",
        headers=_headers(cfg),
        json={"soNo": so_no, "docNo": doc_no},
        timeout=HTTP_TIMEOUT,
    )
    resp.raise_for_status()


def post_resync_confirm(cfg: Config, so_id: int, ok: bool) -> None:
    resp = requests.post(
        f"{cfg.api_url}/api/salesorders/bridge/{so_id}/resync-confirm",
        headers=_headers(cfg),
        json={"ok": ok},
        timeout=HTTP_TIMEOUT,
    )
    resp.raise_for_status()


def post_lock(cfg: Config, so_id: int) -> None:
    resp = requests.post(
        f"{cfg.api_url}/api/salesorders/bridge/{so_id}/lock",
        headers=_headers(cfg),
        timeout=HTTP_TIMEOUT,
    )
    resp.raise_for_status()


def post_invoice(cfg: Config, so_id: int, inv_no: int, inv_date, inv_amt: float) -> None:
    resp = requests.post(
        f"{cfg.api_url}/api/salesorders/bridge/{so_id}/invoice",
        headers=_headers(cfg),
        json={"invNo": inv_no, "invDate": inv_date.isoformat(), "invAmt": inv_amt},
        timeout=HTTP_TIMEOUT,
    )
    resp.raise_for_status()


def read_live_invoice(cfg: Config, so_no: int):
    """Reads INVNO/INVDATE/INVAMT straight off the live oowkhdr table for one
    SO -- unlike everything else in this script, this touches the live table
    directly (SHARED-safe per dbf.reader's own docstring), because by the
    time printinvoice2 calls drunbridge(soNo) the homsys_queue staging
    folder for this order is long gone (deleted at the original SoNo/DocNo
    confirm). Linear scan (dbf.reader.DbfTable.find has no index support)
    -- fine here, this only runs for one explicit SO on a user click, not
    the bulk path. Returns None if the order isn't found, INVNO is still
    zero/blank, or INVDATE isn't set yet -- all normal, expected states
    before BMS has actually invoiced the order.
    """
    with DbfTable(cfg.path("oowkhdr.dbf")) as t:
        found = t.find("DOCNO", str(so_no))  # legacy: docno = msono, same value reused
        if found is None:
            return None
        _, row = found
        inv_no = get_int(row, "INVNO")
        inv_date = get_date(row, "INVDATE")
        inv_amt = get_decimal(row, "INVAMT")
        if inv_no <= 0 or inv_date is None:
            return None
        return inv_no, inv_date, inv_amt


def post_oos_status(cfg: Config, so_id: int, lines: list[dict]) -> None:
    resp = requests.post(
        f"{cfg.api_url}/api/salesorders/bridge/{so_id}/oos-status",
        headers=_headers(cfg),
        json={"lines": lines},
        timeout=HTTP_TIMEOUT,
    )
    resp.raise_for_status()


def sync_oos(cfg: Config, so_id: int, so_no: int) -> None:
    """Reads whatever's still present in live oowkdet for this SO and posts
    it as the current allocated snapshot. Called from recover_one() (every
    existing drunbridge(soNo) call from invoice.SCX -- Forward-to-Invoice
    move button, and again from printinvoice2) and from lock_order()
    (drunbridge(soNo, "PROCESS"), when BMS processes the order out of the
    queue) -- no new FoxPro call site needed for either. A CProdNo missing
    from this snapshot (already deleted as a full stockout, or never
    existed) is treated as fully OOS on the backend side (full-overwrite
    semantics in SyncOosStatusAsync) -- we only need to post what's here.
    """
    lines = []
    with DbfTable(cfg.path("oowkdet.dbf")) as t:
        for _, row in t.records():
            if str(row.get("DOCNO", "")).strip() != str(so_no).strip():
                continue
            lines.append({
                "cProdNo": str(row.get("CPRODNO", "")).strip(),
                "qtyCs": get_int(row, "QTYCS"),
                "qtyPc": get_int(row, "QTYPC"),
                "stkFlag": get_int(row, "STKFLAG"),
                "netAmt": get_decimal(row, "NETAMT"),
            })

    if not lines:
        log.warning("SO %s: no oowkdet lines found for OOS sync", so_no)
        return

    post_oos_status(cfg, so_id, lines)
    log.info("order %s: synced OOS status for %d line(s) on SO %s", so_id, len(lines), so_no)


def sync_invoice(cfg: Config, so_id: int, so_no: int) -> None:
    invoice = read_live_invoice(cfg, so_no)
    if invoice is None:
        log.info("order %s: SO %s not invoiced yet, nothing to sync", so_id, so_no)
        return
    inv_no, inv_date, inv_amt = invoice
    post_invoice(cfg, so_id, inv_no, inv_date, inv_amt)
    log.info("order %s: synced INVNO %s / INVDATE %s / INVAMT %s for SO %s", so_id, inv_no, inv_date, inv_amt, so_no)


def _read_ledger() -> list[dict]:
    if not os.path.exists(LEDGER_FILE):
        return []
    entries = []
    with open(LEDGER_FILE, "r", encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if line:
                entries.append(json.loads(line))
    return entries


def _append_ledger(entry: dict) -> None:
    with open(LEDGER_FILE, "a", encoding="utf-8") as fh:
        fh.write(json.dumps(entry) + "\n")


def _rewrite_ledger(entries: list[dict]) -> None:
    with open(LEDGER_FILE, "w", encoding="utf-8") as fh:
        for e in entries:
            fh.write(json.dumps(e) + "\n")


def _to_dbf_date(value: str | None):
    """HOMSys DTOs give ISO dates ("2026-08-20"); DBF date fields want YYYYMMDD."""
    if not value:
        return None
    return date.fromisoformat(value[:10])


def queue_dir(cfg: Config, so_no: int) -> str:
    """Per-order scratch folder invoice.SCX's own append method reads --
    this script never writes the live tables itself. Left in place until
    this script's own recovery pass confirms the order and deletes it.
    """
    return cfg.path(os.path.join("homsys_queue", str(so_no)))


def _stage_appended(cfg: Config, so_no: int) -> bool:
    """True once invoice.SCX has appended this order into the live tables --
    it leaves the trace in stg_oowkhdr.dbf's own APPENDED field, which we
    poll directly here instead of scanning the whole (large, shared) live
    oowkhdr table on every run.
    """
    stage_hdr = os.path.join(queue_dir(cfg, so_no), "stg_oowkhdr.dbf")
    if not os.path.isfile(stage_hdr):
        return False
    with DbfTable(stage_hdr) as t:
        row = t.record_at(1)
        return row is not None and get_bool(row, APPENDED_FIELD)


def resync_dir(cfg: Config, so_no: int) -> str:
    """Per-order scratch folder for a post-deallocation edit resync -- kept
    separate from homsys_queue (which is only ever for brand-new orders, so
    SonNo there is a claim in progress, not yet a live oowkhdr row).
    legacy\\vfp\\invoice_appendhomsys.prg's pResyncOrder reads this folder
    and REPLACEs field-by-field into the already-live record, keyed on DOCNO
    -- it never APPEND BLANKs a new header row here.
    """
    return cfg.path(os.path.join("homsys_resync", str(so_no)))


RESYNC_FAILED_MARKER = "RESYNC_FAILED.txt"


def _resync_appended(cfg: Config, so_no: int) -> bool:
    """True once pResyncOrder has REPLACEd the live record -- same APPENDED-
    flag-on-the-staged-header convention as the push flow's _stage_appended."""
    stage_hdr = os.path.join(resync_dir(cfg, so_no), "stg_oowkhdr.dbf")
    if not os.path.isfile(stage_hdr):
        return False
    with DbfTable(stage_hdr) as t:
        row = t.record_at(1)
        return row is not None and get_bool(row, APPENDED_FIELD)


def _resync_failed(cfg: Config, so_no: int) -> bool:
    """True if pResyncOrder could not find the live oowkhdr record by DOCNO
    (SEEK failed) and left the RESYNC_FAILED marker instead of setting
    APPENDED -- see the plan's not-found case."""
    return os.path.isfile(os.path.join(resync_dir(cfg, so_no), RESYNC_FAILED_MARKER))


def stage_resync_order(cfg: Config, order: dict) -> None:
    """Stages a post-deallocation HOMSys edit for pResyncOrder to REPLACE
    into the still-live oowkhdr/oowkdet record. Unlike write_order, this
    reuses the order's own existing SoNo/DocNo (never claim_number -- a
    resync updates a record that's already live, it never mints a new SO#)
    and only stages the ENCODE-OWNED field subset (OOWKHDR_RESYNC_FIELDS),
    which is the actual safety boundary against ever touching a BMS-owned
    column such as STATUS/ALLOCATE.
    """
    doc_no = order["DocNo"]

    header_values = {}
    for src, dbf_name in HEADER_FIELD_MAP.items():
        if dbf_name not in OOWKHDR_RESYNC_FIELDS:
            continue
        if src == "DocNo":
            header_values[dbf_name] = doc_no
        elif src in ("OrderDate", "PoDate"):
            header_values[dbf_name] = _to_dbf_date(order.get(src))
        else:
            header_values[dbf_name] = order.get(src)
    header_values["EXPECTDEL"] = False
    header_values["USERNAME"] = f"{order.get('CreatedBy') or ''} homsys"

    detail_rows = []
    for line in order.get("Lines", []):
        detail_values = {}
        for src, dbf_name in DETAIL_FIELD_MAP.items():
            detail_values[dbf_name] = doc_no if src == "DocNo" else line.get(src)
        if detail_values.get("CSUPPLIER") is not None:
            detail_values["CSUPPLIER"] = str(detail_values["CSUPPLIER"]).strip().rjust(2)
        detail_rows.append(detail_values)

    stage_dir = resync_dir(cfg, order["SoNo"])
    stage_paths = build_resync_stage_tables(cfg, stage_dir)

    with DbfWriter(stage_paths["oowkhdr.dbf"]) as w:
        w.append_record(header_values)

    with DbfWriter(stage_paths["oowkdet.dbf"]) as w:
        for detail_values in detail_rows:
            w.append_record(detail_values)


def recover_resync(cfg: Config) -> None:
    """Stages any pending post-deallocation edit not yet staged, and confirms
    (or reports failed) any staged resync that pResyncOrder has since picked
    up. Unlike the push flow's process_order/recover split, a resync needs no
    ledger -- SoId/SoNo/DocNo are already known from HOMSys (NeedsResync is
    the durable source of truth), so this is safe to just re-derive from
    /resync-pending on every run.
    """
    try:
        pending = fetch_resync_pending(cfg)
    except Exception:
        log.exception("resync: failed to fetch /resync-pending")
        return

    for order in pending:
        so_id, so_no = order["SoId"], order["SoNo"]
        rdir = resync_dir(cfg, so_no)
        try:
            if not os.path.isdir(rdir):
                stage_resync_order(cfg, order)
                log.info("order %s: staged resync for SO %s, awaiting pResyncOrder", so_id, so_no)
            elif _resync_appended(cfg, so_no):
                post_resync_confirm(cfg, so_id, ok=True)
                shutil.rmtree(rdir, ignore_errors=True)
                log.info("order %s: resync confirmed for SO %s", so_id, so_no)
            elif _resync_failed(cfg, so_no):
                post_resync_confirm(cfg, so_id, ok=False)
                shutil.rmtree(rdir, ignore_errors=True)
                log.warning("order %s: resync FAILED for SO %s -- BMS could not find the live record", so_id, so_no)
            else:
                log.info("order %s: resync for SO %s still staged, awaiting pResyncOrder", so_id, so_no)
        except Exception:
            log.exception("order %s: resync handling failed for SO %s, will retry next run", so_id, so_no)


def write_order(cfg: Config, order: dict, so_no: int) -> None:
    doc_no = so_no  # legacy: docno = msono, same value reused as both

    header_values = {}
    for src, dbf_name in HEADER_FIELD_MAP.items():
        if src == "DocNo":
            header_values[dbf_name] = doc_no
        elif src in ("OrderDate", "PoDate"):
            header_values[dbf_name] = _to_dbf_date(order.get(src))
        else:
            header_values[dbf_name] = order.get(src)
    header_values.update(HEADER_CONSTANTS)
    header_values["USERNAME"] = f"{order.get('CreatedBy') or ''} homsys"

    detail_rows = []
    for line in order.get("Lines", []):
        detail_values = {}
        for src, dbf_name in DETAIL_FIELD_MAP.items():
            detail_values[dbf_name] = doc_no if src == "DocNo" else line.get(src)
        # CSUPPLIER is a char mirror of STR(supplier,2) — right-justified
        # (" 9"), not left-justified like a normal text field.
        if detail_values.get("CSUPPLIER") is not None:
            detail_values["CSUPPLIER"] = str(detail_values["CSUPPLIER"]).strip().rjust(2)
        detail_rows.append(detail_values)

    po_num = (order.get("PoNum") or "").strip()
    pofiles_row = None
    if po_num:
        pofiles_row = {
            "PONUM": po_num,
            "PODATE": _to_dbf_date(order.get("PoDate")),
            "SONO": so_no,
            "ORDERDATE": _to_dbf_date(order.get("OrderDate")),
            "CUSTKEY": order.get("CustKey"),
            "CUSNAME": order.get("CusName"),
            "SYSDATE": date.today(),
            "TRANSDATE": date.today(),
        }

    stage_dir = queue_dir(cfg, so_no)
    stage_paths = build_stage_tables(cfg, stage_dir)

    with DbfWriter(stage_paths["oowkhdr.dbf"]) as w:
        w.append_record(header_values)

    with DbfWriter(stage_paths["oowkdet.dbf"]) as w:
        for detail_values in detail_rows:
            w.append_record(detail_values)

    if pofiles_row is not None:
        with DbfWriter(stage_paths["pofiles.dbf"]) as w:
            w.append_record(pofiles_row)


def process_order(cfg: Config, order: dict) -> None:
    so_id = order["SoId"]
    docnum_path = cfg.path("docnum.dbf")

    so_no = claim_number(docnum_path, "SO")
    _append_ledger({"so_id": so_id, "so_no": so_no, "confirmed": False, "claimed_at": datetime.now().isoformat()})
    log.info("order %s: claimed SO number %s", so_id, so_no)

    write_order(cfg, order, so_no)
    log.info("order %s: staged as SO %s, awaiting append in invoice.SCX", so_id, so_no)


def _mark_confirmed(so_id: int, so_no: int) -> None:
    entries = _read_ledger()
    for e in entries:
        if e["so_id"] == so_id and e["so_no"] == so_no:
            e["confirmed"] = True
    _rewrite_ledger(entries)


def recover(cfg: Config) -> set[int]:
    """Resumes any ledger entry left unconfirmed by a prior crashed run.
    Returns the set of so_ids handled here, so main() doesn't reprocess
    them from the fresh /pending fetch.
    """
    handled: set[int] = set()
    entries = _read_ledger()
    pending = [e for e in entries if not e["confirmed"]]
    if not pending:
        return handled

    log.warning("recovering %d unconfirmed ledger entr%s from a prior run", len(pending), "y" if len(pending) == 1 else "ies")

    resp = fetch_pending(cfg)
    by_id = {o["SoId"]: o for o in resp}

    for e in pending:
        so_id, so_no = e["so_id"], e["so_no"]
        try:
            qdir = queue_dir(cfg, so_no)
            if os.path.isdir(qdir) and _stage_appended(cfg, so_no):
                # invoice.SCX has appended this order since the last run -- confirm
                # now, then remove the queue folder ourselves so it's never re-read.
                post_confirm(cfg, so_id, so_no, so_no)
                _mark_confirmed(so_id, so_no)
                shutil.rmtree(qdir, ignore_errors=True)
                log.info("order %s: confirmed as SO %s (appended by invoice.SCX)", so_id, so_no)
            elif os.path.isdir(qdir):
                # Already staged, just not appended yet -- leave it for invoice.SCX,
                # re-check again next run. Not an error.
                log.info("order %s: SO %s still staged, awaiting append in invoice.SCX", so_id, so_no)
            else:
                # Queue folder is missing (crashed before staging completed) -- re-stage.
                order = by_id.get(so_id)
                if order is None:
                    log.error("order %s: no longer in /pending but ledger entry unconfirmed for SO %s — needs manual review", so_id, so_no)
                    handled.add(so_id)
                    continue
                write_order(cfg, order, so_no)
                log.info("order %s: re-staged as SO %s, awaiting append in invoice.SCX", so_id, so_no)
        except Exception:
            log.exception("order %s: recovery failed for SO %s, will retry next run", so_id, so_no)
        handled.add(so_id)

    return handled


def _find_so_id(so_no: int) -> int | None:
    """Looks up the HOMSys SoId for a BMS SO number via the ledger. SO numbers
    can be reused across separate claims of the same number (docnum's counter
    is not guaranteed monotonic across sandbox test runs) -- take the most
    recently claimed entry, not the first match, so a stale earlier claim
    never wins over the order that's actually live in oowkhdr today.
    """
    entries = _read_ledger()
    entry = next((e for e in reversed(entries) if e["so_no"] == so_no), None)
    return entry["so_id"] if entry else None


def post_deallocate(cfg: Config, so_id: int) -> None:
    resp = requests.post(
        f"{cfg.api_url}/api/salesorders/bridge/{so_id}/deallocate",
        headers=_headers(cfg),
        timeout=HTTP_TIMEOUT,
    )
    resp.raise_for_status()


def lock_order(cfg: Config, so_no: int) -> None:
    """Called from invoice.SCX's cnt1.cmdproc.Click via drunbridge(soNo, "PROCESS")
    -- the moment BMS actually processes the order out of the Process Orders
    queue (optn_init1), as opposed to merely appending/confirming its SoNo.
    Locks the order for editing in HOMSys; a1112.scx's deallocate() unlocks it
    again via deallocate_order below. Also syncs the OOS snapshot here -- previously
    this only happened at Forward-to-Invoice (recover_one's sync_oos call),
    so the OOS report had nothing to show between Processed and invoicing.
    """
    so_id = _find_so_id(so_no)
    if so_id is None:
        log.info("SO %s: no ledger entry, nothing to lock", so_no)
        return
    post_lock(cfg, so_id)
    log.info("order %s: locked SO %s (processed in optn_init1)", so_id, so_no)

    try:
        sync_oos(cfg, so_id, so_no)
    except Exception:
        # Same rationale as recover_one: OOS status is a reporting aid, never
        # let it fail the lock that actually matters.
        log.exception("SO %s: OOS sync failed after processing, leaving allocated status stale", so_no)


def deallocate_order(cfg: Config, so_no: int) -> None:
    """Called from a1112.scx's deallocate() via drunbridge(soNo, "DEALLOCATE").
    Posts to the dedicated /deallocate endpoint, which clears SalesOrder.IsLocked
    so the order becomes editable in HOMSys again -- editing was blocked from
    the moment invoice.SCX's drunbridge(soNo) confirmed it into BMS. Despite the
    name, this does NOT drop the SO's OosSyncLine rows: BMS's own deallocate
    already wipes the stockout evidence out of oowkdet, so the last-synced
    OosSyncLine snapshot is kept as the OOS report's only record that a real
    stockout happened at Process time. A later re-Process overwrites it with
    whatever actually happens next time.
    """
    so_id = _find_so_id(so_no)
    if so_id is None:
        log.info("SO %s: no ledger entry, nothing to clear", so_no)
        return
    post_deallocate(cfg, so_id)
    log.info("order %s: cleared OOS status and unlocked SO %s (deallocated)", so_id, so_no)


def recover_one(cfg: Config, so_no: int) -> None:
    """Single-order sync, called from invoice.SCX's drunbridge(soNo) --
    both from the Forward-to-Invoice move button (order not yet confirmed)
    and from printinvoice2 once BMS assigns INVNO (order already confirmed
    long ago, its queue folder long gone). Ledger entries are never deleted,
    only marked confirmed (see _mark_confirmed), so we match on so_no alone
    here -- not on an unconfirmed entry -- and always fall through to
    sync_invoice regardless of confirm state: it's a no-op if INVNO isn't
    set yet, and does the actual work once it is. Also always syncs the
    current oowkdet OOS snapshot (sync_oos); lock_order() syncs it too at
    Processed time, so no FoxPro-side change is needed to pick up allocate()'s
    stock-out deletions between the two points; see sync_oos() for what
    "current" means here.
    """
    so_id = _find_so_id(so_no)
    if so_id is None:
        log.info("SO %s: no ledger entry, nothing to sync", so_no)
        return

    entry = next(e for e in reversed(_read_ledger()) if e["so_no"] == so_no)
    if not entry["confirmed"]:
        qdir = queue_dir(cfg, so_no)
        if os.path.isdir(qdir) and _stage_appended(cfg, so_no):
            post_confirm(cfg, so_id, so_no, so_no)
            _mark_confirmed(so_id, so_no)
            shutil.rmtree(qdir, ignore_errors=True)
            log.info("order %s: confirmed as SO %s (single-SO sync from Forward to Invoice)", so_id, so_no)
        elif os.path.isdir(qdir):
            # pAppendOrderBySo should have run before this call -- if APPENDED is
            # still false here, the append itself failed or didn't happen. Leave
            # it for the next bulk recover() pass rather than guessing why.
            log.warning("SO %s: still staged but not appended -- not confirming yet", so_no)
        else:
            log.warning("SO %s: no queue folder and ledger entry unconfirmed -- needs manual review", so_no)

    sync_invoice(cfg, so_id, so_no)

    try:
        sync_oos(cfg, so_id, so_no)
    except Exception:
        # OOS status is a reporting aid, not part of the invoice-forward
        # transaction itself -- never let it fail the sync that matters.
        log.exception("SO %s: OOS sync failed, leaving allocated status stale", so_no)


def main() -> int:
    cfg = Config()
    if not cfg.valid():
        log.error("homsys_api_url, homsys_api_key, destination and homsys_branch must all be set in %s (or their HOMSYS_BRIDGE_* / HOMSYS_DBF_ROOT / HOMSYS_BRANCH env var fallbacks)", CONFIG_JSON_PATH)
        return 1

    try:
        recover_resync(cfg)
    except Exception:
        # A post-deallocation edit resync piggybacks on this same launch but
        # is never allowed to block the push/confirm/deallocate flow below.
        log.exception("resync pass failed, continuing with the rest of this run")

    so_no_arg = sys.argv[3] if len(sys.argv) > 3 else ""
    action_arg = sys.argv[4] if len(sys.argv) > 4 else ""
    if so_no_arg.strip():
        try:
            action = action_arg.strip().upper()
            if action == "DEALLOCATE":
                deallocate_order(cfg, int(so_no_arg))
            elif action == "PROCESS":
                lock_order(cfg, int(so_no_arg))
            else:
                recover_one(cfg, int(so_no_arg))
        except Exception:
            log.exception("single-SO sync failed for SO %s", so_no_arg)
            return 1
        return 0

    try:
        already_handled = recover(cfg)
    except Exception:
        log.exception("recovery pass failed, aborting this run")
        return 1

    try:
        orders = fetch_pending(cfg)
    except requests.RequestException:
        log.exception("failed to fetch pending orders")
        return 1

    orders = [o for o in orders if o["SoId"] not in already_handled]
    if not orders:
        log.info("no pending orders")
        return 0

    pushed, failed = 0, 0
    for order in orders:
        try:
            process_order(cfg, order)
            pushed += 1
        except Exception:
            log.exception("order %s: failed, skipping (left for next run)", order.get("SoId"))
            failed += 1

    log.info("run complete: %d pushed, %d failed", pushed, failed)
    return 0 if failed == 0 else 1


def _touch_marker(marker_path: str) -> None:
    try:
        with open(marker_path, "w"):
            pass
    except OSError:
        log.exception("could not write marker file %s", marker_path)


if __name__ == "__main__":
    marker_path = sys.argv[1] if len(sys.argv) > 1 else None
    try:
        exit_code = main()
    finally:
        if marker_path:
            _touch_marker(marker_path)
    sys.exit(exit_code)
