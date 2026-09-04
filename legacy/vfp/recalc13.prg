* recalc13.prg
* recalculate ending balance of inventory file (temporary file)
* up to specified date
* param is file and target date
* with additional parameter to exclude dus clearing 02/06/2008
param xeif,xdate,xclude

closeprod = openfile("prod4win")
closeinact = openfile("inactive")
closehdr = .f.
closedet = .f.

CLOSESYSPARAM = openfile("sysparam")
sele sysparam
paramwhseno = val(bcode)
paramwhnumbers = trim(whnumbers)

IMHDR = GETFNAME(XDATE,"IH")
IMDET = GETFNAME(XDATE,"ID")

closehdr = openfile("imtr_hdr")
closedet = openfile("imtr_det")

TEMPHDR = NEWNAME("HDR")
TEMPDET = NEWNAME("DET")
temppr_recalc13 = NEWNAME("dbf")
if file("&imhdr") .and. file("&imdet")
   * use renamed imtr file if they exist otherwise use current month file
   sele IMTR_HDR
   copy to &TEMPHDR stru
   sele IMTR_DET
   copy to &TEMPDET stru
   use &TEMPHDR alias TEMPHDR in 0 exclu
   use &TEMPDET alias TEMPDET in 0 exclu
   if file("&IMHDR")
      sele TEMPHDR
      appe from &IMHDR
   endif
   if file("&IMDET")
      sele TEMPDET
      appe from &IMDET
   endif
else    
   * CURRENT MONTH
   sele IMTR_HDR
   copy to &TEMPHDR for POSTED<= XDATE .and. .not. empty(POSTED)
   sele IMTR_DET
   copy to &TEMPDET 
   use &TEMPHDR alias TEMPHDR in 0 excl
   use &TEMPDET alias TEMPDET in 0 excl
endif

sele prod4win
copy to &temppr_recalc13
use &temppr_recalc13 alias temppr_recalc13 in 0 excl
sele temppr_recalc13
appe from inactive
index on cprodno tag cprodno
set order to cprodno

idx311 = newname("311")

sele (xeif)    
repl all qtycs with begcs, qtypc with begpc
repl all salescs with 0, salespc with 0, othiscs with 0, othispc with 0
repl all rrcs with 0, rrpc with 0, othincs with 0, othinpc with 0
repl all tohcs with 0, tohpc with 0, alloccs with 0, allocpc with 0

if type("TOHLOOSE")="N"
   repl all INLOOSE with 0, OUTLOOSE with 0, tohloose with begloose
endif   
  
MIDXDET = NEWNAME("XD1")    
MIDXHDR = NEWNAME("XH1")
    
sele TEMPDET
index on DOCTYPE+str(DOCNO,8) to &MIDXDET
set rela to cprodno into temppr_recalc13

sele TEMPHDR
if xclude 
   *dele for docclass $ "1/3"
   dele for docclass = "1"
   pack
endif
index on DOCTYPE+str(DOCNO,8) to &MIDXHDR
set filter to posted <= xdate .and. .not. empty(posted)

mctr=0
go top
do while .not. eof()
    mctr=mctr+1
    mpostdate=posted
    mdoctype=doctype
    mdocno=docno
    mkey=mdoctype+str(mdocno,8)
    ************************
    servingwhseno=0
    if type("SERVEWH")="N"
       servingwhseno=servewh
    endif
    if servingwhseno=0 .or. str(servingwhseno,2) $ paramwhnumbers    &&=paramwhseno
       oktocompute=.t.
    else
       oktocompute=.f.
    endif
    ************************
    
    sele TEMPDET
    seek mkey
    do while doctype=mdoctype .and. docno=mdocno .and. .not. eof() .and. oktocompute
       wait window "Recalculating Inventory for prodno "+cprodno+" posted "+dtoc(mpostdate) nowait
       mqtycs = qtycs
       mqtypc = qtypc
       mqq = qtycs
       mpp=qtypc
       IF TYPE("LOOSEPC")="N"
          MLOOSE = LOOSEPC
          mqloose = loosepc
       ELSE
          MLOOSE=0
          mqloose=0
       ENDIF
       mkey = cprodno+str(class,1)
       msupplier=temppr_recalc13.supplier
       mcprodno=cprodno
       mclass=class        && class in for rsta
       mclassout=classout
       mpieces=temppr_recalc13.pieces
       muc=uc
       if trim(mdoctype) $ "INVOICE,STN,RTS,MIS,MISS,STAO,PTAO,"
           mqtycs = 0-qtycs
           mqtypc = 0-qtypc
           MLOOSE = MLOOSE* -1
       endif
       
       sele (xeif)
       loca for cprodno=mcprodno .and. class=mclass
       if .not. found()
          append blank
          repl cprodno with mcprodno,class with mclass,supplier with msupplier
       endif
       repl qtycs with qtycs + mqtycs,qtypc with qtypc + mqtypc
       if type("TOHLOOSE")="N"
          REPL tohloose WITH tohloose+ MLOOSE
       endif
       m=depcs("qtycs","qtypc",mpieces)
      
       ** update other fields
       if trim(mdoctype) $ "INVOICE,STN,RTS,MIS,MISS,STAO,PTAO,"
          if trim(mdoctype)="INVOICE"
             repl salescs with salescs + mqq,salespc with salespc + mpp
             m=depcs("salescs","salespc",mpieces)
          else
             repl othiscs with othiscs + mqq,othispc with othispc + mpp
             m=depcs("othiscs","othispc",mpieces)
             if type("TOHLOOSE")="N"
                repl outloose with outloose+mqloose
             endif
          endif
       else
          if trim(mdoctype)="RR"
             repl rrcs with rrcs + mqq,rrpc with rrpc + mpp
             m=depcs("rrcs","rrpc",mpieces)
          else
             repl othincs with othincs + mqq,othinpc with othinpc + mpp
             m=depcs("othincs","othinpc",mpieces)
             if type("TOHLOOSE")="N"
                repl inloose with inloose+mqloose
             endif
          endif
       endif
      
       if mqtycs+mqtypc>0
          repl uc with muc
       endif
       if trim(mdoctype)="RSTA"
          loca for cprodno=mcprodno .and. class=mclassout
          if .not. found()
             append blank
             repl cprodno with mcprodno,class with mclassout,supplier with msupplier
          endif
          repl qtycs with qtycs - mqq,qtypc with qtypc- mpp,uc with muc
          repl othiscs with othiscs + mqq,othispc with othispc + mpp
          m=depcs("qtycs","qtypc",mpieces)
          m=depcs("othiscs","othispc",mpieces)
          if type("TOHLOOSE")="N"
             repl outloose with outloose+mqloose
          endif

       endif
       sele TEMPDET
       skip
    enddo
    sele TEMPHDR
    skip
enddo
m=closetab("prod4win",closeprod)
m=closetab("inactive",closeinact)
m=closetab("imtr_hdr",closehdr)
m=closetab("imtr_det",closedet)
m=closetab("sysparam",closesysparam)

M=DELFILE("TEMPHDR")
M=DELFILE("TEMPDET")
M=DELFILE("temppr_recalc13")
sele (xeif)
set index to
erase &idx311
