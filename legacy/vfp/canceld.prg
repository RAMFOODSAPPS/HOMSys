* canceld.prg
* save to canceld
param xdoctype,xdocno,xdocdate,xname,xamt,xcanceldate,xremarks,xusername
* m=canceld("INVOICE",invno,invdate,cusname,mamt,canceldate,mremarks,musername
msele=sele()
if .not. file("DOCCANCEL.DBF")
   create table DOCCANCEL(DOCTYPE C(10), DOCNO N(pub_docnosize), DOCDATE D(8), NAME C(35), AMOUNT N(12,2),;
       CANCELDATE D(8), REMARKS C(50), USERNAME C(20) )
   m=closedbf("DOCCANCEL")
endif

closecanceld = .f.
if .not. used("DOCCANCEL")
   use DOCCANCEL in 0 shared
   closecanceld = .t.
endif   
      
sele DOCCANCEL
append blank
repl doctype with xdoctype,docno with xdocno,docdate with xdocdate,name with xname
repl amount with xamt,canceldate with xcanceldate,remarks with xremarks
repl username with xusername

if closecanceld
   m=closedbf("DOCCANCEL")
endif   
sele (msele)
return .t.
