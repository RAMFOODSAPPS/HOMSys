"""FoxPro-compatible byte-range locking via pywin32, so this bridge and a
live VFP6 session can safely interleave record-level locks on the same
open DBF the way two VFP processes would.

VFP's byte-offset scheme — MEASURED 2026-10-08 against VFP6 and VFP9 (a live
VFP holding each lock while LockFileEx probed every byte up to 2^33):
  - record lock RLOCK(n):          byte 0x7FFFFFFE - n
  - header lock RLOCK("0") / APPEND: byte 0x7FFFFFFE          (n = 0)
  - FLOCK():                        a range that always starts at 0x7FA7AD36
    (its end varies), and does NOT include the header byte.
(An earlier assumption of 0x7FFFFFFF for the header was wrong: VFP appended
while Python "held" it and Python overwrote VFP's new record.)

lock_append() is the one to hold across "read RECCOUNT, write records,
update RECCOUNT" on a table VFP may have open: it takes the header byte
(blocks / is blocked by any VFP APPEND or RLOCK("0")) and the first FLOCK
byte (blocks / is blocked by any VFP FLOCK()). Record locks are unaffected.

docnum.dbf's GETSAVE increment locks the specific SO-doctype record via
RLOCK, i.e. the record-lock helper, spun exactly like docnum.prg's
`do while .t. / mlock = rlock() / if mlock / exit / endif / enddo`.
"""
from __future__ import annotations

import time

import pywintypes
import win32con
import win32file

_RECORD_BASE = 0x7FFFFFFE
_HEADER_BYTE = 0x7FFFFFFE
_FLOCK_FIRST_BYTE = 0x7FA7AD36


class LockTimeout(TimeoutError):
    pass


def _handle(fileobj):
    return win32file._get_osfhandle(fileobj.fileno())


def _overlapped(offset: int) -> pywintypes.OVERLAPPED:
    ov = pywintypes.OVERLAPPED()
    ov.Offset = offset & 0xFFFFFFFF
    ov.OffsetHigh = 0
    return ov


class _ByteRangeLock:
    """Context manager: exclusive-locks a single byte in fileobj for its
    lifetime, spin-retrying (matching docnum.prg's rlock() loop) until
    acquired or timeout_s elapses.
    """

    def __init__(self, fileobj, offset: int, timeout_s: float = 10.0, poll_s: float = 0.05):
        self._fileobj = fileobj
        self._offset = offset
        self._timeout_s = timeout_s
        self._poll_s = poll_s
        self._locked = False

    def acquire(self):
        handle = _handle(self._fileobj)
        deadline = time.monotonic() + self._timeout_s
        flags = win32con.LOCKFILE_EXCLUSIVE_LOCK | win32con.LOCKFILE_FAIL_IMMEDIATELY
        while True:
            try:
                # pywin32: LockFileEx(h, flags, nbytesLow, nbytesHigh, ol) — exactly 1 byte (was (0, 1) = 4 GB,
                # which reached past the header byte and made two locks on one handle collide).
                win32file.LockFileEx(handle, flags, 1, 0, _overlapped(self._offset))
                self._locked = True
                return self
            except pywintypes.error:
                if time.monotonic() >= deadline:
                    raise LockTimeout(f"Timed out waiting for byte {self._offset:#x} on {self._fileobj.name}")
                time.sleep(self._poll_s)

    def release(self):
        if not self._locked:
            return
        handle = _handle(self._fileobj)
        try:
            win32file.UnlockFileEx(handle, 1, 0, _overlapped(self._offset))
        finally:
            self._locked = False

    def __enter__(self):
        return self.acquire()

    def __exit__(self, *exc):
        self.release()


def lock_record(fileobj, recno: int, timeout_s: float = 10.0) -> _ByteRangeLock:
    """1-based recno, matching VFP's RECNO()/RLOCK()."""
    return _ByteRangeLock(fileobj, _RECORD_BASE - recno, timeout_s)


def lock_header(fileobj, timeout_s: float = 10.0) -> _ByteRangeLock:
    """VFP's RLOCK("0") byte only — prefer lock_append() for appends."""
    return _ByteRangeLock(fileobj, _HEADER_BYTE, timeout_s)


class _AppendLock:
    """Header byte + first FLOCK byte, both held (see module doc)."""

    def __init__(self, fileobj, timeout_s: float):
        self._locks = [_ByteRangeLock(fileobj, _FLOCK_FIRST_BYTE, timeout_s), _ByteRangeLock(fileobj, _HEADER_BYTE, timeout_s)]

    def __enter__(self):
        taken = []
        try:
            for lk in self._locks:
                taken.append(lk.acquire())
        except BaseException:
            for lk in reversed(taken):
                lk.release()
            raise
        return self

    def __exit__(self, *exc):
        for lk in reversed(self._locks):
            lk.release()


def lock_append(fileobj, timeout_s: float = 10.0) -> _AppendLock:
    """Hold while appending to a table VFP may have open (waits for any VFP APPEND / FLOCK, and makes VFP wait)."""
    return _AppendLock(fileobj, timeout_s)
