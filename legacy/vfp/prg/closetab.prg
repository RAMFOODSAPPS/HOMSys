param xfile,xclose
mclose=.f.
if xclose
   if used(xfile)
      sele (xfile)
      use
      mclose=.t.
   endif
endif
return mclose
