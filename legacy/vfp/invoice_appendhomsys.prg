* Standalone .PRG, DO'd directly from Form1.drunbridge -- NOT pasted into
* a form method. Call `DO invoice_appendhomsys` right after the existing
* SalesOrderBridge.exe marker-poll completes (staging is done by then) and
* before falling through to open the form / rebuild the grid. The main
* body below runs first; everything under it is a set of local PROCEDUREs
* this file calls internally (DO/UDF), not separate form methods.
*
* Deployment: this file (or its compiled .FXP -- VFP auto-compiles a .PRG
* on first DO if no .FXP is present, or COMPILE it ahead of time) must sit
* in the same live BMS program folder as invoice.SCX itself, so a bare
* `DO invoice_appendhomsys` resolves it. Ship it through the same channel
* SalesOrderBridge.exe already uses (the BMS_auto_Distribution folder
* auto-pushed to all 25 branches) rather than a new manual push.
*
* Reads what SalesOrderBridge.exe stages under
* <bms-root>\homsys_queue\<sono>\stg_oowkhdr.dbf / stg_oowkdet.dbf /
* stg_pofiles.dbf and appends those rows into the real, live oowkhdr/
* oowkdet/pofiles tables via native APPEND BLANK + GATHER -- this keeps
* every open .CDX tag in sync automatically, the same way indexes stay
* correct during normal multi-branch BMS use. A raw Python byte-append
* never did this (SET ORDER TO docno would not show those rows).
*
* Supersedes the old ReindexSO.exe approach (retired): REINDEX needs
* exclusive table access, which is nearly impossible to get on
* oowkhdr/oowkdet while any of 25 branches has the table open SHARED --
* true whether REINDEX runs in a separate EXE or inline. APPEND BLANK +
* GATHER never has that problem; it's the same write path BMS itself uses.
*
* Once an order's three tables are all appended, this leaves the trace of
* that in the staged stg_oowkhdr.dbf's own APPENDED field -- it does NOT
* delete the queue folder. SalesOrderBridge.exe polls that field on its
* own next run (cheap: one tiny stage file, not a scan of the whole live
* oowkhdr table), confirms the order back to HOMSys, and deletes the queue
* folder itself once confirmed. This also makes a second Form1.Init/
* Refresh click before that happens a no-op instead of a duplicate append.
*
* Known limitation: if the running BMS session crashes between appending
* oowkhdr/oowkdet and setting APPENDED (rare -- this all runs in a few
* record writes), the next run re-appends all three tables from scratch,
* since nothing dedupes oowkhdr/oowkdet by DOCNO. Not handled here; flagged
* as an accepted risk rather than adding transactional rollback for it.

*======================================================================
* --- Form1.appendhomsysqueue (main body -- paste this whole file in) ---
* LPARAMETERS tnSoNo is OPTIONAL, same convention as Form1.drunbridge:
*   - omitted / 0     -> bulk download path (unchanged, below): append
*                        every staged HOMSys order found under homsys_queue.
*   - a real SO number -> single-order upload path: append just that one
*                        order, called locally (no DO...IN, no full-path
*                        resolution needed -- pAppendOrderBySo lives in this
*                        same file/program object).
*======================================================================
LPARAMETERS tnSoNo

LOCAL lcRoot, lcQueueRoot, laDirs[1], lnDirs, lnI, lcPriorErr

IF PCOUNT() > 0 AND tnSoNo > 0
    wait wind "BMS-HOMSYS - SYNC(1)..." NOWAIT
    DO pAppendOrderBySo WITH tnSoNo
    wait wind "BMS-HOMSYS - SYNC(OK)..." NOWAIT
    RETURN
ENDIF

* Derive the live data folder from the already-open oowkhdr table itself
* (\\Acastillano\setup\ADC\BMSRAM), never from SYS(5)+SYS(2003) -- that's
* this running program's own current directory (wherever invoice.SCX and
* this .prg live), a different folder from where the DBFs actually sit.
* drunbridge derives run_bridge.bat's data-folder arg the same way, from
* the same already-open alias -- this keeps both sides pointed at the
* same physical folder instead of this .prg silently missing/creating a
* homsys_queue under the program folder and never touching the real table.
IF NOT USED("oowkhdr")
    RETURN
ENDIF
lcRoot = ADDBS(JUSTPATH(DBF("oowkhdr")))
lcQueueRoot = lcRoot + "homsys_queue"

IF NOT DIRECTORY(lcQueueRoot)
    RETURN
ENDIF

