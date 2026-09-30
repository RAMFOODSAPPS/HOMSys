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

Usage: SalesOrderBridge.exe <marker-file-path> [bms-directory] [so-no] [action] [inv-no]
       SalesOrderBridge.exe --drain [bms-directory]   (scheduled 5-min catch-up)
Offline-safe: single-SO actions are written to <bms-directory>\\homsys_outbox\\
first and VFP is released immediately; see outbox.py.
(inv-no only with CANCELINV -- a1174.scx Invoice Cancellation -- or RFCINV. RFCPOST
-- c1110k2.scx toRFC, RSR -> auto-posted RFC -- takes so-no 0 and, in place of
inv-no, "D"+DTOS(posting date) or comma-separated RFC numbers.)
The default and PROCESS single-SO actions also queue the live oowkdet
snapshot for the HOMSys OOS report (see enqueue_actions / read_oos_lines).
The marker file is touched (empty) once: right after a single-SO action's
events are in the outbox, or after staging on the bulk path -- and always
on exit, so VFP's poll loop never waits the full timeout.
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

import hashlib
import json
import logging
import os
import shutil
import sys
import time
from datetime import date, datetime

import requests

import outbox
from dbf.docnum import claim_number
from dbf.reader import DbfTable, get_bool, get_date, get_decimal, get_int, scan_fields
from dbf.stage import (APPENDED_FIELD, OFFSHORE_DUP_FIELD, OOWKHDR_RESYNC_FIELDS, TEXT_TYPES, append_text_record,
                       build_offshore_stage_tables, build_resync_stage_tables, build_stage_tables)
from dbf.writer import DbfWriter

CONFIG_JSON_PATH = r"C:\fox\client\config.json"

LOG_DIR = os.path.join(os.environ.get("LOCALAPPDATA", "."), "HOMSys")
os.makedirs(LOG_DIR, exist_ok=True)
LOG_FILE = os.path.join(LOG_DIR, "salesorder_bridge.log")
LEDGER_FILE = os.path.join(LOG_DIR, "bridge-ledger.jsonl")

HTTP_TIMEOUT = 5  # bounded so this exe can't outlast VFP's ~15s poll window

# This PC's identity for the server-side download claim (POST bridge/{soId}/claim):
# only the claimant may re-claim or see its own claimed orders in /pending.
CLAIMANT = (os.environ.get("COMPUTERNAME") or os.environ.get("HOSTNAME") or "unknown-pc").strip()
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
        params={"branch": cfg.branch, "claimant": CLAIMANT},
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


def post_claim(cfg: Config, so_id: int) -> bool:
    """Reserve the order for this PC BEFORE taking a SO# from docnum.dbf. False
    (409) = another PC holds it or it is already in BMS -- skip it. Any other
    failure raises, so no SO# is taken without a claim."""
    resp = requests.post(
        f"{cfg.api_url}/api/salesorders/bridge/{so_id}/claim",
        headers=_headers(cfg),
        params={"branch": cfg.branch},
        json={"claimedBy": CLAIMANT},
        timeout=HTTP_TIMEOUT,
    )
    if resp.status_code == 409:
        try:
            msg = resp.json().get("message")
        except ValueError:
            msg = resp.text[:200]
        log.warning("order %s: not claimed -- %s", so_id, msg)
        return False
    resp.raise_for_status()
    return True


def post_resync_confirm(cfg: Config, so_id: int, ok: bool) -> None:
    resp = requests.post(
        f"{cfg.api_url}/api/salesorders/bridge/{so_id}/resync-confirm",
        headers=_headers(cfg),
        json={"ok": ok},
        timeout=HTTP_TIMEOUT,
    )
    resp.raise_for_status()


def _post_by_sono(cfg: Config, so_no: int, action: str, body: dict | None = None) -> None:
    """POST /api/salesorders/bridge/by-sono/{so_no}/{action}?branch= -- the
    server resolves the HOMSys order from SO# + branch, so this works from any
    workstation (the old soId routes needed this PC's own ledger). Raises
    HTTPError 404 when it isn't a HOMSys order; outbox.drain drops those."""
    resp = requests.post(
        f"{cfg.api_url}/api/salesorders/bridge/by-sono/{so_no}/{action}",
        headers=_headers(cfg),
        params={"branch": cfg.branch},
        json=body,
        timeout=HTTP_TIMEOUT,
    )
    resp.raise_for_status()


def post_heartbeat(cfg: Config, timeout: float = HTTP_TIMEOUT) -> None:
    resp = requests.post(
        f"{cfg.api_url}/api/salesorders/bridge/heartbeat",
        headers=_headers(cfg),
        params={"branch": cfg.branch},
        timeout=timeout,
    )
    resp.raise_for_status()


INVOICE_FIELDS = ["DOCNO", "STATUS", "INVNO", "INVDATE", "INVAMT"]


def get_string_upper(row: dict, name: str) -> str:
    return str(row.get(name, "")).strip().upper()


def read_header_states(cfg: Config, so_nos: set[int]) -> dict[int, dict]:
    """STATUS/INVNO/INVDATE/INVAMT for each SO# found, from oocuhdr (orders
    already posted by EOD) and then oowkhdr (live) -- the live row wins. A SO#
    in neither is gone from BMS (e.g. deleted by a1174's cancellation). Uses
    scan_fields, so it's a cheap pass even over the ~57 MB oowkhdr.
    """
    states: dict[int, dict] = {}
    for name in ("oocuhdr.dbf", "oowkhdr.dbf"):
        path = cfg.path(name)
        if not os.path.isfile(path):
            continue
        for row in scan_fields(path, INVOICE_FIELDS):
            so_no = get_int(row, "DOCNO")  # legacy: docno = msono, same value reused
            if so_no in so_nos:
                states[so_no] = {
                    "status": get_string_upper(row, "STATUS"),
                    "inv_no": get_int(row, "INVNO"),
                    "inv_date": get_date(row, "INVDATE"),
                    "inv_amt": get_decimal(row, "INVAMT"),
                }
    return states


def read_live_invoice(cfg: Config, so_no: int):
    """INVNO/INVDATE/INVAMT for one SO, read straight off the live tables
    (SHARED-safe). Checks oocuhdr too: an event delivered late (branch was
    offline across an EOD) finds the order already posted out of oowkhdr.
    Returns None if the order isn't found, INVNO is still zero/blank, or
    INVDATE isn't set yet -- all normal before BMS has invoiced the order.
    """
    state = read_header_states(cfg, {so_no}).get(so_no)
    if state is None or state["inv_no"] <= 0 or state["inv_date"] is None:
        return None
    return state["inv_no"], state["inv_date"], state["inv_amt"]


def read_live_delivery(cfg: Config, so_no: int):
    """Reads DELIVERED/STATUS straight off the live VSHDR table for one order
    -- a1146F's delivery-status maintenance screen edits VSHDR directly, so
    this touches the live table directly, same rationale as read_live_invoice.
    Matched on VSHDR.SONO, not DOCNO -- DOCNO on this table is the invoice
    number, a separate sequence from the SO number (confirmed against live
    data: e.g. DOCNO=87182010 / SONO=88219563 on the same row). Returns None
    if no matching INVOICE row is found.
    """
    return read_delivery_headers(cfg, {so_no}).get(so_no)


VSHDR_FIELDS = ["DOCTYPE", "DOCNO", "SONO", "DELIVERED", "STATUS", "VSNO", "VSDATE", "PLATE_NO",
                "TRUCKER", "DRIVER", "VESSEL", "VOYAGE", "BLNO", "EDD", "EDA"]


def read_delivery_headers(cfg: Config, so_nos: set[int]) -> dict[int, dict]:
    """VSHDR INVOICE row per SO# (first match, same as the old single-SO read),
    via scan_fields -- one cheap pass serves one SO or the reconcile sweep's
    whole candidate set."""
    def text(row, field):
        return str(row.get(field, "")).strip() or None

    found: dict[int, dict] = {}
    for row in scan_fields(cfg.path("vshdr.dbf"), VSHDR_FIELDS):
        if get_string_upper(row, "DOCTYPE") != "INVOICE":
            continue
        so_no = get_int(row, "SONO")
        if so_no not in so_nos or so_no in found:
            continue
        found[so_no] = {
            "inv_no": str(row.get("DOCNO", "")).strip(),
            "delivered": get_date(row, "DELIVERED"),
            "status": text(row, "STATUS"),
            "vs_no": get_int(row, "VSNO") or None,
            "vs_date": get_date(row, "VSDATE"),
            "plate_no": text(row, "PLATE_NO"),
            "trucker": text(row, "TRUCKER"),
            "driver": text(row, "DRIVER"),
            "vessel": text(row, "VESSEL"),
            "voyage": text(row, "VOYAGE"),
            "blno": text(row, "BLNO"),
            "edd": get_date(row, "EDD"),
            "eda": get_date(row, "EDA"),
        }
    return found


