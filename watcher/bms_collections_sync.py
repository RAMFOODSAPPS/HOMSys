"""Read-only mirror of BMS's posted collections for HOMSys (HO archives OH03{MMYY} / OD03{MMYY}).

OH03 = one header per posted OR / RPDC (DOCID, DOCNO, DOCDATE, DCR, POSTED, STATDESC "POSTED"/"Cancelled").
OD03 = its double-entry lines: CR 10301 (A/R Trade) per invoice REFNO = amount applied to that invoice,
DR 10507 (Prepaid CEWT, 2307) = withholding, DR cash-in-bank accounts = money received; BADCQF = bounced.
Every receipt balances (DR = CR).

Runs in the normal 5-minute LegacyMasterWatcher pass (config "type": "bms-collections"). File names are
computed from config "since" (default "2023-01", per the user 2026-10-07) up to this month, never listed
(a stray future-dated file such as OH031226, written 2026-02, is ignored until that month). A month is re-read only when either file's
size or mtime moved; only receipts whose content changed are sent (per-receipt hash in
%LOCALAPPDATA%\\HOMSys\\bms-collections-state.json). The first sync of a file sends "reset" so the server
drops whatever it held for it. Never writes to the archives.
"""
import hashlib
import json
import logging
import os
from datetime import date

from dbf.reader import get_bool, get_date, get_decimal, get_int, get_string, scan_fields

log = logging.getLogger("legacy_master_watcher")

STATE_FILE = os.path.join(os.environ.get("LOCALAPPDATA", "."), "HOMSys", "bms-collections-state.json")
BATCH = 400

OH_FIELDS = ["DOCID", "DOCNO", "DOCDATE", "DCRNO", "DCRDATE", "USERNAME", "PAYOR", "TCODE", "POSTED", "STATDESC", "DEPTCODE"]
OD_FIELDS = ["WHSENO", "CKEY", "CUSTKEY", "DOCID", "DOCNO", "REFID", "REFNO", "REFDATE", "DRAMT", "CRAMT", "ACCTNO",
             "DRAWEEBANK", "CHECKNO", "CHECKDATE", "BADCQF", "DEPBANK", "PAYMENTID", "RECBANK", "REFERENCE", "TRTYPE"]


def _iso(d: date | None) -> str | None:
    return d.isoformat() if d else None


def _months(since: str) -> list[str]:
    """['1026', '0926', ...] — MMYY from this month back to `since` ("YYYY-MM"), newest first."""
    sy, sm = (int(x) for x in since.split("-")[:2])
    today = date.today()
    y, m = today.year, today.month
    out = []
    while (y, m) >= (sy, sm):
        out.append(f"{m:02d}{y % 100:02d}")
        m -= 1
        if m == 0:
            y, m = y - 1, 12
    return out


def read_month(oh_path: str, od_path: str) -> dict[str, dict]:
    """{"DOCID-DOCNO": receipt with its lines} for one month."""
    docs: dict[str, dict] = {}
    for r in scan_fields(oh_path, OH_FIELDS):
        key = f"{get_int(r, 'DOCID')}-{get_int(r, 'DOCNO')}"
        docs[key] = {
            "docId": get_int(r, "DOCID"), "docNo": get_int(r, "DOCNO"),
            "docDate": _iso(get_date(r, "DOCDATE")), "dcrNo": get_int(r, "DCRNO"), "dcrDate": _iso(get_date(r, "DCRDATE")),
            "userName": get_string(r, "USERNAME"), "payor": get_string(r, "PAYOR"), "tCode": get_int(r, "TCODE"),
            "posted": _iso(get_date(r, "POSTED")), "statDesc": get_string(r, "STATDESC"), "deptCode": get_int(r, "DEPTCODE"),
            "lines": [],
        }
    for r in scan_fields(od_path, OD_FIELDS):
        doc = docs.get(f"{get_int(r, 'DOCID')}-{get_int(r, 'DOCNO')}")
        if doc is None:
            continue  # a detail row without its header (mid-posting) — picked up next run
        doc["lines"].append({
            "whseNo": get_int(r, "WHSENO"), "custKey": get_string(r, "CUSTKEY"), "cKey": get_string(r, "CKEY"),
            "refId": get_int(r, "REFID"), "refNo": get_int(r, "REFNO"), "refDate": _iso(get_date(r, "REFDATE")),
            "drAmt": round(get_decimal(r, "DRAMT"), 2), "crAmt": round(get_decimal(r, "CRAMT"), 2), "acctNo": get_int(r, "ACCTNO"),
            "draweeBank": get_string(r, "DRAWEEBANK"), "checkNo": get_string(r, "CHECKNO"), "checkDate": _iso(get_date(r, "CHECKDATE")),
            "badCheck": get_bool(r, "BADCQF"), "depBank": get_string(r, "DEPBANK"), "paymentId": get_string(r, "PAYMENTID"),
            "recBank": get_string(r, "RECBANK"), "reference": get_string(r, "REFERENCE"), "trType": get_int(r, "TRTYPE"),
        })
    return docs


