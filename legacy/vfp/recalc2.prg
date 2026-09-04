* program name : recalc2.prg
* racalculate inventory file
param xdate
close data
close tables

thisdate=ctod(xdate)   &&ctod("12/30/1999")

use avail4
copy to tempeif
use tempeif in 0 exclusive
wait window "this is recalc222 press press press "

do recalc11 with "tempeif",thisdate     && update qtycs from imtr
*do recalc1a  with "tempeif",thisdate   && update allocated from imtr
do recalc1b with "tempeif",thisdate    && update allocated from oowk
dclose=.f.
if .not. used("prod4win")
   use prod4win in 0 shared
   dclose=.t.
endif   
if .not. used("inactive")
   use inactive in 0 shared
   dclose2=.t.
endif   
temppr_recalc2 = newname("dbf")
sele prod4win
copy to &temppr_recalc2
use &temppr_recalc2 alias temppr_recalc2 in 0 excl
sele temppr_recalc2
appe from inactive
index on cprodno tag cprodno
set order to cprodno

sele tempeif
set rela to cprodno into temppr_recalc2
go top
do while .not. eof()
   mpieces=temppr_recalc2.pieces
   wait window "Updating TOHCS, TOHPC of prodno "+cprodno nowait
   repl tohcs with begcs+rrcs+othincs - (salescs + othiscs)
   repl tohpc with begpc+rrpc+othinpc - (salespc + othispc)
   m=depcs("tohcs","tohpc",mpieces)
   repl qtycs with tohcs - alloccs,qtypc with tohpc - allocpc
   m=depcs("qtycs","qtypc",mpieces)
   skip
enddo   
if dclose
  m=closedbf("prod4win")
endif  
if dclose2
  m=closedbf("inactive")
endif
m = delfile("temppr_recalc2")
m=messagebox("processing completed. verify tempeif.dbf")
clear all