def read_live_delivery_lines(cfg: Config, inv_no: str) -> list[dict]:
    """Reads every reconciled VSDET row for this invoice -- REC_CS/REC_PC/
    REC_AMT can differ from the line's original QTYCS/QTYPC/NETAMT when
    partially rejected on delivery. VSDET has no SONO column (confirmed
    against a live sample), only DOCTYPE/DOCNO, so this is matched on the
    invoice number read off VSHDR, not the SO number. Linear scan, same
    single-invoice rationale as read_live_delivery.

    A blank REC_STAT is VSDET's default unreconciled state, not "rejected" --
    confirmed against a live sample: blank-REC_STAT rows are ~96% REC_CS=0
    with REC_CS != QTYCS (never touched), while REC_STAT="1" rows are ~92%
    REC_CS == QTYCS (actually confirmed received). Some other BMS process,
    not a1146F, is what sets REC_STAT/REC_CS/REC_PC/REC_AMT -- a1146F never
    writes VSDET. Rows still in the default state are skipped entirely rather
    than forwarded as a real "0 received" -- that would be indistinguishable
    from a genuine full rejection on the HOMSys side.
    """
    lines = []
    for row in scan_fields(cfg.path("vsdet.dbf"), ["DOCTYPE", "DOCNO", "CPRODNO", "REC_CS", "REC_PC", "REC_AMT", "REC_STAT"]):
        if get_string_upper(row, "DOCTYPE") != "INVOICE":
            continue
        if str(row.get("DOCNO", "")).strip() != inv_no:
            continue
        rec_stat = str(row.get("REC_STAT", "")).strip() or None
        if rec_stat is None:
            continue
        lines.append({
            "cProdNo": str(row.get("CPRODNO", "")).strip(),
            "receivedQtyCs": get_int(row, "REC_CS"),
            "receivedQtyPc": get_int(row, "REC_PC"),
            "receivedAmt": get_decimal(row, "REC_AMT"),
            "receivedStatus": rec_stat,
        })
    return lines


def post_delivery_status(cfg: Config, so_no: int, header: dict, lines: list[dict]) -> None:
    inv_no = header["inv_no"]
    resp = requests.post(
        f"{cfg.api_url}/api/salesorders/bridge/by-sono/{so_no}/delivery",
        headers=_headers(cfg),
        params={"branch": cfg.branch},
        json={
            "delivered": header["delivered"].isoformat() if header["delivered"] else None,
            "status": header["status"],
            # Belt-and-suspenders cross-check on the HOMSys side, on top of
            # the SoNo+branch match -- catches a SONO collision/reuse (see
            # _find_so_id's own docstring on docnum.dbf's counter not being
            # guaranteed monotonic) before it overwrites the wrong order.
            "invNo": int(inv_no) if inv_no.isdigit() else None,
            "lines": lines,
            "vsNo": header["vs_no"],
            "vsDate": header["vs_date"].isoformat() if header["vs_date"] else None,
            "plateNo": header["plate_no"],
            "trucker": header["trucker"],
            "driver": header["driver"],
            "vessel": header["vessel"],
            "voyage": header["voyage"],
            "blNo": header["blno"],
            "edd": header["edd"].isoformat() if header["edd"] else None,
            "eda2": header["eda"].isoformat() if header["eda"] else None,
        },
        timeout=HTTP_TIMEOUT,
    )
    resp.raise_for_status()


def sync_delivery_status(cfg: Config, so_no: int) -> None:
    """Single-order sync, called from a1146F's delivery-status maintenance
    screen via run_bridge.bat's "DELIVERED" action once VSHDR.DELIVERED/
    STATUS are saved for that order's invoice. Keyed on SO number just like
    every other single-order bridge call, but looked up server-side by
    SoNo+branch rather than through _find_so_id's local ledger -- a1146F can
    run on this order's original workstation long after that ledger entry
    (if any survives locally) was written, so a DB-side lookup is the more
    durable match. Also pushes every VSDET line for the same invoice, for the
    per-SKU received-vs-shipped view, plus the VS delivery-run details
    (trucker/driver/plate/vessel/etc.) from the same VSHDR row.
    """
    header = read_live_delivery(cfg, so_no)
    if header is None:
        log.warning("SO %s: no VSHDR record found for delivery sync", so_no)
        return
    inv_no = header["inv_no"]
    lines = read_live_delivery_lines(cfg, inv_no) if inv_no else []
    if send_once(cfg, f"delivery:{so_no}", {"header": header, "lines": lines},
                 lambda: post_delivery_status(cfg, so_no, header, lines)):
        log.info("order %s: synced delivery status (delivered=%s, status=%s, %d line(s))",
                  so_no, header["delivered"], header["status"], len(lines))


def read_doccancel(cfg: Config, inv_no: int) -> dict | None:
    """Reads the DOCCANCEL.DBF row a1174.scx's canceld() just appended for this
    invoice -- the only surviving record of the cancellation, since a1174
    deletes the oowkhdr/oowkdet (or oocuhdr/oocudet) rows outright. Takes the
    LAST matching row: the same number can in theory be cancelled, reused and
    cancelled again. Small table (a few hundred rows), linear scan is fine.
    """
    found = None
    with DbfTable(cfg.path("doccancel.dbf")) as t:
        for _, row in t.records():
            if str(row.get("DOCTYPE", "")).strip().upper() != "INVOICE":
                continue
            if get_int(row, "DOCNO") != inv_no:
                continue
            found = row
    if found is None:
        return None
    return {
        "cancel_date": get_date(found, "CANCELDATE"),
        "remarks": str(found.get("REMARKS", "")).strip() or None,
        "cancelled_by": str(found.get("USERNAME", "")).strip() or None,
        # canceld() is passed the invoice's own date and oowkdet NETAMT total
        "inv_date": get_date(found, "DOCDATE"),
        "inv_amt": get_decimal(found, "AMOUNT") or None,
    }


RFC_HDR_FIELDS = ["DOCTYPE", "DOCNO", "DOCDATE", "STATUS", "REFTYPE1", "REFNO1", "REFTYPE3", "REFNO3",
                  "POSTED", "USERNAME", "REMARKS", "REMARKS2"]
RFC_DET_FIELDS = ["DOCTYPE", "DOCNO", "CPRODNO", "QTYCS", "QTYPC", "PIECES", "SPAMT", "AMT", "TAX",
                  "DISCAMT1", "DISCAMT2", "RETCODE", "RSNO", "REMARKS"]


def rfc_tables_present(cfg: Config) -> bool:
    """imtr_hdr + imtr_det both exist in this data folder. Checked before any RFC
    sync: a missing table must not raise FileNotFoundError inside outbox.drain
    (an OSError = "transient, stop and keep" -- it would block every later event
    forever), and must never be read as "no RFCs" (that would post empty sets
    and wipe the RFCs HOMSys already holds)."""
    return os.path.isfile(cfg.path("imtr_hdr.dbf")) and os.path.isfile(cfg.path("imtr_det.dbf"))


def read_rfcs(cfg: Config, inv_nos: set[int] | None = None, posted_since: date | None = None,
              rfc_nos: set[int] | None = None) -> dict[int, list[dict]]:
    """Every POSTED RFC (imtr_hdr DOCTYPE="RFC", STATUS="2") that references an
    invoice (REFTYPE1="INVOICE", REFNO1=inv), with its imtr_det lines, grouped
    as inv_no -> [rfc...] in the shape POST bridge/invoice-rfcs expects.

    Which invoices: `inv_nos` (one invoice, or the reconcile sweep's
    candidates), or every invoice touched by `rfc_nos` (the RFC numbers
    c1110bb's Post button just posted) or by an RFC posted on/after
    `posted_since`. Whatever the selector, each returned invoice always
    carries its COMPLETE set of posted RFCs -- HOMSys replaces the order's RFC
    set wholesale, so a partial set would delete the older RFCs. Both c1110bb RFC
    kinds -- "RFC from invoice" (addrejected, every line) and a manual "Add New
    RFC" -- are stamped identically (REFTYPE2="RSR"), so this reports lines and
    HOMSys decides Full RFC vs Invoiced with RFC by comparing quantities.
    Unposted (STATUS "1") and cancelled-unposted (STATUS "4") RFCs are skipped.
    Both tables are small (~1 MB); scan_fields keeps this well under a second.
    """
    def text(row, field):
        return str(row.get(field, "")).strip() or None

    posted_rows = [
        row for row in scan_fields(cfg.path("imtr_hdr.dbf"), RFC_HDR_FIELDS)
        if get_string_upper(row, "DOCTYPE") == "RFC" and get_string_upper(row, "REFTYPE1") == "INVOICE"
        and str(row.get("STATUS", "")).strip() == "2" and get_int(row, "REFNO1") > 0
    ]
    if inv_nos is None:
        wanted = set()
        for row in posted_rows:
            posted = get_date(row, "POSTED")
            if (rfc_nos and get_int(row, "DOCNO") in rfc_nos) or \
               (posted_since is not None and posted is not None and posted >= posted_since):
                wanted.add(get_int(row, "REFNO1"))
    else:
        wanted = inv_nos

    headers: dict[int, dict] = {}
    for row in posted_rows:
        inv_no = get_int(row, "REFNO1")
        if inv_no not in wanted:
            continue
        posted = get_date(row, "POSTED")
        rfc_no = get_int(row, "DOCNO")
        rfc_date = get_date(row, "DOCDATE") or posted
        headers[rfc_no] = {
            "invNo": inv_no,
            "rfc": {
                "rfcNo": rfc_no,
                # c1110k2 toRFC (updaterfc) links the RFC to its approved RSR:
                # REFTYPE3="CRSR", REFNO3=RSR docno.
                "rsrNo": (get_int(row, "REFNO3") or None) if get_string_upper(row, "REFTYPE3") == "CRSR" else None,
                "rfcDate": rfc_date.isoformat() if rfc_date else None,
                "postedDate": posted.isoformat() if posted else None,
                "userName": text(row, "USERNAME"),
                "remarks": text(row, "REMARKS"),
                "remarks2": text(row, "REMARKS2"),
                "lines": [],
            },
        }
    if not headers:
        return {}

    for row in scan_fields(cfg.path("imtr_det.dbf"), RFC_DET_FIELDS):
        if get_string_upper(row, "DOCTYPE") != "RFC":
            continue
        h = headers.get(get_int(row, "DOCNO"))
        cprodno = str(row.get("CPRODNO", "")).strip()
        if h is None or not cprodno:
            continue  # blank CPRODNO = the placeholder row BMS appends on add/cancel
        h["rfc"]["lines"].append({
            "cProdNo": cprodno,
            "qtyCs": get_int(row, "QTYCS"),
            "qtyPc": get_int(row, "QTYPC"),
            "pieces": get_int(row, "PIECES"),
            "spAmt": get_decimal(row, "SPAMT"),
            "amt": get_decimal(row, "AMT"),
            "tax": get_decimal(row, "TAX"),
            "discAmt1": get_decimal(row, "DISCAMT1"),
            "discAmt2": get_decimal(row, "DISCAMT2"),
            "retCode": text(row, "RETCODE"),
            "rsNo": text(row, "RSNO"),
            "remarks": text(row, "REMARKS"),
        })

    by_inv: dict[int, list[dict]] = {}
    for h in headers.values():
        by_inv.setdefault(h["invNo"], []).append(h["rfc"])
    return by_inv


