#!/usr/bin/env python3
"""Single-shot legacy-master sync check, meant to be fired by Windows Task
Scheduler on a repeating trigger (e.g. every 5 minutes) rather than looping
itself — Task Scheduler owns the recurrence.

Generalized runner for HOMSys's "legacy master DBF -> SQL" sync endpoints —
today that's just pricing (/api/pricing/sync), but more legacy VFP masters
(reference data, etc.) are expected to get their own importer + sync
endpoint over time. Add a URL to HOMSYS_SYNC_URLS and it's covered, no code
change needed here.

All DBF parsing and change-detection logic lives on the .NET side, one
importer per data family — this script has no F:\\ awareness at all. A run
that finds nothing new just returns fast, so a short interval is safe.

Config via environment variables (set them as System/User env vars so
Task Scheduler picks them up, since a scheduled task has no console to pass
args to):
  HOMSYS_SYNC_URLS  comma-separated list, e.g.
                    "http://host:5200/api/pricing/sync,http://host:5200/api/reference/sync"
  HOMSYS_API_KEY    must match the target endpoint(s)' configured API key

Logs to %LOCALAPPDATA%\\HOMSys\\legacy_master_watcher.log (Task Scheduler
runs headless — nothing written to a console is visible after the fact).
"""
import os
import sys
import logging
import requests

LOG_DIR = os.path.join(os.environ.get("LOCALAPPDATA", "."), "HOMSys")
os.makedirs(LOG_DIR, exist_ok=True)
LOG_FILE = os.path.join(LOG_DIR, "legacy_master_watcher.log")

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s %(message)s",
    handlers=[logging.FileHandler(LOG_FILE)],
)
log = logging.getLogger("legacy_master_watcher")


def sync_one(url: str, api_key: str) -> bool:
    try:
        resp = requests.post(url, headers={"X-Api-Key": api_key}, timeout=120)
        resp.raise_for_status()
        log.info("%s -> ok: %s", url, resp.json().get("data"))
        return True
    except requests.RequestException as exc:
        log.error("%s -> failed: %s", url, exc)
        return False


def main() -> int:
    urls_raw = os.environ.get("HOMSYS_SYNC_URLS")
    api_key = os.environ.get("HOMSYS_API_KEY")
    if not urls_raw or not api_key:
        log.error("HOMSYS_SYNC_URLS and HOMSYS_API_KEY must both be set")
        return 1

    urls = [u.strip() for u in urls_raw.split(",") if u.strip()]
    if not urls:
        log.error("HOMSYS_SYNC_URLS is set but empty after parsing")
        return 1

    results = [sync_one(url, api_key) for url in urls]
    return 0 if all(results) else 1


if __name__ == "__main__":
    sys.exit(main())
