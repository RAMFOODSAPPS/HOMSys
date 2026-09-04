* recalcc3.prg
* 10/22/2009
* recalculation of class3.dbf inventory

param xe3mo, xeicl3, xdate
msele = select()

tempcl3 = newname("dbf")
tempcl3a = newname("dbf")
midxcl3 = newname("idx")

if file(xe3mo)
   use &xe3mo alias xe3mo in 0 shared
   sele xe3mo

else
   mclose = openfile("class3")
   sele class3
endif
*sele class3
copy to &tempcl3 for posted <= xdate
use &tempcl3 alias tempcl3 in 0 excl
sele tempcl3
repl qtycs with 0-qtycs, qtypc with 0-qtypc, loosepc with 0-loosepc, amt with 0-amt, ;
   tohcs with 0-tohcs, tohpc with 0-tohpc, tohloose with 0-tohloose, doctype with reftype, ;
   docno with refno, docdate with refdate for ec = 2
*index on doctype+str(docno,8)+cprodno+str(uc,8,2) to &midxcl3
*total on doctype+str(docno,8)+cprodno+str(uc,8,2) to &tempcl3a fiel qtycs,qtypc,amt
index on cprodno+str(uc,8,2) to &midxcl3
total on cprodno+str(uc,8,2) to &tempcl3a fiel tohcs,tohpc,tohloose,qtycs,qtypc,loosepc,amt
use &tempcl3a alias tempcl3a in 0 excl
sele tempcl3a
dele for qtycs+qtypc+loosepc = 0
pack
sele (xeicl3)
appe from &tempcl3a

m = delfile("tempcl3a")
m = delfile("tempcl3")
if file(xe3mo)
   m = closedbf("xe3mo")
else
   if mclose
      m = closedbf("class3")
   endif
endif

sele (msele)