def post_rfc_sync(cfg: Config, invoices: list[dict]) -> set[int]:
    """POST bridge/invoice-rfcs -- a batch of {soNo, invNo, rfcs}. HOMSys merges
    them in (adds new, never deletes), skips invoices that aren't HOMSys orders,
    and returns the invoice numbers that matched. Never 404s, so one unknown
    invoice can't fail the batch."""
    if not invoices:
        return set()
    resp = requests.post(
        f"{cfg.api_url}/api/salesorders/bridge/invoice-rfcs",
        headers=_headers(cfg),
        params={"branch": cfg.branch},
        json={"invoices": invoices},
        timeout=HTTP_TIMEOUT,
    )
    resp.raise_for_status()
    return {int(n) for n in resp.json().get("matchedInvNos", [])}


# ── Sent-RFC list (processed backlog) ───────────────────────────────────────
# <bms data folder>\homsys_outbox\rfc_sent.json : {inv_no: [rfc_no, ...]} of
# RFCs already uploaded to a HOMSys order, so every RFC is sent ONCE and never
# again. Shared LAN folder (any PC's bridge sees it); only written inside
# outbox.drain's lock. An RFC is recorded only when HOMSys reports its invoice
# matched a HOMSys order -- one that arrives before its order is known is
# retried rather than lost. Missing file -> seeded from what HOMSys already
# holds. Drift (e.g. a HOMSys DB restore) is healed by sync_rfc_gaps, which
# compares against HOMSys's own RFC list on every bridge run.

RFC_LEDGER = "rfc_sent.json"


def _rfc_ledger_path(cfg: Config) -> str:
    return os.path.join(outbox.outbox_dir(cfg.dbf_root), RFC_LEDGER)


def save_rfc_ledger(cfg: Config, ledger: dict[int, set[int]]) -> None:
    path = _rfc_ledger_path(cfg)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8") as fh:
        json.dump({str(inv): sorted(nos) for inv, nos in sorted(ledger.items())}, fh)
    os.replace(tmp, path)


def load_rfc_ledger(cfg: Config) -> dict[int, set[int]]:
    path = _rfc_ledger_path(cfg)
    try:
        with open(path, "r", encoding="utf-8") as fh:
            return {int(inv): set(nos) for inv, nos in json.load(fh).items()}
    except FileNotFoundError:
        pass
    except (OSError, ValueError):
        log.exception("%s unreadable -- reseeding it from HOMSys", RFC_LEDGER)
    # Seed from HOMSys's own RFC list; a network error propagates, so the event
    # that needed it stays queued (transient) instead of re-sending everything.
    ledger = {int(c["InvNo"]): set(c["RfcNos"]) for c in fetch_reconcile_candidates(cfg)
              if c.get("InvNo") and c.get("RfcNos")}
    save_rfc_ledger(cfg, ledger)
    log.info("%s seeded from HOMSys: %d RFC(s) on %d invoice(s) already there",
             RFC_LEDGER, sum(len(v) for v in ledger.values()), len(ledger))
    return ledger


def _post_and_record(cfg: Config, batch: list[dict], ledger: dict[int, set[int]]) -> set[int]:
    matched = post_rfc_sync(cfg, batch)
    for b in batch:
        if b["invNo"] in matched:
            ledger.setdefault(b["invNo"], set()).update(r["rfcNo"] for r in b["rfcs"])
    save_rfc_ledger(cfg, ledger)
    return matched


def upload_new_rfcs(cfg: Config, by_inv: dict[int, list[dict]], so_by_inv: dict[int, int] | None = None) -> tuple[int, int, int]:
    """Uploads only the RFCs not yet in rfc_sent.json; an invoice with nothing
    new isn't sent at all. Returns (invoices sent, RFCs sent, invoices matched)."""
    ledger = load_rfc_ledger(cfg)
    batch = []
    for inv, rfcs in by_inv.items():
        new = [r for r in rfcs if r["rfcNo"] not in ledger.get(inv, set())]
        if new:
            batch.append({"soNo": (so_by_inv or {}).get(inv) or None, "invNo": inv, "rfcs": new})
    if not batch:
        return 0, 0, 0
    matched = _post_and_record(cfg, batch, ledger)
    return len(batch), sum(len(b["rfcs"]) for b in batch), len(matched)


def sync_rfcs_for_invoice(cfg: Config, so_no: int, inv_no: int) -> None:
    """"rfc" event (RFCINV): this invoice's posted RFCs not uploaded before."""
    if not rfc_tables_present(cfg):
        log.warning("INV %s: imtr_hdr/imtr_det not found under %s -- RFC sync skipped", inv_no, cfg.dbf_root)
        return
    sent_inv, sent_rfc, matched = upload_new_rfcs(cfg, read_rfcs(cfg, inv_nos={inv_no}), {inv_no: so_no})
    log.info("INV %s: %d new RFC(s) uploaded (%s)", inv_no, sent_rfc,
             "nothing new" if not sent_inv else ("HOMSys order" if matched else "not a HOMSys order"))


def sync_rfc_scan(cfg: Config, since: date, rfc_nos: list[int] | None = None) -> None:
    """"rfc_scan" event (c1110k2 toRFC's RFCPOST call): invoices with an RFC
    posted on/after `since` (or touched by `rfc_nos`); only their RFCs not
    uploaded before are sent -- already-processed ones stay out of the batch."""
    if not rfc_tables_present(cfg):
        log.warning("RFC scan: imtr_hdr/imtr_det not found under %s -- skipped", cfg.dbf_root)
        return
    by_inv = read_rfcs(cfg, posted_since=since, rfc_nos=set(rfc_nos or []))
    sent_inv, sent_rfc, matched = upload_new_rfcs(cfg, by_inv)
    log.info("RFC scan (RFC# %s / since %s): %d invoice(s) with posted RFCs; %d new RFC(s) sent for %d invoice(s), %d HOMSys order(s)",
             ",".join(map(str, rfc_nos or [])) or "-", since, len(by_inv), sent_rfc, sent_inv, matched)


def cancel_invoice(cfg: Config, so_no: int, inv_no: int, action: str, force: bool = False) -> bool:
    """Sends the a1174 cancellation once per invoice (see send_once)."""
    return send_once(cfg, f"cancel:{inv_no}", {"inv": inv_no, "action": action},
                     lambda: _post_invoice_cancel(cfg, so_no, inv_no, action), force=force)


