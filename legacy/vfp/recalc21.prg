* recalc21.prg
* recalculate ending balance of inventory file (temporary file) - transient stocks
* up to specified date
* param is file and target date
param xeif,xdate

closeprod = openfile("prod4win")
closeinact = openfile("inactive")
closesysparam = openfile("sysparam")

closeprod=.f.
closehdr=.f.
closedet=.f.

sele sysparam
paramwhseno=val(bcode)
paramwhnumbers = trim(whnumbers)

IMHDR = GETFNAME(XDATE,"TH")
IMDET = GETFNAME(XDATE,"TD")
if file("cumpath.mem")
   rest from cumpath additive
   mhdr2 = getfname(xdate,"TH")
   imhdr = withpath(mhdr2,dosdir)
   mhdr2 = getfname(xdate,"TD")
   imdet = withpath(mhdr2,dosdir)
endif   


closehdr = openfile("TSHDR")
closedet = openfile("TSDET")

TEMPHDR = NEWNAME("HDR")
TEMPDET = NEWNAME("DET")

if file("&imhdr") .and. file("&imdet")
    * use renamed imtr file if they exist otherwise use current month file
   SELE TSHDR
   COPY TO &TEMPHDR STRU
   SELE TSDET
   COPY TO &TEMPDET STRU
   USE &TEMPHDR ALIAS TEMPHDR IN 0 EXCLUSIVE
   USE &TEMPDET ALIAS TEMPDET IN 0 EXCLUSIVE
   IF FILE("&IMHDR")
      SELE TEMPHDR
      APPEND FROM &IMHDR
   ENDIF
   IF FILE("&IMDET")
      SELE TEMPDET
      APPEND FROM &IMDET
   ENDIF
else    
   * CURRENT MONTH
   SELE TSHDR
   COPY TO &TEMPHDR FOR POSTED<= XDATE .AND. .NOT. EMPTY(POSTED)
   SELE TSDET
   COPY TO  &TEMPDET 
   USE &TEMPHDR ALIAS TEMPHDR IN 0 EXCLUSIVE
   USE &TEMPDET ALIAS TEMPDET IN 0 EXCLUSIVE
ENDIF

sele prod4win
copy to &temppr_recalc21
use &temppr_recalc21 alias temppr_recalc21 in 0 excl
sele temppr_recalc21
appe from inactive
index on cprodno tag cprodno
set order to cprodno

idx311 = newname("311")

sele (xeif)    
REPL ALL TOHCS WITH BEGCS,INCS WITH 0,OUTCS WITH 0
if type("TOHPC") <> "U"
   REPL ALL TOHPC WITH BEGPC,INPC WITH 0,OUTPC WITH 0
endif   
MIDXDET = NEWNAME("XD1")    
MIDXHDR = NEWNAME("XH1")
    
sele TEMPDET
INDEX ON DOCTYPE+STR(DOCNO,8) TO  &MIDXDET
*set order to doctype
set rela to cprodno into temppr_recalc21

sele TEMPHDR
INDEX ON DOCTYPE+STR(DOCNO,8) TO &MIDXHDR
set filter to posted <= xdate .and. .not. empty(posted)
MREC=RECCOUNT()
*browse
go top
do while .not. eof()
    MREC = MREC-1
    wait window "Recalculating Inventory "+" posted "+dtoc(posted)+' RECS '+STR(MREC,6) nowait

*    mctr=mctr+1
    mpostdate=posted
    mdoctype=doctype
    mdocno=docno
    mkey=mdoctype+str(mdocno,8)
    ************************
    servingwhseno=SERVEWH    
    if servingwhseno=0 .or. str(servingwhseno,2) $ paramwhnumbers   &&=paramwhseno
       oktocompute=.t.
    else
       oktocompute=.f.
    endif
    ************************
    
    sele TEMPDET
    seek mkey
    do while doctype=mdoctype .and. docno=mdocno .and. .not. eof() .and. oktocompute
*       wait window "Recalculating Inventory for prodno "+cprodno+" posted "+dtoc(mpostdate) nowait
       mqtypc=0
       mqtycs = qtycs       
       if type("qtypc")<>"U"
          mqtypc=qtypc
       endif
       mkey = cprodno+str(class,1)
       msupplier=temppr_recalc21.supplier
       mcprodno=cprodno
       MMclass=class        && class in for rsta
       mpieces=temppr_recalc21.pieces
       muc=uc
       sele (xeif)
       loca for cprodno=mcprodno .and. class=MMclass
       if .not. found()
          append blank
          repl cprodno with mcprodno,class with MMclass,supplier with msupplier
       endif
       IF SUBSTR(MDOCTYPE,1,3)="ICR"
          REPL INCS WITH INCS + MQTYCS,UC WITH MUC
          if type("TOHPC")<>"U"
             repl inpc with inpc + mqtypc
          endif
       ELSE
          REPL OUTCS WITH OUTCS + MQTYCS
          if type("TOHPC")<>"U"
             repl outpc with outpc + mqtypc
          endif

       ENDIF
       sele TEMPDET
       skip
    enddo
    sele TEMPHDR
    skip
 enddo
sele (xeif)
REPL ALL TOHCS WITH BEGCS + INCS - OUTCS
if type("TOHPC")<>"U"
   repl all tohpc with begpc + inpc - outpc
endif

*browse
*wait window "total records press a key processed= "+str(mctr,6) +"press a key"
M=CLOSETAB("PROD4WIN",closeprod)
M=CLOSETAB("inactive",closeinact)
M=CLOSETAB("TSHDR",CLOSEHDR)
M=CLOSETAB("TSDET",CLOSEDET)
M=CLOSETAB("SYSPARAM",CLOSESYSPARAM)

M=DELFILE("TEMPHDR")
M=DELFILE("TEMPDET")

sele (xeif)
set index to
erase &idx311
sele temppr_recalc21
use 
erase &temppr_recalc21