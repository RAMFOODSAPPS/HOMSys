* delfile.prg
* delete file
param malias
mretval=.f.
if used(malias)
   sele (malias)
   mdbf=dbf()
   midx = ndx(1,malias)
   use
   erase &mdbf
   if len(midx)>0
      erase &midx
   endif
   mretval=.t.
endif
return mretval
   