def _post_invoice_cancel(cfg: Config, so_no: int, inv_no: int, action: str) -> bool:
    """a1174.scx Invoice Cancellation (CANCELINV), via run_bridge.bat with the
    invoice number as the extra 5th arg; details from DOCCANCEL. so_no is 0
    when the form couldn't resolve it; the server then matches on inv_no +
    branch. Most BMS invoices aren't HOMSys orders -- a 404 there is expected,
    logged as info, not an error. (RFCs are NOT cancellations -- see read_rfcs.)
    """
    info = None
    try:
        info = read_doccancel(cfg, inv_no)
    except OSError:
        log.exception("INV %s: could not read cancel details for %s, posting without date/remarks", inv_no, action)
    if info is None:
        info = {"cancel_date": date.today(), "remarks": None, "cancelled_by": None, "inv_date": None, "inv_amt": None}

    resp = requests.post(
        f"{cfg.api_url}/api/salesorders/bridge/invoice-cancel",
        headers=_headers(cfg),
        params={"branch": cfg.branch},
        json={
            "soNo": so_no or None,
            "invNo": inv_no,
            "cancelDate": info["cancel_date"].isoformat() if info["cancel_date"] else None,
            "remarks": info["remarks"],
            "cancelledBy": info["cancelled_by"],
            "invDate": info["inv_date"].isoformat() if info["inv_date"] else None,
            "invAmt": info["inv_amt"],
        },
        timeout=HTTP_TIMEOUT,
    )
    if resp.status_code == 404:
        log.info("SO %s / INV %s: not a HOMSys order, nothing to cancel", so_no, inv_no)
        return False
    resp.raise_for_status()
    log.info("SO %s: synced invoice cancellation of INV %s via %s (%s, %s)",
             so_no, inv_no, action, info["cancel_date"], info["remarks"])
    return True


def read_oos_lines(cfg: Config, so_no: int) -> list[dict]:
    """Whatever's still present in live oowkdet for this SO -- the allocated
    snapshot for the HOMSys OOS report. A CProdNo missing from it (already
    deleted as a full stockout) is treated as fully OOS on the backend
    (full-overwrite semantics in SyncOosStatusAsync). Timing matters: BMS's
    own deallocate later wipes this evidence, which is why the outbox captures
    it at event time rather than re-reading at delivery time.
    """
    return [
        {
            "cProdNo": str(row.get("CPRODNO", "")).strip(),
            "qtyCs": get_int(row, "QTYCS"),
            "qtyPc": get_int(row, "QTYPC"),
            "stkFlag": get_int(row, "STKFLAG"),
            "netAmt": get_decimal(row, "NETAMT"),
        }
        for row in scan_fields(cfg.path("oowkdet.dbf"), ["DOCNO", "CPRODNO", "QTYCS", "QTYPC", "STKFLAG", "NETAMT"])
        if get_int(row, "DOCNO") == so_no
    ]


# ── Sent-event list (processed backlog, every event type) ────────────────────
# <bms data folder>\homsys_outbox\sent_events.json : {key: fingerprint} of the
# last content successfully uploaded per order + event type, e.g.
#   "confirm:87017542", "state:87017542" (lock/deallocate), "invoice:...",
#   "oos:...", "delivery:...", "cancel:<inv>"
# An event whose content matches what was last uploaded is skipped -- already
# processed, not sent again (e.g. Process clicked twice, printinvoice run
# again, a delivery re-saved unchanged). Only recorded on a successful upload
# (a 404 "not a HOMSys order" or any failure is never recorded). Shared LAN
# folder, written inside outbox.drain's lock. The reconcile sweep posts with
# force=True -- it acts on HOMSys's own state, so a wrongly skipped event can
# never leave HOMSys stuck -- and records what it sent. RFCs have their own
# per-RFC list (rfc_sent.json).

EVENT_LEDGER = "sent_events.json"


def _event_ledger_path(cfg: Config) -> str:
    return os.path.join(outbox.outbox_dir(cfg.dbf_root), EVENT_LEDGER)


def _load_event_ledger(cfg: Config) -> dict[str, str]:
    try:
        with open(_event_ledger_path(cfg), "r", encoding="utf-8") as fh:
            return json.load(fh)
    except FileNotFoundError:
        return {}
    except (OSError, ValueError):
        log.exception("%s unreadable -- starting a fresh one", EVENT_LEDGER)
        return {}


def _save_event_ledger(cfg: Config, ledger: dict[str, str]) -> None:
    path = _event_ledger_path(cfg)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8") as fh:
        json.dump(ledger, fh, sort_keys=True)
    os.replace(tmp, path)


def _fingerprint(content) -> str:
    return hashlib.sha1(json.dumps(content, sort_keys=True, default=str).encode("utf-8")).hexdigest()


def send_once(cfg: Config, key: str, content, post, force: bool = False) -> bool:
    """Calls post() unless `content` is exactly what was last uploaded under
    `key`. post() raises on failure (nothing recorded) or returns False for
    "not a HOMSys order" (nothing recorded). Returns True if it posted."""
    fp = _fingerprint(content)
    ledger = _load_event_ledger(cfg)
    if not force and ledger.get(key) == fp:
        log.info("%s: already uploaded with this content -- skipped", key)
        return False
    if post() is False:
        return False
    ledger[key] = fp
    _save_event_ledger(cfg, ledger)
    return True


def _forget_event(cfg: Config, key: str) -> None:
    """Drops one key from sent_events.json so its next upload is never skipped."""
    ledger = _load_event_ledger(cfg)
    if ledger.pop(key, None) is not None:
        _save_event_ledger(cfg, ledger)


def sync_invoice(cfg: Config, so_no: int) -> None:
    invoice = read_live_invoice(cfg, so_no)
    if invoice is None:
        log.info("SO %s: not invoiced yet, nothing to sync", so_no)
        return
    inv_no, inv_date, inv_amt = invoice
    body = {"invNo": inv_no, "invDate": inv_date.isoformat(), "invAmt": inv_amt}
    if send_once(cfg, f"invoice:{so_no}", body, lambda: _post_by_sono(cfg, so_no, "invoice", body)):
        log.info("SO %s: synced INVNO %s / INVDATE %s / INVAMT %s", so_no, inv_no, inv_date, inv_amt)


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


# ── Offshore hand-off (HON / LKA-HO -> HOMSys -> For Branch) ────────────────
# Automated form of UTIL18 TCODE 31 (download7) + UTIL19 uploadoowk. An
# Offshore Encoder's order downloads into the ORIGIN BMS with OFFSHORE = .T.
# (write_order), so it never shows in the origin's Print Picklist; once the
# origin has cleared it (Process + FCCOS + verify: the invoice.optn_init4
# Confirm Clean Orders condition), the origin bridge uploads its full oowkhdr/
# oowkdet/oowkdis rows (outbox "offshore_upload") and stages
#   <root>\homsys_offshore_sent\<so#>\   -> pMarkOffshoreSent sets the origin
#                                          copy to "E" / "To ADC-Picklist"
# HOMSys then hands the order to its ForBranch, whose bridge stages the rows
#   <root>\homsys_offshore_in\<so#>\     -> pAppendOffshore appends them as
#                                          status "5" "PickListed", PICKNO 0
# and reports back (outbox "offshore_received"). The SO# is kept, like the
# old B-file. Both staging steps and both events are local, so they work
# offline; only fetching the awaiting/inbound lists needs HOMSys.

OFFSHORE_SENT_DIR = "homsys_offshore_sent"
OFFSHORE_IN_DIR = "homsys_offshore_in"
OFFSHORE_REPORTED_MARKER = "REPORTED.txt"

# How the order lands in the target BMS (user decision; UTIL19 itself lands
# "4" "For Allocation").
OFFSHORE_ARRIVAL = {"STATUS": "5", "STATDESC": "PickListed", "PICKNO": "0", "OFFSHORE": "F"}


def fetch_offshore_awaiting(cfg: Config) -> list[int]:
    resp = requests.get(
        f"{cfg.api_url}/api/salesorders/bridge/offshore-awaiting",
        params={"branch": cfg.branch},
        headers=_headers(cfg), timeout=HTTP_TIMEOUT)
    resp.raise_for_status()
    return [int(n) for n in resp.json().get("data", [])]


def fetch_offshore_inbound(cfg: Config) -> list[dict]:
    resp = requests.get(
        f"{cfg.api_url}/api/salesorders/bridge/offshore-inbound",
        params={"branch": cfg.branch},
        headers=_headers(cfg), timeout=HTTP_TIMEOUT)
    resp.raise_for_status()
    return _pascalize(resp.json().get("data", []))


def _subdirs(root: str) -> list[str]:
    try:
        return [n for n in os.listdir(root) if os.path.isdir(os.path.join(root, n))]
    except FileNotFoundError:
        return []


def _read_rows_by_docno(path: str, docnos: set[int]) -> dict[int, list[dict]]:
    """Full rows (every text-type column, raw DBF text) for the given DOCNOs --
    one sequential pass. Binary T columns are left out (see dbf.stage)."""
    if not docnos or not os.path.isfile(path):
        return {}
    with DbfTable(path) as t:
        names = [f.name for f in t.fields if f.type in TEXT_TYPES]
    rows: dict[int, list[dict]] = {}
    for row in scan_fields(path, names):
        doc = get_int(row, "DOCNO")
        if doc in docnos:
            rows.setdefault(doc, []).append(row)
    return rows


