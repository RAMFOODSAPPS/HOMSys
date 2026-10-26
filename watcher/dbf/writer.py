"""Record append + in-place field update for VFP6 DBFs, built on top of
dbf.reader's header/field parsing. Pair every call here with the matching
dbf.locking context manager — this module does no locking itself.

Only C / N / D / L fields are supported for writing (same scope as
dbf.reader / DbfReader.cs — none of the bridge's target tables use I or
memo fields).
"""
from __future__ import annotations

import struct
from datetime import date

from .reader import DbfHeader, DbfField, ENCODING, decode_record, get_string


def _encode_value(field: DbfField, value) -> bytes:
    if field.type == "C":
        s = "" if value is None else str(value)
        raw = s.encode(ENCODING, errors="replace")[: field.length]
        return raw.ljust(field.length, b" ")

    if field.type in ("N", "F"):
        if value is None:
            return b" " * field.length
        if field.decimals > 0:
            s = f"{float(value):.{field.decimals}f}"
        else:
            s = str(int(value))
        raw = s.encode("ascii")[: field.length]
        return raw.rjust(field.length, b" ")

    if field.type == "D":
        if value is None:
            return b" " * 8
        if isinstance(value, str):
            return value.encode("ascii").ljust(8, b" ")[:8]
        return value.strftime("%Y%m%d").encode("ascii")

    if field.type == "L":
        # VFP writes "?" only for a truly never-set logical (e.g. a field added
        # to the table after this record existed). A freshly appended record
        # has every field set by definition, so an unmapped logical defaults
        # to False, matching how a11102's cmdSave.Click leaves flags it never
        # touches (e.g. EXPECTDEL) at their blank/false default — not unknown.
        if value is None:
            return b"F"
        return b"T" if value else b"F"

    if field.type == "T":
        if value is None:
            return b"\x00" * 8  # VFP datetime "no value" is all-zero, not blank/space
        raise ValueError(f"Writing a non-null T (datetime) value isn't implemented ({field.name})")

    raise ValueError(f"Unsupported field type for write: {field.type} ({field.name})")


def create_dbf(path: str, fields: list[DbfField]) -> None:
    """Writes a fresh, zero-record DBF (header + field descriptors +
    terminator + EOF byte) for an arbitrary field list. Used to build local
    scratch tables shaped like a subset of a live table's columns — no VFP
    dependency, this is pure struct-packing.
    """
    header_length = 32 + 32 * len(fields) + 1
    record_length = 1 + sum(f.length for f in fields)

    today = date.today()
    header = bytearray(32)
    header[0] = 0x03  # plain DBF, no memo
    header[1] = today.year - 2000
    header[2] = today.month
    header[3] = today.day
    struct.pack_into("<i", header, 4, 0)  # record count
    struct.pack_into("<h", header, 8, header_length)
    struct.pack_into("<h", header, 10, record_length)

    with open(path, "wb") as fh:
        fh.write(header)
        offset = 1
        for f in fields:
            descriptor = bytearray(32)
            name_bytes = f.name.encode(ENCODING)[:10]
            descriptor[0:len(name_bytes)] = name_bytes
            descriptor[11] = ord(f.type)
            descriptor[16] = f.length
            descriptor[17] = f.decimals
            fh.write(descriptor)
            offset += f.length
        fh.write(b"\x0d")  # field descriptor terminator
        fh.write(b"\x1a")  # end-of-file marker


class DbfWriter:
    """Opens the same path as dbf.reader.DbfTable, but read-write, for
    appends and in-place field updates. Caller is responsible for holding
    the appropriate dbf.locking lock around each operation.
    """

    def __init__(self, path: str):
        self.path = path
        self._fh = open(path, "r+b")
        self.header = DbfHeader(self._fh)

    @property
    def fileobj(self):
        return self._fh

    def _reread_record_count(self) -> int:
        self._fh.seek(4)
        (count,) = struct.unpack("<i", self._fh.read(4))
        self.header.record_count = count
        return count

    def append_record(self, values: dict) -> int:
        """Writes a new record built from values (field name -> python value,
        unlisted fields are written blank) and bumps RECCOUNT() in the header.
        Caller must hold dbf.locking.lock_header for the duration. Returns the
        new 1-based RECNO().
        """
        record_count = self._reread_record_count()
        new_recno = record_count + 1

        buf = bytearray(self.header.record_length)
        buf[0:1] = b" "  # not deleted
        for f in self.header.fields:
            raw = _encode_value(f, values.get(f.name))
            buf[f.offset:f.offset + f.length] = raw

        self._fh.seek(self.header.header_length + (new_recno - 1) * self.header.record_length)
        self._fh.write(bytes(buf))

        self._fh.seek(4)
        self._fh.write(struct.pack("<i", new_recno))

        today = date.today()
        self._fh.seek(1)
        self._fh.write(bytes([today.year - 2000, today.month, today.day]))

        self._fh.flush()
        self.header.record_count = new_recno
        return new_recno

    def record_at(self, recno: int) -> dict | None:
        """Same semantics as DbfTable.record_at, but off this writer's own
        handle so a find-then-lock-then-update sequence stays on one fd.
        """
        if recno < 1 or recno > self.header.record_count:
            return None
        self._fh.seek(self.header.header_length + (recno - 1) * self.header.record_length)
        buf = self._fh.read(self.header.record_length)
        if len(buf) != self.header.record_length or buf[0:1] == b"*":
            return None
        return decode_record(buf, self.header.fields)

    def find(self, field: str, value: str):
        """First non-deleted record whose field trims-equal to value. Returns
        (recno, row) or None. Linear scan — fine for docnum.dbf-sized tables
        (a handful of doctype rows), not meant for big tables like oowkhdr.
        """
        for recno in range(1, self.header.record_count + 1):
            row = self.record_at(recno)
            if row is not None and get_string(row, field) == value:
                return recno, row
        return None

    def update_field(self, recno: int, field_name: str, value) -> None:
        """In-place field update on an existing record. Caller must hold
        dbf.locking.lock_record(recno) for the duration.
        """
        f = self.header.field(field_name)
        raw = _encode_value(f, value)
        self._fh.seek(self.header.header_length + (recno - 1) * self.header.record_length + f.offset)
        self._fh.write(raw)
        self._fh.flush()

    def read_field_raw(self, recno: int, field_name: str) -> str:
        f = self.header.field(field_name)
        self._fh.seek(self.header.header_length + (recno - 1) * self.header.record_length + f.offset)
        return self._fh.read(f.length).decode(ENCODING, errors="replace").strip()

    def close(self):
        self._fh.close()

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        self.close()
