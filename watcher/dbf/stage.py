"""Builds scratch DBFs shaped like a subset of the live oowkhdr/oowkdet/
pofiles columns, under a persistent per-order queue folder
(<dbf_root>\\homsys_queue\\<so_no>\\) that salesorder_bridge.write_order()
stages into (no locking needed, private folder) and invoice.SCX's own
thisform.appendhomsysqueue method later reads and appends into the real
tables via native APPEND BLANK + GATHER, which keeps every open .CDX tag
in sync -- see watcher\\invoice_scx_appendhomsys.prg and the plan's
addendum on why raw Python byte-appends were left in place of that.

Field types/lengths are read from each live table's own header (DbfTable),
never hardcoded, so a scratch column always matches its real counterpart.
"""
from __future__ import annotations

import os

from .reader import DbfField, DbfTable
from .writer import create_dbf

# Flipped by invoice.SCX's own append method once it has appended this
# order into the live tables. salesorder_bridge.py polls this field on
# stg_oowkhdr.dbf directly instead of scanning the whole (large, shared)
# live oowkhdr table every run.
APPENDED_FIELD = "APPENDED"

# HOMSys-side field maps this bridge writes to, restated here (not imported
# from salesorder_bridge -- that module imports this one) so stage.py has no
# circular dependency; keep in sync with salesorder_bridge.py's own maps.
OOWKHDR_FIELDS = [
    "DOCNO", "CUSTKEY", "CUSNAME", "CKEY", "ORDERDATE", "PONUM", "PODATE",
    "INVREM", "CCODE", "WHSENO", "SHIPTOLN1", "SHIPTOLN2", "TERM", "TERMDAYS",
    "SALESMAN", "CSMAN", "SERVEWH", "DELWHSE",
    "STATUS", "STATDESC", "ALLOCATE", "WITHVAT", "EXPECTDEL", "USERNAME",
]
OOWKDET_FIELDS = [
    "DOCNO", "PRODNO", "CPRODNO", "PRODDESC", "PACKSIZE", "QTYCS", "QTYPC",
    "PIECES", "UM", "SUPPLIER", "CSUPPLIER", "PRICELIST", "TAXRATE",
]
POFILES_FIELDS = [
    "PONUM", "PODATE", "SONO", "ORDERDATE", "CUSTKEY", "CUSNAME",
    "SYSDATE", "TRANSDATE",
]

# ENCODE-OWNED subset of OOWKHDR_FIELDS only -- used for a post-deallocation
# resync (see stage_resync_order in salesorder_bridge.py). STATUS/STATDESC/
# ALLOCATE are BMS-owned workflow state and must never be staged here: this
# list is the actual safety boundary the VFP side relies on (it REPLACEs
# field-by-field from whatever the stage table carries, so a BMS-owned
# column simply never being present is what keeps pResyncOrder from being
# able to clobber it, even by mistake).
OOWKHDR_RESYNC_FIELDS = [
    "DOCNO", "CUSTKEY", "CUSNAME", "CKEY", "ORDERDATE", "PONUM", "PODATE",
    "INVREM", "CCODE", "WHSENO", "SHIPTOLN1", "SHIPTOLN2", "TERM", "TERMDAYS",
    "SALESMAN", "CSMAN", "SERVEWH", "DELWHSE", "EXPECTDEL", "USERNAME",
]

# (live table filename, scratch table name, fields to carry over)
_TABLES = [
    ("oowkhdr.dbf", "stg_oowkhdr", OOWKHDR_FIELDS),
    ("oowkdet.dbf", "stg_oowkdet", OOWKDET_FIELDS),
    ("pofiles.dbf", "stg_pofiles", POFILES_FIELDS),
]

# Resync stages only the header (ENCODE-OWNED subset) + detail lines --
# no pofiles, since a resync never mints a new PO record.
_RESYNC_TABLES = [
    ("oowkhdr.dbf", "stg_oowkhdr", OOWKHDR_RESYNC_FIELDS),
    ("oowkdet.dbf", "stg_oowkdet", OOWKDET_FIELDS),
]


def build_resync_stage_tables(cfg, stage_dir: str) -> dict:
    """Like build_stage_tables, but for a post-deallocation resync: stages
    only the ENCODE-OWNED header fields (OOWKHDR_RESYNC_FIELDS) plus detail
    lines, no pofiles. Returns {live_filename: stage_path}.
    """
    os.makedirs(stage_dir, exist_ok=True)
    paths = {}
    for live_name, stage_name, wanted_fields in _RESYNC_TABLES:
        with DbfTable(cfg.path(live_name)) as t:
            fields = [t.header.field(name) for name in wanted_fields]
        if stage_name == "stg_oowkhdr":
            offset = sum(f.length for f in fields)
            fields = fields + [DbfField(APPENDED_FIELD, "L", 1, 0, offset)]
        stage_path = os.path.join(stage_dir, stage_name + ".dbf")
        create_dbf(stage_path, fields)
        paths[live_name] = stage_path
    return paths


def build_stage_tables(cfg, stage_dir: str) -> dict:
    """Creates stg_oowkhdr.dbf / stg_oowkdet.dbf / stg_pofiles.dbf in
    stage_dir, field-for-field matching the subset of the live tables this
    bridge writes. Returns {live_filename: stage_path}.
    """
    os.makedirs(stage_dir, exist_ok=True)
    paths = {}
    for live_name, stage_name, wanted_fields in _TABLES:
        with DbfTable(cfg.path(live_name)) as t:
            fields = [t.header.field(name) for name in wanted_fields]
        if stage_name == "stg_oowkhdr":
            offset = sum(f.length for f in fields)
            fields = fields + [DbfField(APPENDED_FIELD, "L", 1, 0, offset)]
        stage_path = os.path.join(stage_dir, stage_name + ".dbf")
        create_dbf(stage_path, fields)
        paths[live_name] = stage_path
    return paths
