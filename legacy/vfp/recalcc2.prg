* recalcc2.prg
* 11/15/2012
* recalculation of class2.dbf inventory

param xe2mo, xeicl2, xdate
msele = select()

tempcl2 = newname("dbf")
tempcl2a = newname("dbf")
midxcl2 = newname("idx")
if month(sysparam.transdate) = month(xdate) and year(sysparam.transdate) = year(xdate)
   mclose = openfile("class2")
   sele class2
   copy to &tempcl2 for posted <= xdate
else
  if file(xe2mo)
     use &xe2mo alias xe2mo in 0 shared
     sele xe2mo
     copy to &tempcl2 for posted <= xdate
  else
     mclose = openfile("class2")
     sele class2
     copy stru to &tempcl2 
  endif
endif

use &tempcl2 alias tempcl2 in 0 excl
sele tempcl2
repl qtycs with 0-qtycs, qtypc with 0-qtypc, loosepc with 0-loosepc, amt with 0-amt, ;
   tohcs with 0-tohcs, tohpc with 0-tohpc, tohloose with 0-tohloose, doctype with reftype, ;
   docno with refno, docdate with refdate for ec = 2
*index on doctype+str(docno,8)+cprodno+str(uc,8,2) to &midxcl2
*total on doctype+str(docno,8)+cprodno+str(uc,8,2) to &tempcl2a fiel qtycs,qtypc,amt
index on cprodno+str(uc,8,2) to &midxcl2
total on cprodno+str(uc,8,2) to &tempcl2a fiel tohcs,tohpc,tohloose,qtycs,qtypc,loosepc,amt
use &tempcl2a alias tempcl2a in 0 excl
sele tempcl2a
dele for qtycs = 0 and qtypc = 0 and loosepc = 0
*dele for qtycs+qtypc+loosepc = 0
pack
sele (xeicl2)
appe from &tempcl2a
m = delfile("tempcl2a")
m = delfile("tempcl2")
if file(xe2mo)
   m = closedbf("xe2mo")
else
   if mclose
      m = closedbf("class2")
   endif
endif

sele (msele)