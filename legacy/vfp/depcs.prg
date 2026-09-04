* depcs.prg
param xqtycs,xqtypc,xxpieces
xretval=.f.
if xxpieces<>0
   mepcs = (&xqtycs * xxpieces) + &xqtypc
   mcs = hconvert(mepcs,xxpieces,1)
   mpc = hconvert(mepcs,xxpieces,2)
   repl &xqtycs with mcs,&xqtypc with mpc
   xretval=.t.
endif   
return xretval