lcPriorErr = ON("ERROR")
ON ERROR DO pAppendErr WITH lcPriorErr
wait wind "HOMSYS-BMS - SYNC(0)..." NOWAIT
lnDirs = ADIR(laDirs, ADDBS(lcQueueRoot) + "*.*", "D")
FOR lnI = 1 TO lnDirs
    wait wind "HOMSYS-BMS - SYNC(1)..." NOWAIT
    IF INLIST(laDirs[lnI, 1], ".", "..") OR NOT "D" $ laDirs[lnI, 5]
        LOOP
    ENDIF
    DO pAppendOrder WITH ADDBS(lcQueueRoot) + laDirs[lnI, 1], lcRoot
ENDFOR

* Post-deallocation HOMSys edits, staged by SalesOrderBridge.exe's own
* recover_resync() under a sibling folder (never homsys_queue -- these
* update an already-live oowkhdr/oowkdet row keyed on DOCNO, they never
* mint a new one). Same folder-per-SO#, ADIR/skip-dot-dirs shape as above.
LOCAL lcResyncRoot, laResyncDirs[1], lnResyncDirs, lnJ
lcResyncRoot = lcRoot + "homsys_resync"
IF DIRECTORY(lcResyncRoot)
    lnResyncDirs = ADIR(laResyncDirs, ADDBS(lcResyncRoot) + "*.*", "D")
    FOR lnJ = 1 TO lnResyncDirs
        wait wind "HOMSYS-BMS - RESYNC(1)..." NOWAIT
        IF INLIST(laResyncDirs[lnJ, 1], ".", "..") OR NOT "D" $ laResyncDirs[lnJ, 5]
            LOOP
        ENDIF
        DO pResyncOrder WITH ADDBS(lcResyncRoot) + laResyncDirs[lnJ, 1], lcRoot
    ENDFOR
ENDIF

wait wind "HOMSYS-BMS - SYNC(OK)..." NOWAIT
ON ERROR &lcPriorErr

RETURN

*======================================================================
* --- pAppendOrderBySo(tnSoNo) -- single-order variant, called from
* Form1.optn_init7's Forward-to-Invoice move button for the one S.O. the
* operator is moving, instead of looping every staged order like the main
* body above does. Queue folders are named by SO# (drunbridge's own
* run_bridge.bat/salesorder_bridge.py convention -- <bms-root>\homsys_queue\
* <so_no>\), so this is just pAppendOrder scoped to one known folder name.
*======================================================================
PROCEDURE pAppendOrderBySo
PARAMETERS tnSoNo

LOCAL lcRoot, lcOrderDir

IF NOT USED("oowkhdr")
    RETURN .F.
ENDIF
lcRoot = ADDBS(JUSTPATH(DBF("oowkhdr")))
lcOrderDir = ADDBS(lcRoot + "homsys_queue") + ALLTRIM(STR(tnSoNo))

IF NOT DIRECTORY(lcOrderDir)
    * Nothing staged for this SO -- either it isn't a HOMSys order at all,
    * or drunbridge hasn't pulled it yet. Caller decides what that means.
    RETURN .F.
ENDIF

LOCAL lcPriorErr
lcPriorErr = ON("ERROR")
ON ERROR DO pAppendErr WITH lcPriorErr

DO pAppendOrder WITH lcOrderDir, lcRoot

ON ERROR &lcPriorErr
RETURN .T.
ENDPROC

*======================================================================
* --- local: pAppendOrder(lcOrderDir, lcRoot) ---
*======================================================================
PROCEDURE pAppendOrder
PARAMETERS lcOrderDir, lcRoot

LOCAL lcStageHdr, llOk
lcStageHdr = ADDBS(lcOrderDir) + "stg_oowkhdr.dbf"

IF pAppendDone(lcStageHdr)
    * Already appended on a prior pass, just not yet confirmed/cleaned up
    * by SalesOrderBridge.exe -- nothing to do.
    RETURN
ENDIF

llOk = .T.
llOk = llOk AND pAppendTable(lcStageHdr, lcRoot + "oowkhdr.dbf", "")
llOk = llOk AND pAppendTable(ADDBS(lcOrderDir) + "stg_oowkdet.dbf", lcRoot + "oowkdet.dbf", "")
llOk = llOk AND pAppendTable(ADDBS(lcOrderDir) + "stg_pofiles.dbf", lcRoot + "pofiles.dbf", "PONUM")

IF llOk
    * Leave the trace of this order's assigned SO#/DOCNO having landed --
    * flip APPENDED on the staged header row itself. SalesOrderBridge.exe's
    * next run polls this field directly (cheap: one small stage file, not
    * a scan of the whole live oowkhdr table), confirms the order back to
    * HOMSys, then deletes this queue folder itself.
    IF USED("_hsdone")
        USE IN _hsdone
    ENDIF
    USE (lcStageHdr) SHARED ALIAS _hsdone IN 0
    REPLACE APPENDED WITH .T. IN _hsdone
    USE IN _hsdone
