* homsysbridge.prg
* Shared helper functions for Form1.refreshfromhomsys() in invoice.SCX.
* SQLEXEC() against HOMSysDb returns date/datetime columns as ISO character
* strings (not VFP D/T values) and NULL columns as .NULL. — DBF REPLACE
* rejects both directly, so every pulled column must go through one of these.

FUNCTION IsoToDate
LPARAMETERS tcStr
LOCAL lcStr
IF ISNULL(tcStr)
	RETURN {}
ENDIF
lcStr = ALLTRIM(tcStr)
IF EMPTY(lcStr)
	RETURN {}
ENDIF
RETURN DATE(VAL(SUBSTR(lcStr,1,4)), VAL(SUBSTR(lcStr,6,2)), VAL(SUBSTR(lcStr,9,2)))
ENDFUNC

FUNCTION IsoToDateTime
LPARAMETERS tcStr
LOCAL lcStr
IF ISNULL(tcStr)
	RETURN {}
ENDIF
lcStr = ALLTRIM(tcStr)
IF EMPTY(lcStr)
	RETURN {}
ENDIF
RETURN DATETIME(VAL(SUBSTR(lcStr,1,4)), VAL(SUBSTR(lcStr,6,2)), VAL(SUBSTR(lcStr,9,2)), ;
	VAL(SUBSTR(lcStr,12,2)), VAL(SUBSTR(lcStr,15,2)), VAL(SUBSTR(lcStr,18,2)))
ENDFUNC

FUNCTION NvlC
LPARAMETERS tuVal, tcDefault
IF ISNULL(tuVal)
	RETURN tcDefault
ENDIF
RETURN tuVal
ENDFUNC

FUNCTION NvlN
LPARAMETERS tuVal, tnDefault
IF ISNULL(tuVal)
	RETURN tnDefault
ENDIF
RETURN tuVal
ENDFUNC

FUNCTION NvlL
LPARAMETERS tuVal, tlDefault
IF ISNULL(tuVal)
	RETURN tlDefault
ENDIF
RETURN tuVal
ENDFUNC