def ready_for_handoff(row: dict) -> bool:
    """A clean offshore order at the origin: invoice.optn_init4 (Confirm Clean
    Orders) lists status $ "4E" .and. offshore -- 4 = passed Process/verify, E =
    FCCOS approved (dsendfccos always sets E at bcode 28/88). F = already
    confirmed for the old UTIL18 download, still ours to hand off. Plus
    optn_init5's own exclusions (For Allocation / DUS PROCESSED / BEYOND HARD
    LIMIT). VFP's <> under SET EXACT OFF compares only up to the right
    operand's length, hence startswith."""
    return (get_string_upper(row, "STATUS") in ("4", "E", "F")
            and get_bool(row, "OFFSHORE")
            and not str(row.get("STATDESC", "")).startswith("For Allocation")
            and "DUS PROCESSED" not in str(row.get("REMARKS", ""))
            and "BEYOND HARD LIMIT" not in str(row.get("CAUSEFAIL", "")).upper())


def offshore_scan(cfg: Config) -> None:
    """Origin side: queue the upload of every awaiting offshore order that is
    clean at the origin (ready_for_handoff), and stage its "mark sent" folder."""
    awaiting = set(fetch_offshore_awaiting(cfg))
    staged = {int(n) for n in _subdirs(cfg.path(OFFSHORE_SENT_DIR)) if n.isdigit()}
    todo = awaiting - staged
    if not todo:
        return
    headers = _read_rows_by_docno(cfg.path("oowkhdr.dbf"), todo)
    ready = {so for so, rows in headers.items() if ready_for_handoff(rows[0])}
    if not ready:
        return
    lines = _read_rows_by_docno(cfg.path("oowkdet.dbf"), ready)
    discounts = _read_rows_by_docno(cfg.path("oowkdis.dbf"), ready)
    for so_no in sorted(ready):
        det = []
        for r in lines.get(so_no, []):
            r = dict(r)
            # UTIL18 download7: repl all origqtycs with qtycs, origqtypc with qtypc
            if "ORIGQTYCS" in r:
                r["ORIGQTYCS"] = r.get("QTYCS", "")
            if "ORIGQTYPC" in r:
                r["ORIGQTYPC"] = r.get("QTYPC", "")
            det.append(r)
        outbox.enqueue(cfg.dbf_root, "offshore_upload", so_no=so_no,
                       header=headers[so_no][0], lines=det, discounts=discounts.get(so_no, []))
        os.makedirs(os.path.join(cfg.path(OFFSHORE_SENT_DIR), str(so_no)), exist_ok=True)
        log.info("SO %s: clean at origin -- offshore upload queued (%d line(s), %d discount row(s))",
                 so_no, len(det), len(discounts.get(so_no, [])))


def _offshore_stage_result(stage_dir: str) -> str | None:
    """"appended" / "duplicate" once pAppendOffshore has handled the folder, else None."""
    stage_hdr = os.path.join(stage_dir, "stg_oowkhdr.dbf")
    if not os.path.isfile(stage_hdr):
        return None
    with DbfTable(stage_hdr) as t:
        row = t.record_at(1)
    if row is None:
        return None
    if get_bool(row, APPENDED_FIELD):
        return "appended"
    if get_bool(row, OFFSHORE_DUP_FIELD):
        return "duplicate"
    return None


def stage_offshore_order(cfg: Config, order: dict) -> None:
    """Target side: stage one inbound order's origin rows for pAppendOffshore.
    Built in a sibling scratch root and renamed into place, so VFP never sees a
    half-written folder."""
    so_no = int(order["SoNo"])
    tmp_dir = os.path.join(cfg.path(OFFSHORE_IN_DIR + ".tmp"), str(so_no))
    shutil.rmtree(tmp_dir, ignore_errors=True)
    paths = build_offshore_stage_tables(cfg, tmp_dir)
    header = dict(order.get("Header") or {})
    header.update(OFFSHORE_ARRIVAL)
    append_text_record(paths["oowkhdr.dbf"], header)
    for row in order.get("Lines") or []:
        append_text_record(paths["oowkdet.dbf"], row)
    if "oowkdis.dbf" in paths:
        for row in order.get("Discounts") or []:
            append_text_record(paths["oowkdis.dbf"], row)
    final_root = cfg.path(OFFSHORE_IN_DIR)
    os.makedirs(final_root, exist_ok=True)
    os.replace(tmp_dir, os.path.join(final_root, str(so_no)))


def offshore_receive(cfg: Config, online: bool) -> None:
    """Target side: report every folder pAppendOffshore has handled (local,
    works offline), drop reported folders HOMSys no longer lists, and stage
    newly inbound orders. A reported folder is kept until HOMSys stops listing
    the order -- re-staging it before the "received" event lands would only
    come back as a duplicate."""
    root = cfg.path(OFFSHORE_IN_DIR)
    staged: dict[int, bool] = {}  # so# -> reported
    for name in _subdirs(root):
        if not name.isdigit():
            continue
        so_no, d = int(name), os.path.join(root, name)
        reported = os.path.isfile(os.path.join(d, OFFSHORE_REPORTED_MARKER))
        if not reported:
            result = _offshore_stage_result(d)
            if result:
                outbox.enqueue(cfg.dbf_root, "offshore_received", so_no=so_no, result=result)
                with open(os.path.join(d, OFFSHORE_REPORTED_MARKER), "w", encoding="utf-8") as fh:
                    fh.write(f"{datetime.now().isoformat()} {result}\n")
                reported = True
                log.info("SO %s: offshore order %s in this BMS, receipt queued", so_no, result)
        staged[so_no] = reported
    if not online:
        return

    inbound = fetch_offshore_inbound(cfg)
    listed = {int(o["SoNo"]) for o in inbound}
    for so_no, reported in staged.items():
        if reported and so_no not in listed:
            shutil.rmtree(os.path.join(root, str(so_no)), ignore_errors=True)
    for order in inbound:
        so_no = int(order["SoNo"])
        if so_no in staged:
            continue
        try:
            stage_offshore_order(cfg, order)
            log.info("SO %s: offshore order from %s staged, awaiting pAppendOffshore", so_no, order.get("OriginBranch"))
        except Exception:
            log.exception("SO %s: staging the offshore order failed, will retry next run", so_no)


def offshore_sync(cfg: Config, online: bool) -> None:
    """Both halves of the hand-off; each branch only ever has work for one side
    per order. Runs before VFP is released on the bulk path, so the invoice
    form's own append pass picks up what was just staged."""
    if online:
        try:
            offshore_scan(cfg)
        except Exception:
            log.exception("offshore scan failed, will retry next run")
    try:
        offshore_receive(cfg, online)
    except Exception:
        log.exception("offshore receive failed, will retry next run")


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
    # Offshore Encoder order: a11102 forces OFFSHORE for every order encoded at
    # bcode 28/88 (HON/LKA-HO). It skips allocation, keeps the order out of Print
    # Picklist and routes it to Confirm Clean Orders -- see ready_for_handoff.
    header_values["OFFSHORE"] = bool(order.get("ForBranch"))

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


def process_order(cfg: Config, order: dict) -> bool:
    so_id = order["SoId"]
    docnum_path = cfg.path("docnum.dbf")

    if not post_claim(cfg, so_id):
        return False
    so_no = claim_number(docnum_path, "SO")
    _append_ledger({"so_id": so_id, "so_no": so_no, "confirmed": False, "claimed_at": datetime.now().isoformat()})
    log.info("order %s: claimed SO number %s", so_id, so_no)

    write_order(cfg, order, so_no)
    log.info("order %s: staged as SO %s, awaiting append in invoice.SCX", so_id, so_no)
    return True


def _mark_confirmed(so_id: int, so_no: int) -> None:
    entries = _read_ledger()
    for e in entries:
        if e["so_id"] == so_id and e["so_no"] == so_no:
            e["confirmed"] = True
    _rewrite_ledger(entries)


def _queue_confirm(cfg: Config, so_id: int, so_no: int) -> None:
    """invoice.SCX has appended this claimed order: queue its SoNo/DocNo
    confirm as an outbox event (FIFO, offline-safe) instead of POSTing it
    directly, then mark the ledger and drop the queue folder -- the outbox now
    owns delivery. Queued the moment the append is seen, so it always sits
    ahead of every later event for the same SO (lock, invoice, ...), which the
    server can only match by SO# once this confirm has landed."""
    outbox.enqueue(cfg.dbf_root, "confirm", so_no=so_no, so_id=so_id)
    _mark_confirmed(so_id, so_no)
    shutil.rmtree(queue_dir(cfg, so_no), ignore_errors=True)
    log.info("order %s: SO %s appended by invoice.SCX, confirm queued", so_id, so_no)


