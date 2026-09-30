"""Durable store-and-forward outbox for BMS -> HOMSys bridge events.

A branch can lose internet at any time, but BMS keeps working on its LAN
share. So every event salesorder_bridge.py is asked to push (lock,
deallocate, invoice, OOS, delivery, cancel) is first written here as one
small JSON file, and only then POSTed. A failed POST leaves the file for the
next bridge run (every VFP click, plus the scheduled `--drain` task), so
nothing is lost while offline -- HOMSys just catches up late.

Lives in <bms data folder>\\homsys_outbox\\ (the shared LAN folder, NOT
%LOCALAPPDATA%), so any workstation's bridge can deliver an event even if the
PC that recorded it is switched off.

Ordering: file names start with a nanosecond timestamp, and only one drainer
runs at a time per folder (.drain.lock), so events go out oldest-first.
Timestamps come from each workstation's own clock -- skew between PCs can
reorder events recorded on different PCs within the same few seconds, which
is fine in practice (lock -> invoice -> delivery for one SO are minutes apart).
"""
from __future__ import annotations

import json
import logging
import os
import re
import shutil
import time
import uuid
from datetime import datetime
from typing import Callable

import requests

log = logging.getLogger("salesorder_bridge")

LOCK_NAME = ".drain.lock"
LOCK_STALE_SECONDS = 600  # a drainer that crashed can't hold the folder forever


def outbox_dir(root: str) -> str:
    return os.path.join(root, "homsys_outbox")


def _write_atomic(path: str, data: dict) -> None:
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8") as fh:
        json.dump(data, fh)
    os.replace(tmp, path)


_last_ns = 0


def _next_stamp() -> int:
    """Strictly increasing within this process. time.time_ns() on Windows only
    ticks every 100 ns-16 ms, so two events queued by one click (e.g. lock +
    oos) can read the same value -- their order would then fall to the random
    suffix, breaking FIFO. Bumping by 1 ns on a repeat keeps them in order."""
    global _last_ns
    _last_ns = max(time.time_ns(), _last_ns + 1)
    return _last_ns


def enqueue(root: str, kind: str, **data) -> str:
    """Writes one event file and returns its path. Atomic (temp + rename), so a
    drainer on another PC never sees a half-written file."""
    d = outbox_dir(root)
    os.makedirs(d, exist_ok=True)
    path = os.path.join(d, f"{_next_stamp():020d}-{uuid.uuid4().hex[:8]}.json")
    _write_atomic(path, {"kind": kind, "created": datetime.now().isoformat(timespec="seconds"), **data})
    return path


def update(path: str, **data) -> bool:
    """Adds fields to a queued event (e.g. the OOS snapshot, captured after VFP
    has already been released). False if it was already delivered."""
    try:
        with open(path, "r", encoding="utf-8") as fh:
            event = json.load(fh)
    except FileNotFoundError:
        return False
    event.update(data)
    _write_atomic(path, event)
    return True


# Event files are exactly "<20-digit ns stamp>-<8 hex>.json" (see enqueue). Anything
# else in the folder -- rfc_sent.json / sent_events.json (the processed-backlog
# lists), .tmp files, the .drain.lock / .last_reconcile markers -- is NOT an event.
_EVENT_NAME = re.compile(r"^\d{20}-[0-9a-f]{8}\.json$")


def pending(root: str) -> list[str]:
    d = outbox_dir(root)
    if not os.path.isdir(d):
        return []
    return sorted(os.path.join(d, f) for f in os.listdir(d) if _EVENT_NAME.match(f))


def _acquire_lock(d: str) -> str | None:
    lock = os.path.join(d, LOCK_NAME)
    for _ in range(2):
        try:
            os.close(os.open(lock, os.O_CREAT | os.O_EXCL | os.O_WRONLY))
            return lock
        except FileExistsError:
            try:
                if time.time() - os.path.getmtime(lock) < LOCK_STALE_SECONDS:
                    return None  # someone else is draining right now
                os.remove(lock)  # stale -- previous drainer died
            except FileNotFoundError:
                pass
    return None


