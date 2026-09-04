"""Python port of docnum.prg's GETSAVE action — the shared spin-locked
document-number counter every BMS module (including this bridge) must go
through to claim a real SO number. Exact source logic being mirrored:

    case upper(maction) = "GETSAVE"
       loca for upper(doctype)=upper(xdoctype)
       if .not. found()
          append blank
          repl doctype with upper(xdoctype)
       endif
       do while .t.
          mlock = rlock()
          if mlock
             exit
          endif
       enddo
       REPL LASTNUM WITH LASTNUM+1
       MRETVAL=LASTNUM
       UNLOCK

Note the append-blank-if-missing step runs unlocked in the original VFP
code too (a pre-existing race in legacy BMS, not something this port
introduces) — "SO" is expected to already have a row in production, so
this path is a safety net, not the common case.
"""
from __future__ import annotations

from .locking import lock_record
from .writer import DbfWriter


def claim_number(docnum_path: str, doctype: str, timeout_s: float = 10.0) -> int:
    """Returns the freshly-incremented LASTNUM for doctype, having just
    reserved it under docnum.dbf's own record lock. Caller must persist
    this number (ledger) before doing anything else — it is now spent.
    """
    doctype = doctype.upper()
    with DbfWriter(docnum_path) as w:
        found = w.find("DOCTYPE", doctype)
        if found is None:
            recno = w.append_record({"DOCTYPE": doctype, "LASTNUM": 0})
        else:
            recno, _ = found

        with lock_record(w.fileobj, recno, timeout_s):
            current = int(w.read_field_raw(recno, "LASTNUM") or "0")
            new_value = current + 1
            w.update_field(recno, "LASTNUM", new_value)

        return new_value