ENDIF
ENDPROC

*======================================================================
* --- local: pAppendDone(lcStageHdr) ---
*======================================================================
PROCEDURE pAppendDone
PARAMETERS lcStageHdr

IF NOT FILE(lcStageHdr)
    RETURN .F.
ENDIF

LOCAL llDone
IF USED("_hscheck")
    USE IN _hscheck
ENDIF
USE (lcStageHdr) SHARED ALIAS _hscheck IN 0
llDone = _hscheck.APPENDED
USE IN _hscheck
RETURN llDone
ENDPROC

*======================================================================
* --- local: pAppendTable(lcStageTable, lcRealTable, lcKeyField) ---
*======================================================================
PROCEDURE pAppendTable
PARAMETERS lcStageTable, lcRealTable, lcKeyField

IF NOT FILE(lcStageTable)
    RETURN .T.  && nothing staged for this table (e.g. order has no PO)
ENDIF

IF USED("_hsreal")
    USE IN _hsreal
ENDIF
SELECT 0
USE (lcRealTable) SHARED AGAIN ALIAS _hsreal

IF EMPTY(lcKeyField)
    * No dedupe needed (oowkhdr/oowkdet) -- APPEND FROM does exactly what a
    * manual SCAN+SCATTER+APPEND BLANK+GATHER loop would, natively, in one
    * command, with the same automatic index-tag maintenance.
    APPEND FROM (lcStageTable)
ELSE
    * Dedupe needed (pofiles, keyed on PONUM) -- APPEND FROM has no way to
    * check "does this key already exist in the target", so this table
    * alone still needs a per-row loop. Matches the old checkponum2
    * dedup-before-insert behavior: never duplicate an existing PONUM row.
    IF USED("_hsstg")
        USE IN _hsstg
    ENDIF
    SELECT 0
    USE (lcStageTable) SHARED ALIAS _hsstg

    SCAN
        SCATTER MEMVAR MEMO
        SELECT _hsreal
        LOCATE FOR ALLTRIM(&lcKeyField) == ALLTRIM(EVALUATE("m." + lcKeyField))
        IF NOT FOUND()
            APPEND BLANK
            GATHER MEMVAR MEMO
        ENDIF
        SELECT _hsstg
    ENDSCAN

    USE IN _hsstg
ENDIF

