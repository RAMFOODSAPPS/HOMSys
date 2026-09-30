* getdocid.prg
* 
param xdoctype
xdocid=0
if .not. empty(xdoctype)
   xsele=sele()
   closedocnum=.f.
   if .not. used("docnum")
      use docnum in 0 shared
      closedocnum=.t.
   endif
   
   sele docnum
   loca for doctype=xdoctype
   xdocid=docid
   if closedocnum
      sele docnum
      use
   endif
   sele (xsele)
endif   
return xdocid