def recover(cfg: Config, online: bool = True) -> set[int]:
    """Resumes any ledger entry left unconfirmed by a prior crashed run.
    Returns the set of so_ids handled here, so main() doesn't reprocess
    them from the fresh /pending fetch. The appended -> confirm step is local
    (it only queues an outbox event), so it runs offline too; only re-staging
    a missing queue folder needs HOMSys.
    """
    handled: set[int] = set()
    entries = _read_ledger()
    pending = [e for e in entries if not e["confirmed"]]
    if not pending:
        return handled

    log.warning("recovering %d unconfirmed ledger entr%s from a prior run", len(pending), "y" if len(pending) == 1 else "ies")

    by_id: dict | None = None

    for e in pending:
        so_id, so_no = e["so_id"], e["so_no"]
        try:
            qdir = queue_dir(cfg, so_no)
            if os.path.isdir(qdir) and _stage_appended(cfg, so_no):
                _queue_confirm(cfg, so_id, so_no)
            elif os.path.isdir(qdir):
                # Already staged, just not appended yet -- leave it for invoice.SCX,
                # re-check again next run. Not an error.
                log.info("order %s: SO %s still staged, awaiting append in invoice.SCX", so_id, so_no)
            else:
                # Queue folder is missing (crashed before staging completed) -- re-stage.
                if not online:
                    log.info("order %s: SO %s needs re-staging, waiting until HOMSys is reachable", so_id, so_no)
                    handled.add(so_id)
                    continue
                if by_id is None:
                    by_id = {o["SoId"]: o for o in fetch_pending(cfg)}
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
    """Looks up the HOMSys SoId for a BMS SO number via THIS workstation's
    ledger -- only meaningful for the SO#-confirm step, which only the PC that
    claimed the number can do. Every other event is looked up server-side by
    SO# + branch instead (see _post_by_sono). SO numbers can be reused across
    claims (docnum's counter is not guaranteed monotonic across sandbox test
    runs) -- take the most recent entry, not the first match.
    """
    entries = _read_ledger()
    entry = next((e for e in reversed(entries) if e["so_no"] == so_no), None)
    return entry["so_id"] if entry else None


def confirm_if_appended(cfg: Config, so_no: int) -> None:
    """The confirm half of the old recover_one: if this PC claimed this SO# and
    invoice.SCX has since appended it (APPENDED flag on the staged header),
    queue the SoNo/DocNo confirm and remove the queue folder. Must be queued
    BEFORE this run's own events for the same SO (HOMSys can't find the order
    by SO# until it's confirmed) -- run_single calls it first, and the outbox
    delivers FIFO."""
    so_id = _find_so_id(so_no)
    if so_id is None:
        return
    entry = next(e for e in reversed(_read_ledger()) if e["so_no"] == so_no)
    if entry["confirmed"]:
        return
    qdir = queue_dir(cfg, so_no)
    if os.path.isdir(qdir) and _stage_appended(cfg, so_no):
        _queue_confirm(cfg, so_id, so_no)
    elif os.path.isdir(qdir):
        # pAppendOrderBySo should have run before this call -- if APPENDED is
        # still false here, the append itself failed or didn't happen. Leave
        # it for the next bulk recover() pass rather than guessing why.
        log.warning("SO %s: still staged but not appended -- not confirming yet", so_no)
    else:
        log.warning("SO %s: no queue folder and ledger entry unconfirmed -- needs manual review", so_no)


# ── Outbox events ───────────────────────────────────────────────────────────
# Every VFP-triggered single-SO action becomes one outbox event (see outbox.py)
# instead of a direct POST, so a branch with no internet loses nothing:
#   PROCESS    (invoice.SCX cmdproc)          -> lock + oos
#   DEALLOCATE (a1112.scx deallocate)         -> deallocate
#   (default)  (invoice.SCX Forward/printinv) -> invoice + oos
#   DELIVERED  (a1146F save)                  -> delivery
#   CANCELINV  (a1174.scx)                    -> cancel
#   RFCPOST    (c1110k2 toRFC: RSR -> auto-posted RFC)
#                                             -> rfc_scan (invoices with an RFC posted on/after
#                                                the "Dyyyymmdd" date passed = sysparam.transdate)
#   RFCINV     (single invoice, kept for manual/diagnostic use) -> rfc
# plus "confirm" (SoNo/DocNo of a newly appended order, see _queue_confirm),
# "offshore_upload" / "offshore_received" (offshore hand-off, see offshore_scan /
# offshore_receive -- queued by the bridge itself, not by a VFP action).
# Delivery is strictly FIFO (outbox.drain), so HOMSys sees events in BMS order.
# lock/deallocate carry no data; invoice/delivery/cancel re-read BMS at
# delivery time (state, not a stale copy); oos carries the oowkdet snapshot
# taken at event time, because BMS's deallocate later wipes that evidence.

RFC_SCAN_DAYS = 7  # c1110bb Post can post a backlog of RFCs dated days back


def enqueue_actions(cfg: Config, so_no: int, action: str, inv_no: int | None,
                    rfc_nos: list[int] | None = None, rfc_since: date | None = None) -> str | None:
    """Queues the events for one VFP action. Returns the OOS event's path when
    one was queued (its snapshot is filled in after VFP is released)."""
    oos_path = None
    if action == "DEALLOCATE":
        outbox.enqueue(cfg.dbf_root, "deallocate", so_no=so_no)
    elif action == "PROCESS":
        outbox.enqueue(cfg.dbf_root, "lock", so_no=so_no)
        oos_path = outbox.enqueue(cfg.dbf_root, "oos", so_no=so_no)
    elif action == "DELIVERED":
        outbox.enqueue(cfg.dbf_root, "delivery", so_no=so_no)
    elif action == "CANCELINV":
        outbox.enqueue(cfg.dbf_root, "cancel", so_no=so_no, inv_no=inv_no, action=action)
    elif action == "RFCINV":
        outbox.enqueue(cfg.dbf_root, "rfc", so_no=so_no, inv_no=inv_no)
    elif action == "RFCPOST":
        since = rfc_since or date.fromordinal(date.today().toordinal() - RFC_SCAN_DAYS)
        outbox.enqueue(cfg.dbf_root, "rfc_scan", so_no=0, since=since.isoformat(), rfc_nos=rfc_nos or [])
    else:
        outbox.enqueue(cfg.dbf_root, "invoice", so_no=so_no)
        oos_path = outbox.enqueue(cfg.dbf_root, "oos", so_no=so_no)
    return oos_path


def deliver_event(cfg: Config, event: dict) -> None:
    """outbox.drain callback -- raises on failure (see outbox.drain for how
    each failure is treated)."""
    kind, so_no = event["kind"], int(event["so_no"])
    if kind == "confirm":
        send_once(cfg, f"confirm:{so_no}", {"so_id": event["so_id"]},
                  lambda: post_confirm(cfg, int(event["so_id"]), so_no, so_no), force=bool(event.get("force")))
    elif kind in ("lock", "deallocate"):
        # one "state" key per SO: lock->lock is skipped, lock->deallocate->lock isn't
        send_once(cfg, f"state:{so_no}", kind, lambda: _post_by_sono(cfg, so_no, kind))
        if kind == "deallocate":
            # HOMSys clears the order's OOS on deallocate, so the next Process's
            # snapshot must be sent even if it's identical to the last one.
            _forget_event(cfg, f"oos:{so_no}")
    elif kind == "invoice":
        sync_invoice(cfg, so_no)
    elif kind == "oos":
        # "lines" missing = the exe that queued it died before capturing the
        # snapshot; best effort is whatever oowkdet holds now.
        lines = event["lines"] if "lines" in event else read_oos_lines(cfg, so_no)
        if lines:
            send_once(cfg, f"oos:{so_no}", lines, lambda: _post_by_sono(cfg, so_no, "oos-status", {"lines": lines}))
        else:
            log.warning("SO %s: no oowkdet lines found for OOS sync", so_no)
    elif kind == "delivery":
        sync_delivery_status(cfg, so_no)
    elif kind == "cancel" and event.get("action") == "RFCINV":
        # queued by an older bridge that treated an RFC as a cancel
        sync_rfcs_for_invoice(cfg, so_no, int(event["inv_no"]))
    elif kind == "cancel":
        cancel_invoice(cfg, so_no, int(event["inv_no"]), event["action"])
    elif kind == "rfc":
        sync_rfcs_for_invoice(cfg, so_no, int(event["inv_no"]))
    elif kind == "rfc_scan":
        sync_rfc_scan(cfg, date.fromisoformat(event["since"]), event.get("rfc_nos"))
    elif kind == "offshore_upload":
        # 409 = the For Branch already has this SO# -> parked in failed\ and shown on the order
        body = {"header": event["header"], "lines": event["lines"], "discounts": event["discounts"]}
        send_once(cfg, f"offshore:{so_no}", body, lambda: _post_by_sono(cfg, so_no, "offshore-upload", body))
    elif kind == "offshore_received":
        body = {"result": event["result"]}
        send_once(cfg, f"offshore-received:{so_no}", body,
                  lambda: _post_by_sono(cfg, so_no, "offshore-received", body))
    else:
        raise ValueError(f"unknown outbox event kind {kind!r}")


# ── Reconciliation sweep ────────────────────────────────────────────────────
# Safety net behind the outbox: re-derive HOMSys's view of this branch's live
# orders from BMS truth and re-post any gap. Covers events that were never
# captured at all (old form still on some workstation, an event that 404'd
# because its SO# confirm hadn't landed yet, an outbox file lost). All the
# endpoints it hits are idempotent. Throttled -- it scans oowkhdr/vshdr.

