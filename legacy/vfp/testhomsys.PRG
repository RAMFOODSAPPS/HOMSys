LPARAMETERS tnSoNo

LOCAL llSingleSo
llSingleSo = (tnSoNo > 0)

IF llSingleSo
	DO pAppendOrderBySo IN invoice_appendhomsys.prg WITH tnSoNo
ENDIF

lcBridgeBat = ADDBS(JUSTPATH(SYS(16))) + "run_bridge.bat"
IF FILE(lcBridgeBat)
	IF llSingleSo
		lcMarker = SYS(2023) + "\hsb-" + ALLTRIM(STR(tnSoNo)) + "-" + SYS(3) + ".done"
	ELSE
		lcMarker = SYS(2023) + "\hsb-" + SYS(3) + ".done"
	ENDIF
	IF FILE(lcMarker)
        ERASE (lcMarker)
    ENDIF
	sele oowkhdr
	moowkpath = dbf()
	lcDir = addbs(justpath(moowkpath))
	IF RIGHT(lcDir, 1) = "\"
	    lcDir = LEFT(lcDir, LEN(lcDir) - 1)
    ENDIF

	* 4th arg only present for the single-SO case -- run_bridge.bat forwards
	* it to SalesOrderBridge.exe, which (per salesorder_bridge.py's
	* recover_one) confirms just this SO back to HOMSys instead of running
	* its normal bulk recover()/fetch_pending pass.
	IF llSingleSo
		lcCmd = '"' + lcBridgeBat + '" "' + lcMarker + '" "' + lcDir + '" "' + ALLTRIM(STR(tnSoNo)) + '"'
	ELSE
		lcCmd = '"' + lcBridgeBat + '" "' + lcMarker + '" "' + lcDir + '"'
	ENDIF

	RUN /N7 &lcCmd

	lnWait = 0

	DO WHILE NOT FILE(lcMarker) AND lnWait < 15
		=INKEY(1)
		lnWait = lnWait + 1
		IF llSingleSo
			wait wind "Syncing SO "+ALLTRIM(STR(tnSoNo))+" with HOMSys ("+transform(lnWait)+")... PLEASE WAIT..." nowait
		ELSE
			wait wind "Downloading from HOMSys ("+transform(lnWait)+")... PLEASE WAIT..." nowait
		ENDIF
	ENDDO
	if .not. file(lcMarker)
	   wait wind "No SO to sync. Download Complete..." nowait
	else
	   wait wind "Download Complete..." nowait
	endif
	IF FILE(lcMarker)
        ERASE (lcMarker)
    ENDIF

ENDIF

IF NOT llSingleSo
	do invoice_appendhomsys.prg
ENDIF