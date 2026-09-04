* recalc22.prg
* recalculate van ending inventory
* up to specified date
* param is file and target date
param xeif,xdate


closeprod=.f.
closeinact=.f.
closehdr=.f.
closedet=.f.
if .not. used("prod4win")
   use prod4win in 0 shared
   closeprod=.t.
endif
if .not. used("inactive")
   use inactive in 0 shared
   closeinact=.t.
endif
CLOSESYSPARAM=.F.
IF .NOT. USED("SYSPARAM")
   USE SYSPARAM IN 0 SHARED
   CLOSESYSPARAM=.T.
ENDIF   


IMHDR = GETFNAME(XDATE,"IH")
IMDET = GETFNAME(XDATE,"ID")

if .not. used("imtr_hdr")
   use imtr_hdr in 0 shared
   closehdr=.t.
endif   
if .not. used("imtr_det")
   use imtr_det in 0 shared
   closedet=.t.
endif   

TEMPHDR = NEWNAME("HDR")
TEMPDET = NEWNAME("DET")

if file("&imhdr") .and. file("&imdet")
    * use renamed imtr file if they exist otherwise use current month file
   SELE IMTR_HDR
   COPY TO &TEMPHDR STRU
   SELE IMTR_DET
   COPY TO &TEMPDET STRU
   USE &TEMPHDR ALIAS TEMPHDR IN 0 EXCLUSIVE
   USE &TEMPDET ALIAS TEMPDET IN 0 EXCLUSIVE
   IF FILE("&IMHDR")
      SELE TEMPHDR
      APPEND FROM &IMHDR for doctype="STAI" .or. doctype="STAO"
   ENDIF
   IF FILE("&IMDET")
      SELE TEMPDET
      APPEND FROM &IMDET for SUBSTR(doctype,1,4) $ "STAI,STAO" .AND. .NOT. EMPTY(CPRODNO)
   ENDIF
else    
   * CURRENT MONTH
   SELE IMTR_HDR
   COPY TO &TEMPHDR FOR POSTED<= XDATE .AND. .NOT. EMPTY(POSTED) .and. substr(doctype,1,4) $ "STAI,STAO"
   SELE IMTR_DET
   COPY TO  &TEMPDET for substr(doctype,1,4) $ "STAI,STAO" .AND. .NOT. EMPTY(CPRODNO)
   USE &TEMPHDR ALIAS TEMPHDR IN 0 EXCLUSIVE
   USE &TEMPDET ALIAS TEMPDET IN 0 EXCLUSIVE
ENDIF


temppr_recalc22 = newname("dbf")
sele prod4win
copy to &temppr_recalc22 
use &temppr_recalc22 alias temppr_recalc22 in 0 excl
sele temppr_recalc22
appe from inactive
index on cprodno tag cprodno
set order to cprodno
idx311 = newname("311")

sele (xeif)    
repl all qtycs with begcs,qtypc with begpc,tohcs with begcs,tohpc with begpc
*BROWSE FIELDS CPRODNO,QTYCS,QTYPC
    
MIDXDET = NEWNAME("XD1")    
MIDXHDR = NEWNAME("XH1")
    
sele TEMPDET
DELETE FOR EMPTY(CPRODNO)

INDEX ON DOCTYPE+STR(DOCNO,8) TO  &MIDXDET
*set order to doctype
set rela to cprodno into temppr_recalc22

sele TEMPHDR
INDEX ON csman+DOCTYPE+STR(DOCNO,8) TO &MIDXHDR
set filter to posted <= xdate .and. .not. empty(posted)
count to  mrec
mctr=0
go top
do while .not. eof()
    mrec=mrec-1
    mctr=mctr+1
    mpostdate=posted
    mdoctype=doctype
    mdocno=docno
    mkey=mdoctype+str(mdocno,8)
    mcsman = csman
    sele TEMPDET
    seek mkey
    do while doctype=mdoctype .and. docno=mdocno .and. .not. eof()
       wait window "Recalculating Inventory for prodno "+cprodno+" posted "+dtoc(mpostdate)+" recs "+str(mrec,6) nowait
       mqtycs = qtycs
       mqtypc = qtypc
       mqq = qtycs
       mpp=qtypc
       
       mkey = cprodno+str(class,1)
       msupplier=temppr_recalc22.supplier
       mcprodno=cprodno
       mclass=class        && class in for rsta
       mclassout=classout
       mpieces=temppr_recalc22.pieces
       muc=uc
       mqq = qtycs
       mqp = qtypc
       if trim(mdoctype) $ "STAI"
           mqtycs = 0-qtycs
           mqtypc = 0-qtypc
       endif
       
       sele (xeif)
       loca for csman=mcsman .and. cprodno=mcprodno .and. class=mclass
*       seek mkey
       if .not. found()
          append blank
          repl csman with mcsman,cprodno with mcprodno,class with mclass,supplier with msupplier
       endif
       repl qtycs with qtycs + mqtycs,qtypc with qtypc + mqtypc
       repl tohcs with tohcs + mqtycs,tohpc with tohpc + mqtypc
       m=depcs("qtycs","qtypc",mpieces)
       m=depcs("tohcs","tohpc",mpieces)
       if trim(mdoctype)="STAO"
          repl loadcs with loadcs + mqq,loadpc with loadpc + mqp
          m=depcs("loadcs","loadpc",mpieces)
       else
          repl unloadcs with unloadcs + mqq, unloadpc with unloadpc + mqp
          m=depcs("unloadcs","unloadpc",mpieces)
       endif
    
       if mqtycs+mqtypc>0
          repl uc with muc
       endif
       sele TEMPDET
       skip
    enddo
    sele TEMPHDR
    skip
 enddo
*wait window "total records press a key processed= "+str(mctr,6) +"press a key"
if closeprod
   m=closedbf("prod4win")
endif
if closehdr
   m=closedbf("imtr_hdr")
endif   
if closedet
   m=closedbf("imtr_det")
endif   
IF CLOSESYSPARAM
  M=CLOSEDBF("SYSPARAM")
ENDIF  
M=DELFILE("TEMPHDR")
M=DELFILE("TEMPDET")
m = delfile("temppr_recalc22")
sele (xeif)
set index to
erase &idx311