RECONCILE_EVERY_SECONDS = 30 * 60
ENTERED_STATUSES = {"", "1", "B"}  # oowkhdr STATUS still "Entered"-like (invoice.SCX Activate's harvy1)


def _reconcile_due(cfg: Config) -> bool:
    """True at most once per RECONCILE_EVERY_SECONDS per branch folder -- the
    marker is touched up front, so concurrent bridge runs don't both sweep."""
    marker = os.path.join(outbox.outbox_dir(cfg.dbf_root), ".last_reconcile")
    try:
        if time.time() - os.path.getmtime(marker) < RECONCILE_EVERY_SECONDS:
            return False
    except OSError:
        pass
    os.makedirs(os.path.dirname(marker), exist_ok=True)
    with open(marker, "w"):
        pass
    return True


def fetch_reconcile_candidates(cfg: Config) -> list[dict]:
    resp = requests.get(
        f"{cfg.api_url}/api/salesorders/bridge/reconcile-candidates",
        params={"branch": cfg.branch},
        headers=_headers(cfg), timeout=HTTP_TIMEOUT)
    resp.raise_for_status()
    return _pascalize(resp.json().get("data", []))


def read_cancelled_invoices(cfg: Config, inv_nos: set[int]) -> dict[int, str]:
    """inv_no -> "CANCELINV" for each of `inv_nos` with a DOCCANCEL row (a1174).
    RFCs are deliberately NOT treated as cancels -- both RFC kinds carry
    REFTYPE2="RSR", so a partial manual RFC used to be mistaken for a full
    cancel here. RFCs are reconciled separately (see reconcile)."""
    found: dict[int, str] = {}
    path = cfg.path("doccancel.dbf")
    if os.path.isfile(path):
        for row in scan_fields(path, ["DOCTYPE", "DOCNO"]):
            if get_string_upper(row, "DOCTYPE") == "INVOICE" and get_int(row, "DOCNO") in inv_nos:
                found[get_int(row, "DOCNO")] = "CANCELINV"
    return found


def reconcile(cfg: Config) -> None:
    candidates = {c["SoNo"]: c for c in fetch_reconcile_candidates(cfg)}
    if not candidates:
        return
    states = read_header_states(cfg, set(candidates))
    inv_nos = {c["InvNo"] for c in candidates.values() if c["InvNo"]}
    inv_nos |= {s["inv_no"] for s in states.values() if s["inv_no"] > 0}
    cancelled = read_cancelled_invoices(cfg, inv_nos)
    deliveries = read_delivery_headers(cfg, set(candidates))
    rfc_ok = bool(inv_nos) and rfc_tables_present(cfg)
    rfcs_by_inv = read_rfcs(cfg, inv_nos=inv_nos) if rfc_ok else {}
    rfc_batch = []

    fixed = 0
    for so_no, c in candidates.items():
        try:
            state = states.get(so_no)
            live_inv = state["inv_no"] if state and state["inv_no"] > 0 else None
            inv = c["InvNo"] or live_inv
            if inv and inv != c["CancelledInvNo"] and inv in cancelled:
                cancel_invoice(cfg, so_no, inv, cancelled[inv], force=True)
                fixed += 1
                continue

            if state is not None:
                if live_inv and state["inv_date"] and live_inv not in (c["InvNo"], c["CancelledInvNo"]):
                    body = {"invNo": live_inv, "invDate": state["inv_date"].isoformat(), "invAmt": state["inv_amt"]}
                    send_once(cfg, f"invoice:{so_no}", body, lambda: _post_by_sono(cfg, so_no, "invoice", body), force=True)
                    fixed += 1
                    log.info("reconcile: SO %s invoice %s re-synced", so_no, live_inv)
                elif (state["status"] not in ENTERED_STATUSES and not c["IsLocked"]
                      and c["WorkflowStatus"] in ("Downloaded", "Deallocated")):
                    send_once(cfg, f"state:{so_no}", "lock", lambda: _post_by_sono(cfg, so_no, "lock"), force=True)
                    fixed += 1
                    log.info("reconcile: SO %s is STATUS %s in BMS, lock re-synced", so_no, state["status"])
                elif state["status"] == "1" and c["WorkflowStatus"] == "Processed" and not c["NeedsResync"]:
                    send_once(cfg, f"state:{so_no}", "deallocate", lambda: _post_by_sono(cfg, so_no, "deallocate"), force=True)
                    fixed += 1
                    log.info("reconcile: SO %s is back to Entered in BMS, deallocate re-synced", so_no)

            rfcs = rfcs_by_inv.get(inv, []) if inv else []
            missing = [r for r in rfcs if r["rfcNo"] not in set(c.get("RfcNos") or [])]
            if rfc_ok and inv and missing:
                rfc_batch.append({"soNo": so_no, "invNo": inv, "rfcs": missing})

            dlv = deliveries.get(so_no)
            if dlv is not None:
                delivered = dlv["delivered"].isoformat() if dlv["delivered"] else None
                if delivered != c["Delivered"] or dlv["status"] != c["DeliveryStatus"]:
                    lines = read_live_delivery_lines(cfg, dlv["inv_no"]) if dlv["inv_no"] else []
                    send_once(cfg, f"delivery:{so_no}", {"header": dlv, "lines": lines},
                              lambda: post_delivery_status(cfg, so_no, dlv, lines), force=True)
                    fixed += 1
                    log.info("reconcile: SO %s delivery re-synced (%s / %s)", so_no, delivered, dlv["status"])
        except (requests.ConnectionError, requests.Timeout):
            log.warning("reconcile: HOMSys unreachable, stopping sweep")
            return
        except requests.HTTPError as ex:
            code = ex.response.status_code if ex.response is not None else None
            log.warning("reconcile: SO %s rejected (HTTP %s), skipping", so_no, code)
    if rfc_batch:
        try:
            _post_and_record(cfg, rfc_batch, load_rfc_ledger(cfg))
            fixed += len(rfc_batch)
            log.info("reconcile: RFCs re-synced for %s", ", ".join(f"SO {b['soNo']}/INV {b['invNo']}" for b in rfc_batch))
        except requests.RequestException:
            log.warning("reconcile: RFC re-sync failed, will retry next sweep")
    log.info("reconcile: %d candidate(s) checked, %d fixed", len(candidates), fixed)


# ── Run modes ───────────────────────────────────────────────────────────────

MARKER_PATH: str | None = None
_marker_released = False


def release_vfp() -> None:
    """Touches the marker VFP polls for -- once. Single-SO actions call this as
    soon as their events are safely in the outbox, so the operator never waits
    on HOMSys (the old flow could freeze the screen 10-15 s when offline)."""
    global _marker_released
    if MARKER_PATH and not _marker_released:
        _touch_marker(MARKER_PATH)
        _marker_released = True


def homsys_reachable(cfg: Config) -> bool:
    """Doubles as the heartbeat: short timeout so an offline branch is detected
    in ~2 s instead of stacking 5 s timeouts on every call."""
    try:
        post_heartbeat(cfg, timeout=2)
        return True
    except requests.RequestException as ex:
        log.warning("HOMSys unreachable (%s) -- %d outbox event(s) pending", type(ex).__name__,
                    len(outbox.pending(cfg.dbf_root)))
        return False


def background_sync(cfg: Config, online: bool | None = None) -> None:
    """Everything that talks to HOMSys but that VFP needn't wait for: resync
    staging, outbox drain, throttled reconcile. Skipped when offline -- the
    outbox keeps everything for the next run."""
    if online is None:
        online = homsys_reachable(cfg)
    if not online:
        return
    try:
        recover_resync(cfg)
    except Exception:
        log.exception("resync pass failed, continuing")
    outbox.drain(cfg.dbf_root, lambda e: deliver_event(cfg, e), on_empty=lambda: _maybe_reconcile(cfg))


def _maybe_reconcile(cfg: Config) -> None:
    """Runs inside outbox.drain's lock, only after the queue emptied -- so the
    sweep's state-based re-posts never overtake an older queued event (FIFO).
    The RFC gap check runs on EVERY bridge run (cheap: the two small RFC
    tables + one candidates GET); the full sweep, which scans the big
    oowkhdr/vshdr tables, stays throttled to RECONCILE_EVERY_SECONDS."""
    try:
        sync_rfc_gaps(cfg)
    except Exception:
        log.exception("RFC gap check failed, will retry on the next bridge run")
    try:
        if _reconcile_due(cfg):
            reconcile(cfg)
    except Exception:
        log.exception("reconcile sweep failed, will retry next time it's due")


