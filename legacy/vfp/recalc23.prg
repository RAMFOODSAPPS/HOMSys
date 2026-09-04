* recalc23.prg
* recalculate van ending inventory from van header and van details
* up to specified date
* param is file and target date
param xeif,xdate


closeprod=.f.
closehdr=.f.
closedet=.f.
closeinact=.f.
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


IMHDR = GETFNAME(XDATE,"VH")
IMDET = GETFNAME(XDATE,"VD")

if .not. used("van_hdr")
   use van_hdr in 0 shared
   closehdr=.t.
endif   
if .not. used("van_det")
   use van_det in 0 shared
   closedet=.t.
endif   

TEMPHDR = NEWNAME("HDR")
TEMPDET = NEWNAME("DET")

if file("&imhdr") .and. file("&imdet")
    * use renamed imtr file if they exist otherwise use current month file
   SELE van_HDR
   COPY TO &TEMPHDR STRU
   SELE van_DET
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
   SELE van_HDR
   COPY TO &TEMPHDR FOR POSTED<= XDATE .AND. .NOT. EMPTY(POSTED)
   SELE van_DET
   COPY TO  &TEMPDET 
   USE &TEMPHDR ALIAS TEMPHDR IN 0 EXCLUSIVE
   USE &TEMPDET ALIAS TEMPDET IN 0 EXCLUSIVE
ENDIF

temppr_recalc23 = newname("dbf")
sele prod4win
copy to &temppr_recalc23
use &temppr_recalc23 alias temppr_recalc23 in 0 excl
sele temppr_recalc23
appe from inactive
index on cprodno tag cprodno
set order to cprodno
idx311 = newname("311")
MIDXDET = NEWNAME("XD1")    
MIDXHDR = NEWNAME("XH1")
    
sele TEMPDET
DELE FOR EMPTY(CPRODNO)
INDEX ON DOCTYPE+STR(DOCNO,8) TO  &MIDXDET
*set order to doctype
set rela to cprodno into temppr_recalc23

sele TEMPHDR
INDEX ON csman+DOCTYPE+STR(DOCNO,8) TO &MIDXHDR
set filter to posted <= xdate .and. .not. empty(posted) .and. substr(doctype,1,3)<> "BEG"

mctr=0
go top
do while .not. eof()
    mctr=mctr+1
    mpostdate=posted
    mdoctype=doctype
    mdocno=docno
    mkey=mdoctype+str(mdocno,8)
    mcsman = csman
    sele TEMPDET
    seek mkey
    do while doctype=mdoctype .and. docno=mdocno .and. .not. eof()
       wait window "Recalculating Inventory for prodno "+cprodno+" posted "+dtoc(mpostdate) nowait
       mqtycs = qtycs
       mqtypc = qtypc
       mqq = qtycs
       mpp=qtypc
       
       mkey = cprodno+str(class,1)
       msupplier=temppr_recalc23.supplier
       mcprodno=cprodno
       mclass=class        && class in for rsta
       IF TRIM(MDOCTYPE) $ "CSI,DSS"
          MCLASS = 1              && ALL DSS SHOULD EB CLASS 1
       ENDIF
       mclassout=classout
       mpieces=temppr_recalc23.pieces
       muc=uc
       mqq = qtycs
       mqp = qtypc
       if trim(mdoctype) $ "CSI,DSS,DUSMSA"
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
       m=depcs("tohcs","tohpc",mpieces)
       m=depcs("qtycs","qtypc",mpieces)
       if mqtycs+mqtypc>0
          repl uc with muc
       endif
       do case
          case trim(mdoctype) $ ",CSI,DSS,"
             repl salescs with salescs + mqq,salespc with salespc + mqp
          case trim(mdoctype) $ "RFDC"
             repl rfdccs with rfdccs + mqq,rfdcpc with rfdcpc + mqp
          case trim(mdoctype) $ ",RGW,RSTA,"
             repl othincs with othincs + mqq,othinpc with othinpc + mqp
          case trim(mdoctype) $ ",DUSMSA,"
             repl othiscs with othiscs + mqq,othispc with othispc + mqp
       endcase
       if trim(mdoctype)="RSTA"
          loca for csman=mcsman .and. cprodno=mcprodno .and. class=mclassout
          if .not. found()
             append blank
             repl csman with mcsman,cprodno with mcprodno,class with mclassout,supplier with msupplier
          endif
          repl qtycs with qtycs - mqq,qtypc with qtypc- mpp,uc with muc
          repl tohcs with tohcs - mqq,tohpc with tohpc- mpp 
          repl othiscs with othiscs + mqq,othispc with othispc + mqp
          m=depcs("qtycs","qtypc",mpieces)
          m=depcs("tohcs","tohpc",mpieces)
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
if closeinact
   m=closedbf("inactive")
endif
if closehdr
   m=closedbf("van_hdr")
endif   
if closedet
   m=closedbf("van_det")
endif   
IF CLOSESYSPARAM
  M=CLOSEDBF("SYSPARAM")
ENDIF  
M=DELFILE("TEMPHDR")
M=DELFILE("TEMPDET")
m = delfile("temppr_recalc23")
sele (xeif)
set index to
erase &idx311
