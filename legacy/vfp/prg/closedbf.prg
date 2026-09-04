param xfile
mclose=.f.
if used(xfile)
   sele (xfile)
   use
   mclose=.t.
endif
return mclose
