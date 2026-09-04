* recalc1a.prg
* recalculate ending balance and allocated quantity from imtr
* up to specified date
* param is file and target date
param xeif,xdate

closeprod2=.f.
closehdr2=.f.
closedet2=.f.
closeinact=.f.
if .not. used("prod4win")
   use prod4win in 0 shared
   closeprod2=.t.
endif
if .not. used("inactive")
   use inactive in 0 shared
   closeinact=.t.
endif
if .not. used("imtr_hdr")
   use imtr_hdr in 0 shared
   closehdr2=.t.
endif   
if .not. used("imtr_det")
   use imtr_det in 0 shared
   closedet2=.t.
endif   
temppr_recalc1a = newname("dbf")
sele prod4win
copy to &temppr_recalc1a
use &temppr_recalc1a alias temppr_recalc1a in 0 excl
sele temppr_recalc1a
appe from inactive
index on cprodno tag cprodno
set order to cprodno


idx311 = newname("311")
sele (xeif)    
repl all alloccs with 0,allocpc with 0
*qtycs with begcs,qtypc with begpc
*index on cprodno+str(class) to &idx311
    
sele imtr_det
set order to doctype
set rela to cprodno into temppr_recalc1a

sele imtr_hdr

set order to doctype
set filter to status="3" .and. docdate<=xdate
go top
do while .not. eof()
    mdoctype=doctype
    mdocno=docno
    mkey=mdoctype+str(mdocno,8)
    if trim(doctype) $ "INVOICE,STN,RTS,MIS,MISS,STAO,"       && outgoind
       ** transaction is outgoing and is allocated
    else
        * transaction is incoming - no allocation
        wait window "Skipping "+doctype+ ' '+str(docno,8) nowait
        skip
        loop
    endif
    sele imtr_det
    seek mkey
    do while doctype=mdoctype .and. docno=mdocno .and. .not. eof()
       wait window "Recalculating allocated for prodno "+cprodno nowait
       mqtycs = qtycs
       mqtypc = qtypc
       mkey = cprodno+str(class,1)
       msupplier=temppr_recalc1a.supplier
       mcprodno=cprodno
       mclass=class        && class in for rsta
       mclassout=classout
       mpieces=temppr_recalc1a.pieces
       muc=uc
       if trim(doctype)="RSTA"
          mclass=classout
       endif
*       if trim(doctype) $ "INVOICE,STN,RTS,MIS,MISS,STAO,"       && outgoind
*           mqtycs = 0-qtycs
*           mqtypc = 0-qtypc
*       endif
       sele (xeif)
       loca for cprodno=mcprodno .and. class=mclass
*       seek mkey
       if .not. found()
          append blank
          repl cprodno with mcprodno,class with mclass,supplier with msupplier
       endif
       repl qtycs with qtycs - mqtycs,qtypc with qtypc - mqtypc
       repl alloccs with alloccs + mqtycs, allocpc with allocpc + mqtypc
       m=depcs("qtycs","qtypc",mpieces)
       m=depcs("alloccs","allocpc",mpieces)
       sele imtr_det
       skip
    enddo
 enddo
if closeprod2
   m=closedbf("prod4win")
endif
if closeinact
   m=closedbf("inactive")
endif
if closehdr2
   m=closedbf("imtr_hdr")
endif   
if closedet2
   m=closedbf("imtr_det")
endif   
sele (xeif)
set index to
erase &idx311
m = delfile("temppr_recalc1a")