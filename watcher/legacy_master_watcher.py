#!/usr/bin/env python3
"""Single-shot pricing-master sync, meant to be fired by Windows Task
Scheduler on a repeating trigger (e.g. every 5 minutes) rather than looping
itself — Task Scheduler owns the recurrence.

Azure can't reach F:\\, so this exe reads and parses F:\\'s pricing DBFs
itself (dbf_reader.py, a byte-for-byte port of DbfReader.cs), diffs the
result against its own last-synced snapshot, and POSTs only the rows that
actually changed to POST /api/masters/sync. A quiet run (nothing changed
anywhere) still POSTs, with an empty body — cheap, and keeps the endpoint's
"last seen" fresh.

api/reference/sync is intentionally not called here — Customers/Products
stay on the manual `import-reference-data` CLI command.

Config via config.json, sitting next to this script (or next to
LegacyMasterWatcher.exe when packaged with PyInstaller) — a plain file you
can edit directly, no env vars / setx / reboot required:
  {
    "apiKey": "<the real HeadlessApiKey value>",
    "syncs": [
      { "url": "https://<host>/api/masters/sync", "path": "F:\\\\" }
    ]
  }
"path" is the local root this exe reads F:\\-style pricing DBFs from
(expects \\PMDM, \\AUTOPROG\\ADDON\\{branch}, \\AUTOPROG\\CUSTOMER\\{branch}
under it) — read locally only, never sent to the server. Optional, defaults
to "F:\\". "syncs" stays a list for parity with the config shape, but today
every entry is a pricing sync; a future second legacy-master data family
would need its own reader function here, not just a new config line.
See config.example.json for a filled-in template.

Local diff state lives in %LOCALAPPDATA%\\HOMSys\\pricing-snapshot.json — the
full row set as of the last successful sync, keyed the same way as the
server's own tables (ProdNo / CategoryCode / (CProdNo,Zone) / RecNo /
(Branch,RecNo)). A branch folder that disappears from disk diffs to "delete
everything for it" against the snapshot, no special-casing needed. Updated
only after a successful POST.

Logs to %LOCALAPPDATA%\\HOMSys\\legacy_master_watcher.log (Task Scheduler
runs headless — nothing written to a console is visible after the fact).
"""
import ctypes
import json
import os
import sys
import logging
import requests

from dbf_reader import DbfReader

LOG_DIR = os.path.join(os.environ.get("LOCALAPPDATA", "."), "HOMSys")
os.makedirs(LOG_DIR, exist_ok=True)
LOG_FILE = os.path.join(LOG_DIR, "legacy_master_watcher.log")
SNAPSHOT_FILE = os.path.join(LOG_DIR, "pricing-snapshot.json")

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s %(message)s",
    handlers=[logging.FileHandler(LOG_FILE)],
)
log = logging.getLogger("legacy_master_watcher")

MB_ICONERROR = 0x10
DEFAULT_ROOT = "F:\\"


def show_timed_messagebox(title: str, text: str, timeout_ms: int = 10000) -> None:
    """Best-effort popup via the undocumented but long-standing user32
    MessageBoxTimeoutW — auto-dismisses after timeout_ms so it can never hang
    this process when the 5-minute Task Scheduler trigger fires with nobody
    logged in (a blocking MessageBoxW has stalled a branch's whole sync cycle
    before — see reference_autolaunched_setup_exe_blocking). Never let a UI
    problem fail the sync itself."""
    try:
        ctypes.windll.user32.MessageBoxTimeoutW(0, text, title, MB_ICONERROR, 0, timeout_ms)
    except Exception:
        pass


def app_dir() -> str:
    if getattr(sys, "frozen", False):
        return os.path.dirname(sys.executable)
    return os.path.dirname(os.path.abspath(__file__))


def load_config() -> dict | None:
    config_path = os.path.join(app_dir(), "config.json")
    if not os.path.isfile(config_path):
        log.error("Config file not found at %s (see config.example.json)", config_path)
        return None
    try:
        with open(config_path, "r", encoding="utf-8-sig") as f:
            return json.load(f)
    except (OSError, json.JSONDecodeError) as exc:
        log.error("Failed to read %s: %s", config_path, exc)
        return None


