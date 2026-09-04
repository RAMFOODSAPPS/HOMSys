"""Minimal reader for Visual FoxPro DBF files — Python port of
HOMSys.Infrastructure/Data/Dbf/DbfReader.cs. Same scope: C/N/D/L field
types only (no memo — none of the bridge's target tables have an .FPT).

Fields are located by name at runtime rather than hardcoded offsets: the
live header is the only source of truth for a layout like oowkhdr's 139
fields, which isn't reproduced anywhere in this repo.

Production DBFs are frequently held open by BMS, so files are opened with
plain "r+b" (Windows default share mode already allows other readers/
writers of an already-open file; a Foxpro-side exclusive USE would be the
only thing that blocks us, same as it would block another VFP session).
"""
from __future__ import annotations

import struct
from dataclasses import dataclass
from datetime import date

ENCODING = "cp1252"


@dataclass(frozen=True)
class DbfField:
    name: str
    type: str
    length: int
    decimals: int
    offset: int  # byte offset within a record; byte 0 of a record is the deletion flag


def _read_exact(fh, n: int) -> bytes:
    buf = fh.read(n)
    if len(buf) != n:
        raise EOFError("Unexpected end of DBF header")
    return buf


class DbfHeader:
    """Parses the 32-byte file header + field descriptor table."""

    def __init__(self, fh):
        header = _read_exact(fh, 32)
        self.record_count = struct.unpack_from("<i", header, 4)[0]
        self.header_length = struct.unpack_from("<h", header, 8)[0]
        self.record_length = struct.unpack_from("<h", header, 10)[0]

        fields = []
        offset = 1
        while fh.tell() < self.header_length - 1:
            descriptor = _read_exact(fh, 32)
            if descriptor[0] == 0x0D:
                break
            name = descriptor[0:11].decode(ENCODING).rstrip("\x00").strip()
            if not name:
                break
            length = descriptor[16]
            decimals = descriptor[17]
            fields.append(DbfField(name, chr(descriptor[11]), length, decimals, offset))
            offset += length
        self.fields = fields

    def field(self, name: str) -> DbfField:
        for f in self.fields:
            if f.name.lower() == name.lower():
                return f
        raise KeyError(f"No such field in DBF: {name}")

    def has_field(self, name: str) -> bool:
        return any(f.name.lower() == name.lower() for f in self.fields)


def decode_record(buf: bytes, fields: list[DbfField]) -> dict:
    return {f.name: buf[f.offset:f.offset + f.length].decode(ENCODING, errors="replace") for f in fields}


def get_string(row: dict, name: str) -> str:
    return row.get(name, "").strip()


def get_int(row: dict, name: str) -> int:
    raw = get_string(row, name)
    try:
        return int(raw)
    except ValueError:
        return 0


def get_decimal(row: dict, name: str) -> float:
    raw = get_string(row, name)
    try:
        return float(raw)
    except ValueError:
        return 0.0


def get_date(row: dict, name: str) -> date | None:
    raw = get_string(row, name)
    if len(raw) != 8:
        return None
    try:
        return date(int(raw[0:4]), int(raw[4:6]), int(raw[6:8]))
    except ValueError:
        return None


def get_bool(row: dict, name: str) -> bool:
    raw = get_string(row, name)
    return len(raw) > 0 and raw[0] in "TtYy"


class DbfTable:
    """Read-only iteration over a DBF's non-deleted records. For writes, open
    the same path with dbf.writer.DbfWriter instead (separate file handle,
    since writes need their own lock/seek discipline).
    """

    def __init__(self, path: str):
        self.path = path
        self._fh = open(path, "rb")
        self.header = DbfHeader(self._fh)

    @property
    def fields(self) -> list[DbfField]:
        return self.header.fields

    def record_at(self, recno: int) -> dict | None:
        """1-based record number. Returns None for a deleted or out-of-range row."""
        if recno < 1 or recno > self.header.record_count:
            return None
        self._fh.seek(self.header.header_length + (recno - 1) * self.header.record_length)
        buf = self._fh.read(self.header.record_length)
        if len(buf) != self.header.record_length or buf[0:1] == b"*":
            return None
        return decode_record(buf, self.header.fields)

    def records(self):
        """Yields (recno, row) for every non-deleted record, 1-based recno."""
        for recno in range(1, self.header.record_count + 1):
            row = self.record_at(recno)
            if row is not None:
                yield recno, row

    def find(self, field: str, value: str):
        """First non-deleted record whose field equals value (VFP-style: trimmed,
        case-sensitive). Returns (recno, row) or None. Linear scan — fine for the
        bridge's low per-run volume, not meant for hot lookups on huge tables.
        """
        for recno, row in self.records():
            if get_string(row, field) == value:
                return recno, row
        return None

    def close(self):
        self._fh.close()

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        self.close()
