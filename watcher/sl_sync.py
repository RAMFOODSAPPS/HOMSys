"""Branch "Fetch invoices" requests for HOMSys Collections, answered from the EOD sales-ledger files.

Branches never run this exe: they queue a request in HOMSys (invoice number + invoice date), and
LegacyMasterWatcher.exe --requests (HO, every minute) polls for it, reads only the files needed:
  - the invoice:  SL{WH}{MMYY}.DBF of its invoice month (or the next month, for late postings);
                  DOCID=1 rows, Total = SUM(AMT + STAX) of its lines (AMT is ex-VAT; BMS's OR applies
                  the VAT-inclusive amount, e.g. inv 92152939: 878.70 + 105.44 = 984.14)
  - its RFCs:     that file and every later month's file; DOCID=6 with REFID=1, REFNO = invoice no,
                  signed SUM(AMT + STAX) — an RFC is often posted months after the invoice
and posts one scoped payload per file, then reports which invoices were found. The branch is the
WH in the file name (the rows' own WHSENO is a different warehouse). Never touches arof4win;
read-only on the archives folder. File names are computed, never listed (ARCHIVES holds thousands).
"""
import logging
import os
from datetime import date

import requests

from dbf.reader import get_date, get_decimal, get_int, get_string, scan_fields

log = logging.getLogger("legacy_master_watcher")

FIELDS = ["DOCID", "DOCNO", "DOCDATE", "TRDATE", "CUSTKEY", "CKEY", "CUSNAME", "TERM", "PONUM", "AMT", "STAX", "REFID", "REFNO",
          "SALESMAN", "EMPNO", "WHSENO"]
NO_DATE = date(1900, 1, 1)


def read_sl_file(path: str, invoice_nos: set[int], rfc_nos: set[int]) -> tuple[list[dict], list[dict]]:
    """Aggregate one SL file for just these invoice numbers (DOCNO of DOCID=1 / REFNO of DOCID=6)."""
    invoices: dict[int, dict] = {}
    # (invoice no., RFC no.) -> the RFC's rows summed; the RFC no. is the DOCID=6 row's DOCNO.
    rfcs: dict[tuple[int, int], dict] = {}
    for row in scan_fields(path, FIELDS):
        docid = get_int(row, "DOCID")
        if docid == 1:
            docno = get_int(row, "DOCNO")
            if docno not in invoice_nos:
                continue
            inv = invoices.get(docno)
            if inv is None:
                inv = invoices[docno] = {
                    "docNo": docno,
                    "docDate": (get_date(row, "DOCDATE") or get_date(row, "TRDATE") or NO_DATE).isoformat(),
                    "custKey": get_string(row, "CUSTKEY"),
                    "cKey": get_string(row, "CKEY"),
                    "custName": get_string(row, "CUSNAME"),
                    "term": get_int(row, "TERM"),
                    "poNum": get_string(row, "PONUM") or None,
                    # Carried onto the HOMS OR / RPDC lines HOMSys writes (OD03/GD03 SALESMAN, EMPNO, WHSENO).
                    "salesman": get_int(row, "SALESMAN"),
                    "empNo": get_int(row, "EMPNO"),
                    "custWhseNo": get_int(row, "WHSENO"),
                    "total": 0.0,
                }
            inv["total"] += get_decimal(row, "AMT") + get_decimal(row, "STAX")
        elif docid == 6 and get_int(row, "REFID") == 1:
            refno = get_int(row, "REFNO")
            if refno in rfc_nos:
                key = (refno, get_int(row, "DOCNO"))
                rfc = rfcs.get(key)
                if rfc is None:
                    date = get_date(row, "DOCDATE") or get_date(row, "TRDATE")
                    rfc = rfcs[key] = {"invoiceNo": refno, "rfcNo": key[1], "rfcDate": date.isoformat() if date else None, "amt": 0.0}
                rfc["amt"] += get_decimal(row, "AMT") + get_decimal(row, "STAX")
    for inv in invoices.values():
        inv["total"] = round(inv["total"], 2)
    return list(invoices.values()), [{**r, "amt": round(r["amt"], 2)} for r in rfcs.values()]


def _month(d: str) -> tuple[int, int]:
    return int(d[0:4]), int(d[5:7])


def _next_month(ym: tuple[int, int]) -> tuple[int, int]:
    return (ym[0] + 1, 1) if ym[1] == 12 else (ym[0], ym[1] + 1)


def _sl_path(archives: str, wh: int, ym: tuple[int, int]) -> str:
    return os.path.join(archives, f"SL{wh:02d}{ym[1]:02d}{ym[0] % 100:02d}.DBF")


def process_request(req: dict, archives: str, base: str, api_key: str, post, conn_failures: list[str]) -> tuple[list[int], str | None]:
    """Satisfy one request: (found invoice numbers, error)."""
    items = {i["invoiceNo"]: _month(i["invoiceDate"]) for i in req["items"]}
    today = date.today()
    found: set[int] = set()
    ym, last = min(items.values()), (today.year, today.month)
    while ym <= last:
        path = _sl_path(archives, req["wh"], ym)
        rfc_nos = {n for n, m in items.items() if m <= ym}
        inv_nos = {n for n, m in items.items() if m <= ym <= _next_month(m) and n not in found}
        if os.path.isfile(path):
            name = os.path.basename(path).upper()
            try:
                invoices, rfcs = read_sl_file(path, inv_nos, rfc_nos)
            except (OSError, EOFError) as exc:
                return sorted(found), f"{name}: read failed: {exc}"
            found.update(i["docNo"] for i in invoices)
            if invoices or rfcs:
                payload = {"wh": req["wh"], "sourceFile": name, "invoices": invoices, "rfcs": rfcs,
                           "scopeInvoiceNos": sorted(rfc_nos)}
                if post(base + "/posted-invoices", api_key, payload, conn_failures) is None:
                    return sorted(found), f"{name}: could not send to HOMSys"
        ym = _next_month(ym)
    return sorted(found), None


def process_pending_requests(base: str, api_key: str, archives: str, post, conn_failures: list[str]) -> bool:
    """base = .../api/collections/sync. Polls HOMSys for branch requests and answers them."""
    try:
        resp = requests.get(base + "/pending", headers={"X-Api-Key": api_key}, timeout=60)
        resp.raise_for_status()
        pending = resp.json().get("data") or []
    except requests.ConnectionError as exc:
        log.error("%s/pending -> could not connect: %s", base, exc)
        if base not in conn_failures:
            conn_failures.append(base)
        return False
    except requests.RequestException as exc:
        log.error("%s/pending -> failed: %s", base, exc)
        return False

    ok = True
    for req in pending:
        try:
            found, error = process_request(req, archives, base, api_key, post, conn_failures)
        except Exception as exc:  # report instead of leaving the branch waiting on a stuck request
            found, error = [], f"unexpected: {exc}"
        log.info("request %s (WH %s, %d invoice(s)): found %d, error=%s", req["id"], req["wh"], len(req["items"]), len(found), error)
        done = post(f"{base}/requests/{req['id']}/complete", api_key, {"foundInvoiceNos": found, "error": error}, conn_failures)
        ok = ok and done is not None and error is None
    return ok