def load_snapshot() -> dict:
    if not os.path.isfile(SNAPSHOT_FILE):
        return {}
    try:
        with open(SNAPSHOT_FILE, "r", encoding="utf-8") as f:
            return json.load(f)
    except (OSError, json.JSONDecodeError) as exc:
        log.warning("Snapshot at %s unreadable (%s) — treating as empty; next sync will be a full baseline", SNAPSHOT_FILE, exc)
        return {}


def save_snapshot(state: dict) -> None:
    tmp = SNAPSHOT_FILE + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(state, f)
    os.replace(tmp, SNAPSHOT_FILE)


# ---- DBF -> current-state readers (field names match PricingDataImporter.cs exactly) ----

def read_product_prices(pmdm_dir: str) -> dict:
    result = {}
    with DbfReader(os.path.join(pmdm_dir, "PROD4WIN.DBF")) as reader:
        for r in reader.records():
            result[str(r.get_int("PRODNO"))] = {
                "newPrice": r.get_decimal_or_null("NEWPRICE"),
                "priceFrom": r.get_date("FROM"),
                "oldPrice1": r.get_decimal_or_null("OLDPRICE1"),
                "srp": r.get_decimal_or_null("SRP"),
                "category": r.get_string("CATEGORY"),
                "barcode": r.get_string("BARCODE"),
                "caseBarcode": r.get_string("CBARCODE"),
            }
    return result


def read_product_categories(pmdm_dir: str) -> dict:
    result = {}
    with DbfReader(os.path.join(pmdm_dir, "prodcat.dbf")) as reader:
        for r in reader.records():
            code = r.get_string("CATEGORY")
            if not code:
                continue
            result[code] = {
                "groupNo": r.get_int("GROUP"),
                "groupDesc": r.get_string("GROUPDESC"),
                "subCat": r.get_string("SUBCAT"),
                "seqNo": r.get_int("SEQNO"),
            }
    return result


def read_prlistx2(pmdm_dir: str) -> dict:
    """ZONE is a comma-separated list of allowed zone codes per row, not a
    single value — same split PricingDataImporter.cs applies."""
    result = {}
    with DbfReader(os.path.join(pmdm_dir, "PRLISTX2.DBF")) as reader:
        for r in reader.records():
            c_prod_no = r.get_string("CPRODNO")
            zone_field = r.get_string("ZONE")
            if not c_prod_no or not zone_field:
                continue
            for raw_zone in (z.strip() for z in zone_field.split(",")):
                if raw_zone:
                    result[f"{c_prod_no}|{raw_zone}"] = {"cProdNo": c_prod_no, "zone": raw_zone}
    return result


def read_prlistx(pmdm_dir: str) -> dict:
    result = {}
    with DbfReader(os.path.join(pmdm_dir, "PRLISTX.DBF")) as reader:
        for r in reader.records():
            c_prod_no = r.get_string("CPRODNO")
            if c_prod_no:
                result[c_prod_no] = True
    return result


def read_price_history(pmdm_dir: str) -> dict:
    result = {}
    with DbfReader(os.path.join(pmdm_dir, "PRCHST.DBF")) as reader:
        for r in reader.records():
            result[str(r.recno)] = {
                "prodNo": r.get_int("PRODNO"),
                "effective": r.get_date("EFFECTIVE"),
                "npAfVat": r.get_decimal("NPAFVAT"),
            }
    return result


def read_zone_addons(branch_dir: str) -> dict:
    result = {}
    with DbfReader(os.path.join(branch_dir, "ZONE.DBF")) as reader:
        for r in reader.records():
            result[str(r.recno)] = {
                "cProdNo": r.get_string("CPRODNO"),
                "cZone": r.get_string("CZONE"),
                "effDate": r.get_date("EFF_DATE"),
                "addOn": r.get_decimal("ADD_ON"),
                "rate": r.get_decimal("RATE"),
                "fixAmt": r.get_decimal("FIXAMT"),
            }
    return result