def _park_failed(path: str, reason: str) -> None:
    """A permanently rejected event (4xx other than 404, or a bug) must not
    block every event queued behind it -- park it for a human to look at."""
    failed = os.path.join(os.path.dirname(path), "failed")
    os.makedirs(failed, exist_ok=True)
    shutil.move(path, os.path.join(failed, os.path.basename(path)))
    log.error("outbox: parked %s in failed\\ -- %s", os.path.basename(path), reason)


def _next_event(root: str) -> str | None:
    files = pending(root)
    return files[0] if files else None


def drain(root: str, deliver: Callable[[dict], None], on_empty: Callable[[], None] | None = None) -> bool | None:
    """Delivers queued events strictly FIFO (oldest file name first) via
    `deliver(event)`, which raises on failure. Returns True if HOMSys was
    reachable, False if the drain stopped on a network error (the failed event
    and everything behind it kept, order preserved -- nothing ever jumps
    ahead), None if there was nothing to send or another drainer holds the lock.

    The folder is re-listed after every event, so an event queued mid-drain is
    still sent in order in the same pass. `on_empty` (the reconcile sweep) runs
    only once the queue is empty and while still holding the lock, so a
    state-based re-post can never overtake an older queued event.

    Outcome per event:
      success                      -> deleted
      HTTP 404                     -> deleted (not a HOMSys order -- BMS fires
                                      these for every order, most aren't ours)
      other 4xx (except 401/408/429) -> parked in failed\\
      network error / timeout / 5xx / 401 / DBF read error -> stop, keep
      any other exception (a bug)  -> parked in failed\\
    """
    if on_empty is None and not pending(root):
        return None
    d = outbox_dir(root)
    os.makedirs(d, exist_ok=True)
    lock = _acquire_lock(d)
    if lock is None:
        log.info("outbox: another drainer is running, leaving %d event(s) to it", len(pending(root)))
        return None

    reachable: bool | None = None
    try:
        while (path := _next_event(root)) is not None:
            name = os.path.basename(path)
            try:
                with open(path, "r", encoding="utf-8") as fh:
                    event = json.load(fh)
            except FileNotFoundError:
                continue
            except (OSError, json.JSONDecodeError) as ex:
                _park_failed(path, f"unreadable: {ex}")
                continue

            try:
                deliver(event)
            except requests.HTTPError as ex:
                code = ex.response.status_code if ex.response is not None else None
                if code == 404:
                    log.info("outbox: %s %s -> 404, not a HOMSys order, dropped", event.get("kind"), event.get("so_no"))
                    os.remove(path)
                    reachable = True
                elif code is not None and 400 <= code < 500 and code not in (401, 408, 429):
                    detail = ex.response.text[:300] if ex.response is not None else ""
                    _park_failed(path, f"HTTP {code}: {detail}")
                    reachable = True
                else:
                    log.warning("outbox: HTTP %s on %s, stopping drain (%d left)", code, name, len(pending(root)))
                    return False
            except (requests.ConnectionError, requests.Timeout):
                log.warning("outbox: HOMSys unreachable, stopping drain (%d event(s) kept)", len(pending(root)))
                return False
            except OSError:
                # e.g. a DBF the event re-reads is momentarily locked/unreachable --
                # transient; keep it and preserve order.
                log.exception("outbox: I/O error delivering %s, stopping drain", name)
                return reachable
            except Exception as ex:  # noqa: BLE001 -- a bug must not wedge the queue
                log.exception("outbox: unexpected error delivering %s", name)
                _park_failed(path, f"{type(ex).__name__}: {ex}")
            else:
                os.remove(path)
                reachable = True
                log.info("outbox: delivered %s %s", event.get("kind"), event.get("so_no"))
            try:
                os.utime(lock)  # keep the lock fresh on a long drain
            except OSError:
                pass
        if on_empty is not None:
            on_empty()
        return reachable
    finally:
        try:
            os.remove(lock)
        except OSError:
            pass
