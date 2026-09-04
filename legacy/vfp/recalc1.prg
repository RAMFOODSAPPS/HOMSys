* recalc1.prg
* recalculate ending balance of inventory file (temporary file)
* up to specified date
* param is file and target date
param xeif,xdate

wait window "this is recalc1 -revised press any key."

closeprod=.f.
closehdr=.f.
closedet=.f.
closeinact = .f.
if .not. used("prod4win")
   use prod4win in 0 shared
   closeprod=.t.
endif
if .not. used("inactive")
   use inactive in 0 shared
   closeinact=.t.
endif
if .not. used("imtr_hdr")
   use imtr_hdr in 0 shared
   closehdr=.t.
endif   
if .not. used("imtr_det")
   use imtr_det in 0 shared
   closedet=.t.
endif   
temppr_recalc1 = newname("dbf")
sele prod4win
copy to &temppr_recalc1 
use &temppr_recalc1 alias temppr_recalc1 in 0 excl
sele temppr_recalc1
appe from inactive
index on cprodno tag cprodno
set order to cprodno

idx311 = newname("311")

sele (xeif)    
repl all qtycs with begcs,qtypc with begpc
repl all salescs with 0,salespc with 0,othiscs with 0,othispc with 0
repl all rrcs with 0,rrpc with 0,othiscs with 0,othispc with 0
repl all tohcs with 0,tohpc with 0,alloccs with 0,allocpc with 0
*index on cprodno+str(class) to &idx311
    
sele imtr_det
set order to doctype
set rela to cprodno into temppr_recalc1

sele imtr_hdr

set order to doctype
set filter to posted <= xdate
go top
do while .not. eof()
    mdoctype=doctype
    mdocno=docno
    mkey=mdoctype+str(mdocno,8)
    sele imtr_det
    seek mkey
    do while doctype=mdoctype .and. docno=mdocno .and. .not. eof()
       wait window "Recalculating Inventory for prodno "+cprodno nowait
       mqtycs = qtycs
       mqtypc = qtypc
       mqq = qtycs
       mpp=qtypc
       
       mkey = cprodno+str(class,1)
       msupplier=temppr_recalc1.supplier
       mcprodno=cprodno
       mclass=class        && class in for rsta
       mclassout=classout
       mpieces=temppr_recalc1.pieces
       muc=uc
       if trim(mdoctype) $ "INVOICE,STN,RTS,MIS,MISS,STAO,"
           mqtycs = 0-qtycs
           mqtypc = 0-qtypc
       endif
       sele (xeif)
       loca for cprodno=mcprodno .and. class=mclass
*       seek mkey
       if .not. found()
          append blank
          repl cprodno with mcprodno,class with mclass,supplier with msupplier
       endif
       repl qtycs with qtycs + mqtycs,qtypc with qtypc + mqtypc
       m=depcs("qtycs","qtypc",mpieces)
       
       ** update other fields
       if trim(mdoctype) $ "INVOICE,STN,RTS,MIS,MISS,STAO,"
          if trim(mdoctype)="INVOICE"
             repl salescs with salescs + mqq,salespc with salespc + mpp
             m=depcs("salescs","salespc",mpieces)
          else
             repl othiscs with othiscs + mqq,othispc with othispc + mpp
             m=depcs("othiscs","othispc",mpieces)
          endif
       else
          if trim(mdoctype)="RR"
             repl rrcs with rrcs + mqq,rrpc with rrpc + mpp
             m=depcs("rrcs","rrpc",mpieces)
          else
             repl othincs with othincs + mqq,othinpc with othinpc + mpp
             m=depcs("othincs","othinpc",mpieces)
          endif
       endif
      
       if mqtycs+mqtypc>0
          repl uc with muc
       endif
       if trim(mdoctype)="RSTA"
*          mkey = mcprodno+str(mclassout,1)
*          seek mkey
          loca for cprodno=mcprodno .and. class=mclassout
          if .not. found()
             append blank
             repl cprodno with mcprodno,class with mclassout,supplier with msupplier
          endif
          repl qtycs with qtycs - mqtycs,qtypc with qtypc- mqtypc,uc with muc
          m=depcs("qtycs","qtypc",mpieces)
       endif
       sele imtr_det
       skip
    enddo
 enddo
if closeprod
   m=closedbf("prod4win")
endif
if closehdr
   m=closedbf("imtr_hdr")
endif   
if closedet
   m=closedbf("imtr_det")
endif   
if closeinact
   m=closedbf("inactive")
endif  
sele (xeif)
set index to
erase &idx311
sele temppr_recalc1
use
erase &temppr_recalc1
