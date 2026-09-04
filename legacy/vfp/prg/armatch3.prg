****************************
* Program   : Armatch3.prg *
* Rev. Date : July 20,2007 *
*           : RLR          *
****************************

param xfile

sele (xfile)
********************************************************************************
do applyrfc4rejected          && apply RFCs for rejected deliveries. set paymentid
********************************************************************************
* && apply payments in (xfile)
tempcr = newname("d28")
wait window "Recalculating DOCBAL Part 1" nowait
sele (xfile)
repl all docbal with dramt
copy to &tempcr for cramt<>0
delete for cramt<>0
pack
use &tempcr alias tempcr in 0 exclu
********************************************************************************
do applypayment2              && Update docbal,oramt in temp21
********************************************************************************
sele tempcr
mdbf=dbf()
sele (xfile)
append from &tempcr           && add back credit records. applied=1 is applied credits, 0 = unapplied
m=delfile("tempcr")
********************************************************************************
do updatedocuclass            && update docuclass in (xfile)      
********************************************************************************

sele (xfile)
go top
return
* return - end of the procedure




********************************************************************************
procedure applyrfc4rejected
* applyRfc4Rejected
* update paymentid of of RFCs for Rejected Deliveries
tempar41 = newname("d41")
tempar42 = newname("d42")
midxar41 = newname("x41")

sele (xfile)
copy to &tempar41 for dramt<>0 
*copy to &tempar42 for docid=6 .and. cramt<>0 .and. empty(paymentid)
copy to &tempar42 for (docid=6 .or. docid=2) .and. cramt<>0 .and. empty(paymentid)
*delete for docid=6 .and. cramt<>0 .and. empty(paymentid)
delete for (docid=6 .or. docid=2) .and. cramt<>0 .and. empty(paymentid)

use &tempar41 alias tempar41 in 0 exclu
sele tempar41
index on custkey+str(docid,2)+str(docno,8) to &midxar41

use &tempar42 alias tempar42 in 0 exclu

sele tempar42
mrec=reccount()
go top
do while .not. eof()
   mrec = mrec-1
   wait window "Processing RFCs of Rejected Deliveries "+str(mrec,6) nowait

   mkey = custkey+str(refid,2)+str(refno,8)
   mcramt = cramt
   sele tempar41
   seek mkey
   if found()
*      mvariance = dramt - mcramt
*      mvar = abs(mvariance)
*      mrate = percent(mvar,dramt)
*      if mrate <= 5

         sele tempar42
         repl paymentid with tempar41.paymentid
*      endif
   endif
   sele tempar42
   skip
enddo
   
sele tempar42
mdbf=dbf()   
sele (xfile)
append from &mdbf
m=delfile("tempar41")
m=delfile("tempar42")
********************************************************************************
procedure applypayment2          
* Update docbal, oramt in (xfile)
wait window "Recalculating DOCBAL Part 2" nowait
midx21 = newname("x21")
sele (xfile)
*index on str(whseno,2)+ckey+paymentid to &midx21
index on paymentid to &midx21
sele tempcr
mrec=reccount()
go top
do while .not. eof()
   mrec=mrec-1
   wait window "Payment application in progress "+str(mrec,8) nowait
   mapplied = 0
   if .not. empty(paymentid)
      mkey = paymentid 
      mamt = cramt
      mdocid = docid
      mreference=","+alltr(doctype)+" #"+str(docno,8)+" Dtd:"+dtoc(docdate)
      sele (xfile)
      seek mkey
      if found()
         repl docbal with docbal - mamt
         if str(mdocid,2) $ ", 2,33,52,"
            repl oramt with oramt + mamt,reference with alltr(reference)+mreference
         else
            repl ordeduct with ordeduct + mamt &&,reference with alltr(reference)+mreference
         endif
         mapplied=1
      endif
   endif
   sele tempcr
   repl applied with mapplied
   skip
enddo
********************************************************************************
procedure updatedocuclass
* update custbal
* mclass 1 = full amount
*        2 = hanging balance
*        3 = overpayment
*        4 = unapplied debits
*        5 = Unapplied credits
*		 6 = Applied credit
*        7 = Fully paid

* RLR 01/30/08 - trap bounced check
sele (xfile)
mrec=reccount()
go top
do while .not. eof()
   mrec = mrec-1
   wait window "Reclassifying documents "+str(mrec,8) nowait
   sele (xfile)
   mclass=0
   if dramt <> 0
      do case
         case badcqf=.t. && Trap Bounced Checks
            mclass = 8                      && bounced checks
         case docid<>1 .and. docbal > 0
            if badcqf=.t.
               mclass = 8                      && bounced checks
            else
               mclass = 4                      && other debits
            endif
         case docbal = dramt
            mclass = 1 						&& full invoice
         case docbal > 0 .and. oramt=0
            mclass = 1                    	&& Full Invoice - no OR/RPDC            
            mcreditrate = percent(docbal,dramt)
            if mcreditrate < 5
               mclass = 2					&& reclassify to hanging balance
            endif 
         case docbal > 0 .and. oramt<>0
            mclass = 2 						&& hanging balance
         case docbal < 0
            mclass = 3						&& over payment
         case docbal = 0
            mclass = 7						&& fully paid
      endcase
   else
*       mclass = iif (empty(paymentid),5,6)    && credit records
      if empty(paymentid) or applied=0
         mclass=5
      else
         mclass=6
      endif
   endif
   repl docuclass with mclass,docutype with getdocutype(mclass)                         
   
   skip
enddo
********************************************************************************
procedure getdocutype
param mdocuclass
local xtype
xtype=""
do case
   case mdocuclass=1
      xtype="INV"
   case mdocuclass=2
      xtype="HB"
   case mdocuclass=3
      xtype="OP"
   case mdocuclass=4
      xtype="OTHER DR"
   case mdocuclass=5
      xtype="UCE"
   case mdocuclass=8
      xtype="BOUNCE CHECKS"
endcase
return xtype