def _hash(doc: dict) -> str:
    return hashlib.sha1(json.dumps(doc, sort_keys=True).encode()).hexdigest()


def _load_state() -> dict:
    try:
        with open(STATE_FILE, encoding="utf-8") as fh:
            return json.load(fh)
    except (OSError, ValueError):
        return {}


def _save_state(state: dict) -> None:
    tmp = STATE_FILE + ".tmp"
    with open(tmp, "w", encoding="utf-8") as fh:
        json.dump(state, fh)
    os.replace(tmp, STATE_FILE)


def _find(archives: str, name: str) -> str | None:
    """Case-insensitive on Windows anyway; try the two spellings seen in the folder."""
    for n in (name + ".dbf", name + ".DBF"):
        p = os.path.join(archives, n)
        if os.path.exists(p):
            return p
    return None


def sync_bms_collections(url: str, api_key: str, archives: str, since: str, post, conn_failures: list[str]) -> bool:
    """url = .../api/collections/sync ; posts to {url}/bms-collections. since = first month to mirror, "YYYY-MM"."""
    endpoint = url.rstrip("/") + "/bms-collections"
    state = _load_state()
    ok = True
    for mmyy in _months(since):
        source = f"OH03{mmyy}"
        oh, od = _find(archives, f"OH03{mmyy}"), _find(archives, f"OD03{mmyy}")
        if not oh or not od:
            continue
        stamp = [os.path.getsize(oh), os.path.getmtime(oh), os.path.getsize(od), os.path.getmtime(od)]
        prev = state.get(source)
        if prev and prev.get("stamp") == stamp:
            continue
        try:
            docs = read_month(oh, od)
        except (OSError, EOFError, ValueError) as exc:
            log.error("BMS collections: can't read %s / %s: %s", oh, od, exc)
            ok = False
            continue

        hashes = {k: _hash(d) for k, d in docs.items()}
        old = (prev or {}).get("docs", {})
        changed = [docs[k] for k, h in hashes.items() if old.get(k) != h]
        removed = [k for k in old if k not in hashes]
        reset = prev is None
        if not changed and not removed:
            state[source] = {"stamp": stamp, "docs": hashes}
            continue

        batches = [changed[i:i + BATCH] for i in range(0, len(changed), BATCH)] or [[]]
        for n, batch in enumerate(batches):
            payload = {"sourceFile": source, "reset": reset and n == 0, "docs": batch,
                       "removed": [{"docId": int(k.split("-")[0]), "docNo": int(k.split("-")[1])} for k in removed] if n == 0 else []}
            data = post(endpoint, api_key, payload, conn_failures)
            if data is None:
                log.error("BMS collections: %s stopped at batch %d/%d — retried next run", source, n + 1, len(batches))
                ok = False
                break
        else:
            state[source] = {"stamp": stamp, "docs": hashes}
            _save_state(state)
            log.info("BMS collections: %s synced (%d changed, %d removed%s)", source, len(changed), len(removed), ", reset" if reset else "")
            continue
        break  # stop on failure; later months retry next run
    _save_state(state)
    return ok
