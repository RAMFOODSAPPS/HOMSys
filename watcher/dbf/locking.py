"""FoxPro-compatible byte-range locking via pywin32, so this bridge and a
live VFP6 session can safely interleave record-level locks on the same
open DBF the way two VFP processes would.

Convention (documented in the plan/ANALYSIS.md research, matches VFP's
own RLOCK()/FLOCK() byte-offset scheme):
  - record lock (RLOCK(n)):  byte offset 0x7FFFFFFE - n   (1 byte)
  - header/file lock (FLOCK): byte offset 0x7FFFFFFF      (1 byte)

The header lock is what VFP takes for APPEND BLANK (mutates RECCOUNT()
in the header, so no single RECNO() applies yet) and is also the right
lock to hold across the whole "read header, decide to insert, write
record, update header" sequence this bridge uses for POFILES/oowkhdr/
oowkdet appends.

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
_HEADER_BYTE = 0x7FFFFFFF


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
                win32file.LockFileEx(handle, flags, 0, 1, _overlapped(self._offset))
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
            win32file.UnlockFileEx(handle, 0, 1, _overlapped(self._offset))
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
    return _ByteRangeLock(fileobj, _HEADER_BYTE, timeout_s)