def read_zone2_addons(branch_dir: str) -> dict:
    result = {}
    with DbfReader(os.path.join(branch_dir, "ZONE2.DBF")) as reader:
        for r in reader.records():
            result[str(r.recno)] = {
                "custKey": r.get_string("CUSTKEY"),
                "cProdNo": r.get_string("CPRODNO"),
                "effDate": r.get_date("EFF_DATE"),
                "addOn": r.get_decimal("ADD_ON"),
                "rate": r.get_decimal("RATE"),
                "fixAmt": r.get_decimal("FIXAMT"),
            }
    return result


def read_customer_branch_zones(branch_dir: str) -> dict:
    result = {}
    with DbfReader(os.path.join(branch_dir, "CUST4WIN.DBF")) as reader:
        for r in reader.records():
            cust_key = r.get_string("CUSTKEY")
            if cust_key:
                result[str(r.recno)] = {"custKey": cust_key, "cZone": r.get_string("CZONE")}
    return result


def read_current_state(root: str) -> dict:
    """Branches are discovered from disk, not hardcoded — the server enforces
    its own ActiveBranches allowlist when applying the delta, so this exe
    doesn't need its own copy of that list."""
    pmdm_dir = os.path.join(root, "PMDM")
    addon_root = os.path.join(root, "AUTOPROG", "ADDON")
    customer_root = os.path.join(root, "AUTOPROG", "CUSTOMER")

    state = {
        "productPrices": read_product_prices(pmdm_dir),
        "productCategories": read_product_categories(pmdm_dir),
        "prlistX2Restrictions": read_prlistx2(pmdm_dir),
        "prlistXRestrictions": read_prlistx(pmdm_dir),
        "priceHistory": read_price_history(pmdm_dir),
        "branches": {},
    }

    if os.path.isdir(addon_root):
        for name in os.listdir(addon_root):
            branch_dir = os.path.join(addon_root, name)
            if not (os.path.isfile(os.path.join(branch_dir, "ZONE.DBF")) and os.path.isfile(os.path.join(branch_dir, "ZONE2.DBF"))):
                continue
            state["branches"].setdefault(name, {})
            state["branches"][name]["zoneAddOns"] = read_zone_addons(branch_dir)
            state["branches"][name]["zone2AddOns"] = read_zone2_addons(branch_dir)

    if os.path.isdir(customer_root):
        for name in os.listdir(customer_root):
            branch_dir = os.path.join(customer_root, name)
            if not os.path.isfile(os.path.join(branch_dir, "CUST4WIN.DBF")):
                continue
            state["branches"].setdefault(name, {})
            state["branches"][name]["customerBranchZones"] = read_customer_branch_zones(branch_dir)

    return state


# ---- current-state vs snapshot -> delta payload ----

def _diff_flat(current: dict, previous: dict) -> tuple[list[str], list[str]]:
    """New/changed keys, and keys no longer present."""
    upserts = [k for k, v in current.items() if previous.get(k) != v]
    deletes = [k for k in previous if k not in current]
    return upserts, deletes


def _build_recno_section(current: dict, previous: dict) -> dict | None:
    """Shared shape for priceHistory / zoneAddOns / zone2AddOns /
    customerBranchZones — all keyed by RecNo, all {upserts, deletes}."""
    upserts, deletes = _diff_flat(current, previous)
    if not upserts and not deletes:
        return None
    return {
        "upserts": [{"recNo": int(k), **current[k]} for k in upserts],
        "deletes": [int(k) for k in deletes],
    }


def _build_product_categories_section(current: dict, previous: dict) -> dict | None:
    upserts, deletes = _diff_flat(current, previous)
    if not upserts and not deletes:
        return None
    return {
        "upserts": [{"categoryCode": k, **current[k]} for k in upserts],
        "deletes": deletes,
    }


def _build_prlistx2_section(current: dict, previous: dict) -> dict | None:
    adds, removes = _diff_flat(current, previous)
    if not adds and not removes:
        return None
    return {
        "adds": [current[k] for k in adds],
        "removes": [previous[k] for k in removes],
    }


def _build_prlistx_section(current: dict, previous: dict) -> dict | None:
    adds = [k for k in current if k not in previous]
    removes = [k for k in previous if k not in current]
    if not adds and not removes:
        return None
    return {"adds": adds, "removes": removes}


