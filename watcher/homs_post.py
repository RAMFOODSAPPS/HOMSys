"""Writes verified HOMSys collections into the HOMS ARCHIVES month files (HO only, LegacyMasterWatcher --requests).

HOMSys queues one posting per HOMS document when the Finance Verifier acts and builds every field value itself:
  OR   -> OH03{MMYY} (header) + OD03{MMYY} (lines)   CR: DR cash in bank / CR 10301 / DR 10507 CEWT
  RPDC -> GH03{MMYY} + GD03{MMYY}                     PR/AR: DR 10552 / CR 10301
  JV   -> GH03{MMYY} + GD03{MMYY}                     matured PDC: DR cash in bank + 10507 / CR 10552,
          numbered from the JV counter in F:\\HOMS\\RAM\\DOCNUM.DBF (claimed once, reported to HOMSys, reused on retry)
Each document then also goes into HOMS's live tables -- OR into or_hdr/or_det, RPDC/JV into gatr_hdr/gatr_det
(homs_live.py -> HOMSPOST.EXE, which keeps their CDX tags) -- and is Done only when both places have it.
The ARCHIVES month files have no CDX/FPT, so rows are appended directly under the same locks VFP takes for APPEND and
FLOCK() (dbf.locking.lock_append, measured against VFP6/VFP9), lines first, header last. A document already in the file (same DOCID + DOCNO) is never
written twice; lines left without a header by an interrupted run are marked deleted and written again.
Never creates a month file: until HOMS has made it, the posting waits ("retry later").
"""
import logging
import os
from datetime import date

import requests

from dbf.docnum import claim_number
from dbf.locking import LockTimeout, lock_append
from dbf.writer import DbfWriter
from homs_live import write_live

log = logging.getLogger("legacy_master_watcher")

LOCK_TIMEOUT_S = 30


def _path(folder: str, name: str) -> str | None:
    for ext in (".dbf", ".DBF"):
        p = os.path.join(folder, name + ext)
        if os.path.isfile(p):
            return p
    return None


def _values(writer: DbfWriter, row: dict) -> dict:
    """Server JSON -> python values typed for this file's own fields. Unset logicals stay blank (as in HOMS)."""
    out = {}
    for f in writer.header.fields:
        v = row.get(f.name)
        if f.type == "D" and isinstance(v, str) and v:
            v = date.fromisoformat(v)
        elif f.type == "L" and f.name not in row:
            v = ""
        out[f.name] = v
    return out


def _doc_records(writer: DbfWriter, docid: int, docno: int) -> list[int]:
    """1-based recnos of live records with this DOCID + DOCNO."""
    hdr = writer.header
    fid, fno = hdr.field("DOCID"), hdr.field("DOCNO")
    data, rl = writer.read_all(), hdr.record_length
    want_id, want_no = str(docid).encode(), str(docno).encode()
    hits = []
    for i in range(len(data) // rl):
        rec = data[i * rl:(i + 1) * rl]
        if rec[0:1] == b"*":
            continue
        if rec[fno.offset:fno.offset + fno.length].strip() == want_no and rec[fid.offset:fid.offset + fid.length].strip() == want_id:
            hits.append(i + 1)
    return hits


def write_posting(job: dict, archives: str, docnum: str, base: str, api_key: str, post, conn_failures: list[str],
                  live_folder: str | None = None, runner: list[str] | None = None) -> dict:
    """Writes one posting; returns the report for .../complete. With live_folder it also goes into HOMS's live tables
    (or_hdr/or_det, gatr_hdr/gatr_det, through runner = HOMSPOST.EXE) and is Done only when both have it."""
    report = _write_archive(job, archives, docnum, base, api_key, post, conn_failures)
    docno = report.pop("docNo", None)
    if not live_folder or report.get("error"):
        return report
    live = write_live(job, docno, live_folder, runner, _values)
    if live.get("error"):
        return live
    report["alreadyPresent"] = report["alreadyPresent"] and live["livePresent"]
    return report


def _write_archive(job: dict, archives: str, docnum: str, base: str, api_key: str, post, conn_failures: list[str]) -> dict:
    head_name, line_name = job["table"] + job["monthKey"], job["detailTable"] + job["monthKey"]
    head_path, line_path = _path(archives, head_name), _path(archives, line_name)
    if head_path is None or line_path is None:
        missing = head_name if head_path is None else line_name
        return {"retryLater": True, "error": f"Waiting for HOMS to create {missing} in {archives}."}

    docno = job.get("docNo")
    if docno is None:  # JV: next number from HOMS DOCNUM, recorded in HOMSys before anything is written
        docno = claim_number(docnum, "JV", LOCK_TIMEOUT_S, create_missing=False)
        log.info("posting %s: claimed JV %s from %s", job["id"], docno, docnum)
        if post(f"{base}/homs-postings/{job['id']}/docno", api_key, {"docNo": docno}, conn_failures) is None:
            return {"retryLater": True, "error": f"JV {docno} claimed but HOMSys couldn't record it (number skipped)."}

    with DbfWriter(line_path) as lines, DbfWriter(head_path) as head:
        with lock_append(lines.fileobj, LOCK_TIMEOUT_S), lock_append(head.fileobj, LOCK_TIMEOUT_S):
            if _doc_records(head, job["docId"], docno):
                return {"file": os.path.basename(head_path), "alreadyPresent": True, "docNo": docno}
            for recno in _doc_records(lines, job["docId"], docno):  # lines of an interrupted earlier run
                lines.mark_deleted(recno)
            for row in job["lines"]:
                lines.append_record(_values(lines, {**row, "DOCNO": docno}))
            head.append_record(_values(head, {**job["header"], "DOCNO": docno}))
    log.info("posting %s: %s %s written to %s (%d line(s))", job["id"], job["kind"], docno, os.path.basename(head_path), len(job["lines"]))
    return {"file": os.path.basename(head_path), "alreadyPresent": False, "docNo": docno}


def process_pending_postings(base: str, api_key: str, archives: str, docnum: str, post, conn_failures: list[str],
                             live_folder: str | None = None, runner: list[str] | None = None) -> bool:
    """base = .../api/collections/sync. Claims the queued postings and writes them."""
    try:
        resp = requests.get(base + "/homs-postings/pending", headers={"X-Api-Key": api_key}, timeout=60)
        resp.raise_for_status()
        jobs = resp.json().get("data") or []
    except requests.ConnectionError as exc:
        log.error("%s/homs-postings/pending -> could not connect: %s", base, exc)
        if base not in conn_failures:
            conn_failures.append(base)
        return False
    except requests.RequestException as exc:
        log.error("%s/homs-postings/pending -> failed: %s", base, exc)
        return False

    ok = True
    for job in jobs:
        try:
            report = write_posting(job, archives, docnum, base, api_key, post, conn_failures, live_folder, runner)
        except (LockTimeout, OSError) as exc:   # file busy / share hiccup: try again next minute
            report = {"retryLater": True, "error": f"{type(exc).__name__}: {exc}"}
        except Exception as exc:                # bad data etc.: park it for finance to see
            report = {"error": f"{type(exc).__name__}: {exc}"}
        if report.get("error"):
            log.warning("posting %s (%s): %s", job["id"], job["kind"], report["error"])
        done = post(f"{base}/homs-postings/{job['id']}/complete", api_key,
                    {"file": report.get("file"), "alreadyPresent": bool(report.get("alreadyPresent")),
                     "error": report.get("error"), "retryLater": bool(report.get("retryLater"))}, conn_failures)
        ok = ok and done is not None and not report.get("error")
    return ok
