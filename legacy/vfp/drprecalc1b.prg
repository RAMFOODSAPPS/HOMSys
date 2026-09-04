* recalc1b.prg
* recalculate ending balance and allocated quantity from oowk
* up to specified date
* param is file and target date
param xeif,xdate,xcustkey
closeprod2=.f.
closehdr2=.f.
closedet2=.f.
CLOSESYSPARAM=.F.
closeinact2 = .f.
IF .NOT. USED("SYSPARAM")
   USE SYSPARAM IN 0 SHARED
   CLOSESYSPARAM=.T.
ENDIF

if .not. used("prod4win")
   use prod4win in 0 shared
   closeprod2=.t.
endif

if .not. used("inactive")
   use inactive in 0 shared
   closeinact2=.t.
endif

if .not. used("oowkhdr")
   use oowkhdr in 0 shared
   closehdr2=.t.
endif   
if .not. used("oowkdet")
   use oowkdet in 0 shared
   closedet2=.t.
endif   

tempxoh = newname("dbf")
tempxod = newname("dbf")
sele oowkhdr
copy to &tempxoh

sele oowkdet
copy to &tempxod

use &tempxoh alias tempxoh in 0 excl
use &tempxod alias tempxod in 0 excl

temppr_rec1b = newname("dbf")
sele prod4win
copy to &temppr_rec1b
use &temppr_rec1b alias temppr_rec1b in 0 excl
sele temppr_rec1b
index on cprodno tag cprodno
appe from inactive
set order to cprodno

idx311 = newname("311")
sele (xeif)
if .not. empty(xcustkey)
dele for custkey <> xcustkey
pack    
endif
repl all alloccs with 0,allocpc with 0
repl all qtycs with begcs,qtypc with begpc

*index on cprodno+str(class) to &idx311

midxxd = newname("idx")
sele tempxod
index on docno to &midxxd
*sele oowkdet
*set order to docno
set rela to cprodno into temppr_rec1b

sele (xeif)
repl all tohcs with begcs+rrcs+othincs - (salescs + othiscs)
repl all tohpc with begpc+rrpc+othinpc - (salespc + othispc)

*browse for cprodno="1117"

midxxh = newname("idx")
sele tempxoh
if .not. empty(xcustkey)
   dele for not drpcust = xcustkey
endif

sele tempxod
if .not. empty(xcustkey)
dele for not drpcust = xcustkey
endif
sele tempxoh
index on docno to &midxxh

*sele oowkhdr
*set order to docno
set filter to substr(status,1,1) $ "23456789" .and. ispsuedoso 
go top
** PROCESS ALLOCATED ONLY IF CURRENT MONTH
IF MONTH(XDATE)=MONTH(SYSPARAM.TRANSDATE) .AND. YEAR(XDATE)=YEAR(SYSPARAM.TRANSDATE)
  
do while .not. eof()
    mmdocno=docno
    *sele oowkdet
    sele tempxod
    seek mmdocno
    do while docno=mmdocno .and. .not. eof()
       wait window "Recalculating S.O. allocated for prodno "+cprodno nowait
       mqtycs = qtycs
       mqtypc = qtypc
       mdrpcust = drpcust
       msupplier=temppr_rec1b.supplier
       mcprodno=cprodno
       mclass= iif(oowkdet.class=0,1,oowkdet.class)     &&class        && class in for rsta
       mpieces=temppr_rec1b.pieces
       sele (xeif)
       if .not. empty(xcustkey)
          loca for cprodno=mcprodno .and. custkey = xcustkey
       else
          loca for cprodno=mcprodno .and. custkey = mdrpcust
       endif
       repl qtycs with qtycs - mqtycs,qtypc with qtypc - mqtypc
       repl alloccs with alloccs + mqtycs, allocpc with allocpc + mqtypc
       m=depcs("qtycs","qtypc",mpieces)
       m=depcs("alloccs","allocpc",mpieces)
       *sele oowkdet
       sele tempxod
       skip
    enddo
    *sele oowkhdr
    sele tempxoh
    skip
 enddo
sele (xeif)
*browse for cprodno="1113"
*browse for cprodno="1117"

 *************
 *** recalc allocated sta-out
 dclose1a = .f.
 if .not. used("drp_hdr")
    use drp_hdr in 0 shared
    dclose1a=.t.
 endif
 dclose1b=.f.
 if .not. used("drp_det")
    use drp_det in 0 shared
    dclose1b = .t.
 endif
 temp1a = newname("d1a")
 temp1b = newname("d1b")
 midx1b = newname("x1b")
 
 sele drp_hdr
 copy to &temp1a for status="3" .and. doctype="STAO"
 sele drp_det
 copy to &temp1b
 if dclose1a
    m=closedbf("drp_hdr")
 endif
 if dclose1b
    m=closedbf("drp_det")
 endif
 use &temp1a alias temp1a in 0 exclusive
 use &temp1b alias temp1b in 0 exclusive
 sele temp1b
 index on doctype+str(docno,pub_docnosize) to &midx1b
 set rela to cprodno into temppr_rec1b
 
 sele temp1a
* browse
 set filter to doctype="STAO"
 go top
 do while .not. eof()
    mdoctype=doctype
    mdocno=docno
    mkey = mdoctype+str(mdocno,pub_docnosize)
    sele temp1b
    seek mkey
    do while docno=mdocno .and. doctype=mdoctype .and. .not. eof()
       wait window "Recalculating STAO allocated for prodno "+cprodno nowait
       mpieces=temppr_rec1b.pieces
       mqtycs=qtycs
       mqtypc=qtypc
       mclass=class
       mcprodno=cprodno
       mdrpcust = custkey
       sele (xeif)
       if .not. empty(xcustkey)
          loca for cprodno=mcprodno .and. class=mclass .and. custkey = xcustkey
       else   
          loca for cprodno=mcprodno .and. class=mclass .and. custkey = mdrpcust
       endif
       repl qtycs with qtycs - mqtycs,qtypc with qtypc - mqtypc
       repl alloccs with alloccs + mqtycs, allocpc with allocpc + mqtypc
       m=depcs("qtycs","qtypc",mpieces)
       m=depcs("alloccs","allocpc",mpieces)
       sele temp1b
       skip
    enddo
    sele temp1a
    skip
 enddo
sele (xeif)

        
 sele temp1b
 set rela to
 m=delfile("temp1a")
 m=delfile("temp1b") 

 
ENDIF     && IF MONTH(XDATE)

m = delfile("tempxoh")
m = delfile("tempxod")
m = delfile("temppr_rec1b")
if closeprod2
   m=closedbf("prod4win")
endif
if closehdr2
   m=closedbf("drp_hdr")
endif   
if closedet2
   m=closedbf("drp_det")
endif   
IF CLOSESYSPARAM
   M=CLOSEDBF("SYSPARAM")
ENDIF   
if closeinact2
  M=CLOSEDBF("inactive")
endif
sele (xeif)
set index to
erase &idx311