def _build_branches_section(current: dict, previous: dict) -> dict | None:
    out = {}
    for name in set(current) | set(previous):
        cur = current.get(name, {})
        prev = previous.get(name, {})
        entry = {}
        zone = _build_recno_section(cur.get("zoneAddOns", {}), prev.get("zoneAddOns", {}))
        zone2 = _build_recno_section(cur.get("zone2AddOns", {}), prev.get("zone2AddOns", {}))
        cust = _build_recno_section(cur.get("customerBranchZones", {}), prev.get("customerBranchZones", {}))
        if zone is not None:
            entry["zoneAddOns"] = zone
        if zone2 is not None:
            entry["zone2AddOns"] = zone2
        if cust is not None:
            entry["customerBranchZones"] = cust
        if entry:
            out[name] = entry
    return out or None


def build_delta(current: dict, previous: dict) -> dict:
    payload = {}

    price_upserts, _ = _diff_flat(current.get("productPrices", {}), previous.get("productPrices", {}))
    if price_upserts:
        payload["productPrices"] = [{"prodNo": int(k), **current["productPrices"][k]} for k in price_upserts]

    section = _build_product_categories_section(current.get("productCategories", {}), previous.get("productCategories", {}))
    if section:
        payload["productCategories"] = section

    section = _build_prlistx2_section(current.get("prlistX2Restrictions", {}), previous.get("prlistX2Restrictions", {}))
    if section:
        payload["prlistX2Restrictions"] = section

    section = _build_prlistx_section(current.get("prlistXRestrictions", {}), previous.get("prlistXRestrictions", {}))
    if section:
        payload["prlistXRestrictions"] = section

    section = _build_recno_section(current.get("priceHistory", {}), previous.get("priceHistory", {}))
    if section:
        payload["priceHistory"] = section

    section = _build_branches_section(current.get("branches", {}), previous.get("branches", {}))
    if section:
        payload["branches"] = section

    return payload


def sync_pricing(url: str, api_key: str, root: str, conn_failures: list[str]) -> bool:
    log.info("Reading pricing masters from %s ...", root)
    try:
        current = read_current_state(root)
    except (OSError, EOFError) as exc:
        log.error("Failed to read DBFs under %s: %s", root, exc)
        return False

    delta = build_delta(current, load_snapshot())
    if delta:
        log.info("%s -> sending delta covering: %s", url, ", ".join(delta.keys()))
    else:
        log.info("%s -> nothing changed, sending empty delta", url)

    try:
        # A first-run baseline (no prior snapshot) can be tens of MB across
        # every branch found on disk — give the server room to actually
        # finish applying it rather than timing out a slow-but-succeeding
        # sync and re-sending the same huge payload forever.
        resp = requests.post(url, headers={"X-Api-Key": api_key}, json=delta, timeout=600)
    except requests.ConnectionError as exc:
        log.error("%s -> could not connect (check network/VPN and the URL): %s", url, exc)
        conn_failures.append(url)
        return False
    except requests.RequestException as exc:
        log.error("%s -> failed: %s", url, exc)
        return False

    try:
        resp.raise_for_status()
    except requests.RequestException as exc:
        log.error("%s -> failed: %s", url, exc)
        return False

    log.info("%s -> ok: %s", url, resp.json().get("data"))
    save_snapshot(current)
    return True


def main() -> int:
    config = load_config()
    if config is None:
        return 1

    api_key = config.get("apiKey")
    syncs = config.get("syncs")
    if not api_key or not isinstance(syncs, list) or not syncs:
        log.error("config.json must have a non-empty \"apiKey\" and a non-empty \"syncs\" list")
        return 1

    results = []
    conn_failures: list[str] = []
    for entry in syncs:
        url = entry.get("url") if isinstance(entry, dict) else None
        if not url:
            log.error("Skipping sync entry with no \"url\": %s", entry)
            results.append(False)
            continue
        root = (entry.get("path") if isinstance(entry, dict) else None) or DEFAULT_ROOT
        results.append(sync_pricing(url, api_key, root, conn_failures))

    if conn_failures:
        show_timed_messagebox(
            "HOMSys Sync - Connection Failed",
            "Could not reach:\n" + "\n".join(conn_failures) + "\n\nCheck network/VPN. See log for details.",
        )

    return 0 if all(results) else 1


if __name__ == "__main__":
    sys.exit(main())
