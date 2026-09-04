* prgoram name : sorate.prg
* create/update sorate.dbf for fillrate computation
param xxwh,xxckey,xxdocno,xxcprodno,xxdocdate
msele = sele()
xsupplier=supplier
*xcprodno=cprodno
xorigq = origqtycs
xorigqp = origqtypc
xq = qtycs
xqp = qtypc
xkey = str(xxwh,2)+xxckey + str(xxdocno,8)+xxcprodno
if .not. file("SORATE.DBF")
   create table SORATE (WHSENO N(2), CKEY C(5), DOCNO N(8),DOCDATE D(8), SUPPLIER N(2),;
       CPRODNO C(4), ORIGQTYCS N(7), ORIGQTYPC N(3), QTYCS N(7), QTYPC N(3) )
   m=closedbf("SORATE")
   use sorate in 0 exclusive
   sele sorate
   index on str(whseno,2)+ckey+str(docno,8)+cprodno tag wcprodno of sorate
   use
endif
closesorate=.f.
if .not. used("SORATE")
   use sorate in 0 shared
   closesorate=.t.
endif        
sele sorate
set order to wcprodno
seek xkey
if .not. found()
   append blank
   repl whseno with xxwh,ckey with xxckey,docno with xxdocno,docdate with xxdocdate,supplier with xsupplier
   repl cprodno with xxcprodno
endif
repl origqtycs with xorigq,origqtypc with xorigqp,qtycs with xq,qtypc with xqp
if closesorate
   m=closedbf("sorate")
endif
sele (msele)      