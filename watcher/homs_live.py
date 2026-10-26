"""Writes a posted document into HOMS's live tables too (after the ARCHIVES month files):
  OR        -> or_hdr / or_det       (as OH03 / OD03)
  RPDC, JV  -> gatr_hdr / gatr_det   (as GH03 / GD03)
HOMS keeps every document in these (posted ones included) and copies them into the ARCHIVES month files when it
posts. Unlike the month files they have CDX indexes, which Python can't maintain, so the rows are staged here
(field-for-field like the live tables, datetime fields left empty) and HOMSPOST.EXE -- VFP6, the runtime HOMS uses --
appends them with native APPEND, keeping every tag and VFP's own locks. See homspost.prg.
The runner is the compiled exe in production; in a dev test it can be [vfp6.exe, homspost.prg].
"""
import logging
import os
import shutil
import subprocess
import tempfile

from dbf.reader import DbfTable
from dbf.writer import DbfWriter, create_dbf

log = logging.getLogger("legacy_master_watcher")

RUN_TIMEOUT_S = 120
_STAGE_TYPES = ("C", "N", "F", "D", "L")
LIVE_TABLES = {"OH03": ("or_hdr", "or_det"), "GH03": ("gatr_hdr", "gatr_det")}


def _live_path(folder: str, name: str) -> str | None:
    for f in os.listdir(folder):
        if f.lower() == name.lower() + ".dbf":
            return os.path.join(folder, f)
    return None


def _stage(live_path: str, stage_path: str, rows: list[dict], to_values) -> None:
    with DbfTable(live_path) as t:
        fields = [f for f in t.fields if f.type in _STAGE_TYPES]
    create_dbf(stage_path, fields)
    with DbfWriter(stage_path) as w:
        for row in rows:
            w.append_record(to_values(w, row))


def write_live(job: dict, docno: int, live_folder: str, runner: list[str], to_values) -> dict:
    """Returns {"livePresent": bool} when the document is in the live tables (written now or already there),
    else {"retryLater"/"error": ...}."""
    hdr_name, det_name = LIVE_TABLES[job["table"]]
    hdr, det = _live_path(live_folder, hdr_name), _live_path(live_folder, det_name)
    if hdr is None or det is None:
        return {"error": f"{hdr_name} / {det_name} not found in {live_folder}."}

    root = os.path.join(os.environ.get("LOCALAPPDATA", "."), "HOMSys")
    os.makedirs(root, exist_ok=True)
    work = tempfile.mkdtemp(prefix=f"homs_live_{job['id']}_", dir=root)
    try:
        _stage(hdr, os.path.join(work, "stg_hdr.dbf"), [{**job["header"], "DOCNO": docno}], to_values)
        _stage(det, os.path.join(work, "stg_det.dbf"), [{**l, "DOCNO": docno} for l in job["lines"]], to_values)
        with open(os.path.join(work, "job.txt"), "w", encoding="ascii", newline="") as fh:   # no \r\r\n: VFP would read blank lines
            fh.write(f"{live_folder}\r\n{job['docId']}\r\n{docno}\r\n{hdr_name}\r\n{det_name}\r\n")

        proc = subprocess.Popen(runner, env={**os.environ, "HOMS_JOB": work})
        try:
            proc.wait(RUN_TIMEOUT_S)
        except subprocess.TimeoutExpired:
            proc.kill()   # this run's own process only (exact PID), never VFP by name
            return {"retryLater": True, "error": f"HOMSPOST didn't finish in {RUN_TIMEOUT_S}s ({hdr_name} / {det_name} busy?)."}

        result_path = os.path.join(work, "result.txt")
        result = open(result_path, encoding="cp1252").read().strip() if os.path.isfile(result_path) else ""
        if result in ("OK", "PRESENT"):
            log.info("posting %s: %s %s %s in %s/%s", job["id"], job["kind"], docno,
                     "written" if result == "OK" else "already", hdr_name, det_name)
            return {"livePresent": result == "PRESENT"}
        # Busy tables (lock not got within SET REPROCESS) and the like: try again next minute.
        return {"retryLater": True, "error": f"{hdr_name} / {det_name}: {result or 'HOMSPOST gave no result (VFP6 runtime missing?)'}"}
    finally:
        shutil.rmtree(work, ignore_errors=True)