* Force the write (and its index-tag updates) fully out to disk now, rather
* than leaving it in this alias's write buffer -- other already-open
* aliases/work areas sharing this same physical table (e.g. whatever
* optn_init1's grid-build reads from) are only guaranteed to see committed
* disk state, not another area's pending buffer. This is the documented fix
* for "another session's write isn't visible without a reindex/reopen" on
* shared tables.
FLUSH

USE IN _hsreal
RETURN .T.
ENDPROC

*======================================================================
* --- local: pResyncOrder(lcOrderDir, lcRoot) ---
* Post-deallocation HOMSys edit write-back. Unlike pAppendOrder (which
* only ever APPEND BLANKs new rows), this SEEKs the already-live oowkhdr
* record by DOCNO and REPLACEs it field-by-field -- oowkhdr/oowkdet are
* NOT deleted+recreated, they're the same record BMS kept live since
* a1112.scx's deallocate() reset its STATUS back to "1"/Entered.
*======================================================================
PROCEDURE pResyncOrder
PARAMETERS lcOrderDir, lcRoot

LOCAL lcStageHdr, lcFailMarker, lcDocNo
lcStageHdr = ADDBS(lcOrderDir) + "stg_oowkhdr.dbf"
lcFailMarker = ADDBS(lcOrderDir) + "RESYNC_FAILED.txt"

IF pAppendDone(lcStageHdr) OR FILE(lcFailMarker)
    * Already REPLACEd on a prior pass, or already reported as not-found --
    * either way SalesOrderBridge.exe's recover_resync() picks this up and
    * cleans the folder up on its own next run; nothing more to do here.
    RETURN
ENDIF

IF USED("_rshdr")
    USE IN _rshdr
ENDIF
USE (lcStageHdr) SHARED ALIAS _rshdr IN 0
SELECT _rshdr
SCATTER MEMVAR MEMO
lcDocNo = m.DOCNO

IF USED("_rsreal")
    USE IN _rsreal
ENDIF
SELECT 0
USE (lcRoot + "oowkhdr.dbf") SHARED AGAIN ALIAS _rsreal
SET ORDER TO docno
SEEK lcDocNo

IF NOT FOUND()
    * Record vanished/renumbered since HOMSys queued this edit -- leave the
    * stage folder in place; recover_resync() reports this back to HOMSys
    * as "resync failed" instead of spinning forever on NeedsResync=true.
    USE IN _rsreal
    USE IN _rshdr
    STRTOFILE(TTOC(DATETIME()) + " DOCNO " + ALLTRIM(STR(lcDocNo)) + " not found in oowkhdr" + CHR(13) + CHR(10), ;
        lcFailMarker, .F.)
    RETURN
ENDIF

* Explicit field-by-field REPLACE -- never GATHER MEMO here. The stage
* table's own schema (OOWKHDR_RESYNC_FIELDS in dbf\stage.py) already
* excludes every BMS-owned column, but naming each field here again is a
* second, belt-and-braces boundary against ever clobbering STATUS/
* STATDESC/ALLOCATE/WITHVAT/etc. even if that stage schema ever grows.
REPLACE CUSTKEY WITH m.CUSTKEY, ;
    CUSNAME WITH m.CUSNAME, ;
    CKEY WITH m.CKEY, ;
    ORDERDATE WITH m.ORDERDATE, ;
    PONUM WITH m.PONUM, ;
    PODATE WITH m.PODATE, ;
    INVREM WITH m.INVREM, ;
    CCODE WITH m.CCODE, ;
    WHSENO WITH m.WHSENO, ;
    SHIPTOLN1 WITH m.SHIPTOLN1, ;
    SHIPTOLN2 WITH m.SHIPTOLN2, ;
    TERM WITH m.TERM, ;
    TERMDAYS WITH m.TERMDAYS, ;
    SALESMAN WITH m.SALESMAN, ;
    CSMAN WITH m.CSMAN, ;
    SERVEWH WITH m.SERVEWH, ;
    DELWHSE WITH m.DELWHSE, ;
    EXPECTDEL WITH m.EXPECTDEL, ;
    USERNAME WITH m.USERNAME
FLUSH
USE IN _rsreal

* Lines: delete every existing row for this DOCNO, then append the staged
* lines fresh -- mirrors HOMSys's own clear-and-rebuild-lines pattern in
* SalesOrderService.UpdateAsync, so both sides converge on "lines as of
* the last HOMSys save", not a field-by-field line diff.
IF USED("_rsdet")
    USE IN _rsdet
ENDIF
SELECT 0
USE (lcRoot + "oowkdet.dbf") SHARED AGAIN ALIAS _rsdet
SET ORDER TO docno
SEEK lcDocNo
SCAN WHILE DOCNO = lcDocNo
    DELETE
ENDSCAN

IF USED("_rsstgdet")
    USE IN _rsstgdet
ENDIF
SELECT 0
USE (ADDBS(lcOrderDir) + "stg_oowkdet.dbf") SHARED ALIAS _rsstgdet
SCAN
    SCATTER MEMVAR MEMO
    SELECT _rsdet
    APPEND BLANK
    GATHER MEMVAR MEMO
    SELECT _rsstgdet
ENDSCAN
USE IN _rsstgdet
FLUSH
USE IN _rsdet

* Success -- flip APPENDED on the staged header, same convention as
* pAppendOrder, so recover_resync() picks this up as confirmed and
* deletes this stage folder itself.
SELECT _rshdr
REPLACE APPENDED WITH .T.
USE IN _rshdr
ENDPROC

*======================================================================
* --- local: pAppendErr(lcPriorErr) ---
*======================================================================
* Runs synchronously inside the live BMS session -- never QUIT here.
* ON ERROR is a global setting, so it's always restored, even on failure.
PROCEDURE pAppendErr
PARAMETERS lcPriorErr

LOCAL lcLog, lcLogRoot
IF USED("oowkhdr")
    lcLogRoot = ADDBS(JUSTPATH(DBF("oowkhdr")))
ELSE
    lcLogRoot = ADDBS(SYS(5) + SYS(2003))
ENDIF
lcLog = lcLogRoot + "homsys_queue\append_errors.txt"
STRTOFILE(TTOC(DATETIME()) + " ERR " + ALLTRIM(STR(LINENO())) + ": " + MESSAGE() + CHR(13) + CHR(10), ;
    lcLog, FILE(lcLog))
    
IF USED("_hsreal")
    USE IN _hsreal
ENDIF
IF USED("_hsstg")
    USE IN _hsstg
ENDIF
IF USED("_hsdone")
    USE IN _hsdone
ENDIF
IF USED("_hscheck")
    USE IN _hscheck
ENDIF
IF USED("_rshdr")
    USE IN _rshdr
ENDIF
IF USED("_rsreal")
    USE IN _rsreal
ENDIF
IF USED("_rsdet")
    USE IN _rsdet
ENDIF
IF USED("_rsstgdet")
    USE IN _rsstgdet
ENDIF

ON ERROR &lcPriorErr
RETURN
ENDPROC