def sync_rfc_gaps(cfg: Config) -> None:
    """Every HOMSys order (reconcile candidate) whose invoice has posted RFCs in
    BMS that HOMSys doesn't hold yet -> just those RFCs, one batch post. Uses
    HOMSys's own RFC list (not rfc_sent.json), so it also heals any drift.
    This is what lets a plain Invoice Processing open/refresh pick up an RFC
    that was never queued (e.g. toRFC run from a form without the hook), instead
    of waiting for the 30-minute full sweep. Matches on HOMSys's own InvNo; the
    full sweep additionally covers orders whose invoice sync never landed."""
    if not rfc_tables_present(cfg):
        return
    candidates = [c for c in fetch_reconcile_candidates(cfg) if c.get("InvNo")]
    if not candidates:
        return
    rfcs_by_inv = read_rfcs(cfg, inv_nos={c["InvNo"] for c in candidates})
    batch = []
    for c in candidates:
        known = set(c.get("RfcNos") or [])
        missing = [r for r in rfcs_by_inv.get(c["InvNo"], []) if r["rfcNo"] not in known]
        if missing:
            batch.append({"soNo": c["SoNo"], "invNo": c["InvNo"], "rfcs": missing})
    if batch:
        _post_and_record(cfg, batch, load_rfc_ledger(cfg))
        log.info("RFC gap check: uploaded %s", ", ".join(
            f"SO {b['soNo']}/INV {b['invNo']} (RFC {','.join(str(r['rfcNo']) for r in b['rfcs'])})" for b in batch))


def run_single(cfg: Config, so_no: int, action: str, inv_no: int | None, rfc_nos: list[int] | None = None,
               rfc_since: date | None = None) -> int:
    if action not in ("DEALLOCATE", "PROCESS", "DELIVERED", "CANCELINV", "RFCINV", "RFCPOST"):
        # Forward-to-Invoice: pAppendOrderBySo may have just appended this SO.
        # Its confirm must be queued ahead of the invoice/oos events below (FIFO).
        try:
            confirm_if_appended(cfg, so_no)
        except Exception:
            log.exception("SO %s: confirm check failed, bulk recover() will retry", so_no)
    oos_path = None
    try:
        oos_path = enqueue_actions(cfg, so_no, action, inv_no, rfc_nos, rfc_since)
    except OSError:
        # Outbox folder not writable -- fall back to the old direct path for
        # this one action rather than losing it silently.
        log.exception("SO %s: could not write outbox, delivering %s directly", so_no, action or "sync")
        release_vfp()
        for kind in {"DEALLOCATE": ["deallocate"], "PROCESS": ["lock"], "DELIVERED": ["delivery"],
                     "CANCELINV": ["cancel"], "RFCINV": ["rfc"], "RFCPOST": ["rfc_scan"]}.get(action, ["invoice"]):
            since = (rfc_since or date.fromordinal(date.today().toordinal() - RFC_SCAN_DAYS)).isoformat()
            deliver_event(cfg, {"kind": kind, "so_no": so_no, "inv_no": inv_no, "action": action, "since": since,
                                "rfc_nos": rfc_nos or []})
        return 0
    release_vfp()

    if oos_path:
        try:
            outbox.update(oos_path, lines=read_oos_lines(cfg, so_no))
        except Exception:
            log.exception("SO %s: OOS snapshot capture failed, delivery will re-read oowkdet", so_no)

    background_sync(cfg)
    return 0


def run_bulk(cfg: Config) -> int:
    """invoice.SCX Form1.Init / Process Orders: download new HOMSys orders.
    VFP appends what this stages right after the marker, so the marker waits
    for staging here -- but not for the outbox/reconcile work after it."""
    online = homsys_reachable(cfg)
    if online:
        try:
            recover_resync(cfg)
        except Exception:
            log.exception("resync pass failed, continuing with the rest of this run")
    rc = _bulk_download(cfg, online)
    offshore_sync(cfg, online)
    release_vfp()
    if online:
        background_sync(cfg, online=True)
    return rc


def _bulk_download(cfg: Config, online: bool) -> int:
    try:
        already_handled = recover(cfg, online)
    except Exception:
        log.exception("recovery pass failed, aborting this run")
        return 1
    if not online:
        return 0  # appended orders' confirms are queued; downloads wait for HOMSys

    try:
        orders = fetch_pending(cfg)
    except requests.RequestException:
        log.exception("failed to fetch pending orders")
        return 1

    orders = [o for o in orders if o["SoId"] not in already_handled]
    orders = _unclaimed(cfg, orders)
    if not orders:
        log.info("no pending orders")
        return 0

    pushed, failed = 0, 0
    for order in orders:
        try:
            if process_order(cfg, order):
                pushed += 1
        except Exception:
            log.exception("order %s: failed, skipping (left for next run)", order.get("SoId"))
            failed += 1

    log.info("run complete: %d pushed, %d failed", pushed, failed)
    return 0 if failed == 0 else 1


def _queued_confirm_so_ids(cfg: Config) -> set[int]:
    """SoIds with a SO# confirm still waiting in the shared outbox (any PC)."""
    ids: set[int] = set()
    for path in outbox.pending(cfg.dbf_root):
        try:
            with open(path, "r", encoding="utf-8") as fh:
                event = json.load(fh)
        except (OSError, ValueError):
            continue
        if event.get("kind") == "confirm" and event.get("so_id") is not None:
            ids.add(int(event["so_id"]))
    return ids


def _unclaimed(cfg: Config, orders: list[dict]) -> list[dict]:
    """/pending lists an order until HOMSys receives its SO# confirm -- and this
    runs BEFORE the outbox drain, so an order whose confirm is still queued
    (offline, or HOMSys erroring) is listed again. Claiming it again appends a
    second BMS order for the same HOMSys order (2026-09-29: SoId 2043 became
    both 88265761 and 88265762). Skip every order that already has a SO#:
    queued in the shared outbox by any PC, or claimed + appended on this PC
    (then its confirm was lost -- re-queue it, forced past sent_events.json)."""
    queued = _queued_confirm_so_ids(cfg)
    claimed = {e["so_id"]: e["so_no"] for e in _read_ledger() if e.get("confirmed")}  # latest claim wins
    fresh = []
    for order in orders:
        so_id = order["SoId"]
        if so_id in queued:
            log.info("order %s: SO# confirm still queued in the outbox -- not claiming a new SO#", so_id)
        elif so_id in claimed:
            log.warning("order %s: already SO %s in BMS (this PC's ledger) but HOMSys has no SO# -- re-queuing its confirm",
                        so_id, claimed[so_id])
            outbox.enqueue(cfg.dbf_root, "confirm", so_no=claimed[so_id], so_id=so_id, force=True)
        else:
            fresh.append(order)
    return fresh


def run_drain(cfg: Config) -> int:
    """`SalesOrderBridge.exe --drain [bms-directory]` -- the scheduled 5-minute
    catch-up (register-bridge-drain-task.ps1), so a branch's backlog flushes
    soon after the connection returns even if nobody clicks anything. Also
    confirms any SO# this PC claimed that invoice.SCX has since appended."""
    online = homsys_reachable(cfg)
    try:
        recover(cfg, online)
    except Exception:
        log.exception("drain: ledger recovery failed, continuing")
    offshore_sync(cfg, online)
    if online:
        background_sync(cfg, online=True)
    return 0


def main() -> int:
    cfg = Config()
    if not cfg.valid():
        log.error("homsys_api_url, homsys_api_key, destination and homsys_branch must all be set in %s (or their HOMSYS_BRIDGE_* / HOMSYS_DBF_ROOT / HOMSYS_BRANCH env var fallbacks)", CONFIG_JSON_PATH)
        return 1

    if len(sys.argv) > 1 and sys.argv[1] == "--drain":
        return run_drain(cfg)

    so_no_arg = sys.argv[3] if len(sys.argv) > 3 else ""
    if so_no_arg.strip():
        action = (sys.argv[4] if len(sys.argv) > 4 else "").strip().upper()
        extra_arg = (sys.argv[5] if len(sys.argv) > 5 else "").strip()
        # 5th arg: the invoice no. (CANCELINV/RFCINV), or for RFCPOST the
        # comma-separated RFC numbers c1110bb's Post button just posted.
        inv_no = int(extra_arg) if extra_arg.isdigit() else None
        rfc_nos, rfc_since = None, None
        if action == "RFCPOST":
            if len(extra_arg) == 9 and extra_arg[:1].upper() == "D" and extra_arg[1:].isdigit():
                # c1110k2 toRFC passes "D" + DTOS(sysparam.transdate) -- exactly
                # what updaterfc stamps as POSTED -- short, whatever the batch size.
                rfc_since = date(int(extra_arg[1:5]), int(extra_arg[5:7]), int(extra_arg[7:9]))
            else:
                rfc_nos = [int(t) for t in extra_arg.replace(" ", "").split(",") if t.isdigit()]
        try:
            return run_single(cfg, int(so_no_arg), action, inv_no, rfc_nos, rfc_since)
        except Exception:
            log.exception("single-SO sync failed for SO %s", so_no_arg)
            return 1

    return run_bulk(cfg)


def _touch_marker(marker_path: str) -> None:
    try:
        with open(marker_path, "w"):
            pass
    except OSError:
        log.exception("could not write marker file %s", marker_path)


if __name__ == "__main__":
    MARKER_PATH = sys.argv[1] if len(sys.argv) > 1 and sys.argv[1] != "--drain" else None
    exit_code = 1
    try:
        exit_code = main()
    finally:
        release_vfp()
    sys.exit(exit_code)
