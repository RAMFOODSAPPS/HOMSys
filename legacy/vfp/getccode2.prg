param mnum 
mtwo = substr(allt(str(mnum)),1,PUB_WHSIZE)
mretccode = 0

sele bmsvar
locate for varname = "GETCCODE1"
if .not. found()
   appe blank
   repl varname with "GETCCODE1", varvalue with "41,42,43,44,45,46,47,48,49,61,62,63,10,64,66,67,68,69,52,53,54,55,48,57"
   mgccode1 = alltr(varvalue)
else
   mgccode1 = alltr(varvalue)
endif
sele bmsvar
locate for varname = "GETCCODE2"
if .not. found()
   appe blank
   repl varname with "GETCCODE2", varvalue with "12,14,22,21,25,28,20,26,23,19,29,39,40,24"
   mgccode2 = alltr(varvalue)
else
   mgccode2 = alltr(varvalue)
endif

do case 
   case mtwo $ mgccode2 && "12,14,22,21,25,28,20,23"
      mretccode = 2
   case mtwo $ mgccode1 && "41,42,43,44,45,46,47,48,49,61,62,63,10"
      mretccode = 1
   otherwise
      mretccode = 3
endcase
return mretccode