"""Minimal Visual FoxPro DBF reader — Python port of
HOMSys.Infrastructure\\Data\\Dbf\\DbfReader.cs, byte-for-byte compatible so
RecNo and parsed values match what's already in SQL.

Every field is read as text (cp1252, stripped) regardless of its declared DBF
type, then parsed on demand by GetInt/GetDecimal/GetDate/GetBool — same as
the C# reader. Deliberate divergence: get_date() returns None for an invalid
calendar date (e.g. Feb 30) instead of raising, unlike the C# code's latent
crash there.

Production DBFs are frequently held open by BMS; opened read-only, which on
Windows allows concurrent readers/writers same as the C# FileShare.ReadWrite.
"""
from __future__ import annotations

import struct
from dataclasses import dataclass
from datetime import date


@dataclass
class DbfField:
    name: str
    type: str
    length: int
    decimals: int
    offset: int


class DbfRecord:
    __slots__ = ("buffer", "fields", "recno")

    def __init__(self, buffer: bytes, fields: list[DbfField], recno: int):
        self.buffer = buffer
        self.fields = fields
        self.recno = recno

    def _find(self, name: str) -> DbfField | None:
        for f in self.fields:
            if f.name.lower() == name.lower():
                return f
        return None

    def get_string(self, name: str) -> str:
        f = self._find(name)
        if f is None:
            return ""
        raw = self.buffer[f.offset:f.offset + f.length]
        return raw.decode("cp1252", errors="replace").strip()

    def get_int(self, name: str) -> int:
        raw = self.get_string(name)
        try:
            return int(raw)
        except ValueError:
            return 0

    def get_decimal(self, name: str) -> float:
        raw = self.get_string(name)
        try:
            return float(raw)
        except ValueError:
            return 0.0

    def get_decimal_or_null(self, name: str) -> float | None:
        raw = self.get_string(name)
        if raw == "":
            return None
        try:
            return float(raw)
        except ValueError:
            return None

    def get_date(self, name: str) -> str | None:
        """8-char YYYYMMDD -> ISO 'YYYY-MM-DD', or None if blank/invalid."""
        raw = self.get_string(name)
        if len(raw) != 8:
            return None
        try:
            y, m, d = int(raw[0:4]), int(raw[4:6]), int(raw[6:8])
        except ValueError:
            return None
        if not (1 <= y <= 9999 and 1 <= m <= 12 and 1 <= d <= 31):
            return None
        try:
            return date(y, m, d).isoformat()
        except ValueError:
            return None

    def get_bool(self, name: str) -> bool:
        raw = self.get_string(name)
        return len(raw) > 0 and raw[0] in "TtYy"


class DbfReader:
    def __init__(self, path: str):
        self.path = path
        self._f = open(path, "rb")

        header = self._read_exact(32)
        self.record_count = struct.unpack_from("<i", header, 4)[0]
        self.header_length = struct.unpack_from("<h", header, 8)[0]
        self.record_length = struct.unpack_from("<h", header, 10)[0]

        fields: list[DbfField] = []
        offset = 1  # byte 0 of each record is the deleted flag

        while self._f.tell() < self.header_length - 1:
            descriptor = self._read_exact(32)
            if descriptor[0] == 0x0D:  # header terminator
                break

            name = descriptor[0:11].decode("cp1252", errors="replace").rstrip("\x00").strip()
            if name == "":
                break

            length = descriptor[16]
            decimals = descriptor[17]
            fields.append(DbfField(name, chr(descriptor[11]), length, decimals, offset))
            offset += length

        self.fields = fields

    def records(self):
        """Non-deleted records, streamed."""
        for i in range(self.record_count):
            self._f.seek(self.header_length + i * self.record_length)
            buffer = self._f.read(self.record_length)
            if len(buffer) < self.record_length:
                return  # truncated tail

            if buffer[0] == 0x2A:  # deleted
                continue

            yield DbfRecord(buffer, self.fields, i + 1)

    def _read_exact(self, count: int) -> bytes:
        data = self._f.read(count)
        if len(data) < count:
            raise EOFError("Unexpected end of DBF header.")
        return data

    def close(self):
        self._f.close()

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, tb):
        self.close()
