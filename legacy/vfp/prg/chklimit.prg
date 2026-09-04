param cur_istemp,cur_wkdet,cur_custkey,cur_ckey,cur_whseno,cur_cprodno,cur_proddesc,cur_pieces,cur_qtycs,cur_qtypc,cur_um,cur_qtyperpc,cur_curqty,cur_docno,cur_orderdate,cur_ponum,cur_podate,cur_username,cur_supplier,cur_class,cur_cusname,cur_consomax2

tempend = newname("dbf")
dhdr = newname("dbf")
ddet = newname("dbf")
midxdhdr = newname("idx")
*mxidxor = newname("idx")
*!*	sele maxorder
*!*	index on custkey+cprodno to &mxidxor
sele sysparam
msytrdate = transdate

*sele tempowkh
sele oowkhdr
copy to &dhdr for custkey = cur_custkey && .and. docno <> mdocno
use &dhdr alias dhdr in 0 excl
sele dhdr
index on docno to &midxdhdr
*sele tempowkd
sele oowkdet
dele for empty(cprodno)
set rela to docno into dhdr
copy to &ddet for docno = dhdr.docno .and. cprodno = cur_cprodno .and. .not. empty(cprodno)
set rela to
m = delfile("dhdr")

&& acastillano - include ace / amc 08/28/2019
maceamcpath = "" 
if sysparam.oldccode = 1
   maceamcpath = addbs(alltr(sysparam.bmsacepath))
else
   if sysparam.oldccode = 2
      maceamcpath = addbs(alltr(sysparam.bmsamcpath))
   endif
endif
isemptypath = .t.
dhdr2 = newname("dbf")
ddet2 = newname("dbf")
midxdhdr2 = newname("idx")
dsales2 = newname("dbf")
if .not. empty(maceamcpath)
   cur_oowkhfl = maceamcpath+"OOWKHDR.DBF"
   cur_oowkdfl = maceamcpath+"OOWKDET.DBF"
   cur_slfile =  maceamcpath+"SALE4WIN.DBF"

   if file("&cur_oowkhfl") .and. file("&cur_oowkdfl") .and. file("&cur_slfile")
 
      use &cur_oowkhfl alias cur_oowkhfl in 0 shared
      use &cur_oowkdfl alias cur_oowkdfl in 0 shared
      use &cur_slfile alias cur_slfile in 0 shared

      sele cur_oowkhfl
      copy to &dhdr2 for custkey = cur_custkey
      use &dhdr2 alias dhdr2 in 0 excl
      sele dhdr2
      index on docno to &midxdhdr2

      sele cur_oowkdfl
      dele for empty(cprodno)
      set rela to docno into dhdr2
      copy to &ddet2 for docno = dhdr2.docno .and. cprodno = cur_cprodno .and. .not. empty(cprodno)
      set rela to

      sele cur_slfile
      copy to &dsales2 for docid = 1 .and. custkey = cur_custkey .and. cprodno = cur_cprodno .and. month(trdate) = month(msytrdate) .and. year(trdate) = year(msytrdate)

      m = closedbf("cur_oowkdfl")
      m = closedbf("cur_oowkhfl")
      m = closedbf("cur_slfile")
      m = delfile("dhdr2")
      isemptypath = .f.
   endif
   
endif
******

use &ddet alias ddet in 0 excl
sele ddet
if .not. isemptypath
   append from &ddet2 && acastillano ace/amc customer allocated order
   erase &ddet2
endif
sum qtycs,qtypc to oqtycs,oqtypc
torder = oqtycs + (oqtypc/cur_pieces)
m = delfile("ddet")

dsales = newname("dbf")
sele sale4win
copy to &dsales for docid = 1 .and. custkey = cur_custkey .and. cprodno = cur_cprodno .and. month(trdate) = month(msytrdate) .and. year(trdate) = year(msytrdate)
use &dsales alias dsales in 0 excl
sele dsales
if .not. isemptypath
   append from &dsales2 && acastillano ace/amc customer sales order
   erase &dsales2 
endif

sum qtycs,qtypc to sqtycs,sqtypc
tsales = sqtycs + (sqtypc/cur_pieces)

m = delfile("dsales")
mtotqty = torder + tsales
if .not. cur_istemp
   mtotorder = mtotqty 
   mtotcs  = sqtycs + oqtycs   
   mtotpc  = sqtypc + oqtypc 
else
   mtotorder = mtotqty + cur_curqty
   mtotcs  = sqtycs + oqtycs + cur_qtycs   && acastillano 08/01/2019
   mtotpc  = sqtypc + oqtypc + cur_qtypc
endif

mbalorder =  (mtotcs * cur_pieces) + mtotpc 

msugmxcs = hconvert(mbalorder,cur_pieces,1)  && acastillano 08/01/2019
msugmxpc = hconvert(mbalorder,cur_pieces,2)
if msugmxpc >0 && add 1 cs if there's excessed pc
   msugmxcs = msugmxcs + 1
endif


issugmax = .f.
m = audtrail("BMS-Order Booking","SO",cur_docno,cur_orderdate,cur_custkey,cur_cprodno,0,cur_qtycs,cur_qtypc,0,"CHECKLIMIT(1) torder=" + allt(str(torder,12,2)) + " tsales=" + allt(str(tsales,12,2)) + " mcurqty=" + allt(str(cur_curqty,12,2)))

store 0 to allmaxcs,allmaxpc,allmonths
curmaxdate = {}

*07/04/2012
if .not. empty(cur_consomax2)
   do getfccoschain with cur_cprodno,cur_consomax2
   m = audtrail("BMS-Order Booking","SO",cur_docno,cur_orderdate,cur_custkey,cur_cprodno,0,cur_qtycs,cur_qtypc,0,"CHECKLIMIT(2) mconsomax2: " + cur_consomax2 + " allmaxcs=" + allt(str(allmaxcs)) + " allmaxpc=" + allt(str(allmaxpc)) + " allmonths=" + allt(str(allmonths)) + " curmaxdate=" + dtoc(curmaxdate))
else
   sele maxorder
   seek cur_custkey+cur_cprodno
   allmaxcs = maxqtycs
   allmaxpc = maxqtypc
   allmonths = months
   curmaxdate = date
   m = audtrail("BMS-Order Booking","SO",cur_docno,cur_orderdate,cur_custkey,cur_cprodno,0,cur_qtycs,cur_qtypc,0,"CHECKLIMIT(3) allmaxcs=" + allt(str(allmaxcs)) + " allmaxpc=" + allt(str(allmaxpc)) + " allmonths=" + allt(str(allmonths)) + " curmaxdate=" + dtoc(curmaxdate))
endif
*if found()
if allmaxcs + allmaxpc <> 0
   m = audtrail("BMS-Order Booking","SO",cur_docno,cur_orderdate,cur_custkey,cur_cprodno,0,cur_qtycs,cur_qtypc,0,"CHECKLIMIT(4) mcustkey: " + cur_custkey + " allmaxcs=" + allt(str(allmaxcs)) + " allmaxpc=" + allt(str(allmaxpc)) + " allmonths=" + allt(str(allmonths)) + " curmaxdate=" + dtoc(curmaxdate))
   **thisform.dorderlimit2 acastillano 08/20/2019 - transfer to prg
   do dorderlimit2 with cur_istemp,cur_wkdet,cur_custkey,cur_ckey,cur_whseno,cur_cprodno,cur_proddesc,cur_pieces,cur_qtycs,cur_qtypc,cur_um,cur_qtyperpc,cur_curqty,cur_docno,cur_orderdate,cur_ponum,cur_podate,cur_username,cur_supplier,cur_class,cur_cusname,allmaxcs,allmaxpc,issugmax,msugmxcs
else
   sele cust4win
   seek cur_custkey
   if found() and !empty(aliaskey)
      custkey1=aliaskey
      sele maxorder
      locate for custkey=custkey1 and cprodno=cur_cprodno
      allmaxcs = maxqtycs
      allmaxpc = maxqtypc
      allmonths = months
      curmaxdate = date
      m = audtrail("BMS-Order Booking","SO",cur_docno,cur_orderdate,cur_custkey,cur_cprodno,0,cur_qtycs,cur_qtypc,0,"CHECKLIMIT(5) aliaskey: " + custkey1 + " allmaxcs=" + allt(str(allmaxcs)) + " allmaxpc=" + allt(str(allmaxpc)) + " allmonths=" + allt(str(allmonths)) + " curmaxdate=" + dtoc(curmaxdate))
   else
      sele maxorder
      seek "ALL"+space(4)+cur_cprodno
      allmaxcs = maxqtycs
      allmaxpc = maxqtypc
      allmonths = months
      curmaxdate = date
      m = audtrail("BMS-Order Booking","SO",cur_docno,cur_orderdate,cur_custkey,cur_cprodno,0,cur_qtycs,cur_qtypc,0,"CHECKLIMIT(6) ALL allmaxcs=" + allt(str(allmaxcs)) + " allmaxpc=" + allt(str(allmaxpc)) + " allmonths=" + allt(str(allmonths)) + " curmaxdate=" + dtoc(curmaxdate))      
   endi
   if allmaxcs + allmaxpc <> 0
      **thisform.dorderlimit2 acastillano 08/20/2019 - transfer to prg
      do dorderlimit2 with cur_istemp,cur_wkdet,cur_custkey,cur_ckey,cur_whseno,cur_cprodno,cur_proddesc,cur_pieces,cur_qtycs,cur_qtypc,cur_um,cur_qtyperpc,cur_curqty,cur_docno,cur_orderdate,cur_ponum,cur_podate,cur_username,cur_supplier,cur_class,cur_cusname,allmaxcs,allmaxpc,issugmax,msugmxcs
   else   
      *m = thisform.updatedmaxstat(mqtycs,mqtypc,mqtycs,mqtypc,"OK. No ORDER LIMIT for this CUSTOMER+SKU",.f.,mproddesc,mpieces,mdocno,mckey,morderdate,mponum,mwhseno,mum,mqtyperpc,musername) &&gbugarin      
      do upmaxstat with cur_istemp,cur_qtycs,cur_qtypc,cur_qtycs,cur_qtypc,"OK. No ORDER LIMIT for this CUSTOMER+SKU",.f.,cur_proddesc,cur_pieces,cur_docno,cur_ckey,cur_orderdate,cur_whseno,cur_um,cur_qtyperpc,cur_username,,cur_podate,cur_ponum,cur_custkey,cur_cusname,cur_cprodno,cur_docno,allmaxcs,allmaxpc,issugmax,msugmxcs &&gbugarin      
      m = audtrail("BMS-Order Booking","SO",cur_docno,cur_orderdate,cur_custkey,cur_cprodno,0,cur_qtycs,cur_qtypc,0,"CHECKLIMIT(7) No limit allmaxcs=" + allt(str(allmaxcs)) + " allmaxpc=" + allt(str(allmaxpc)) + " allmonths=" + allt(str(allmonths)))      
      mnotok = .f.
      *thisform.dordervsavesalesx4
   endif
endif

procedure dorderlimit2
param dor_istemp,dor_wkdet,dor_custkey,dor_ckey,dor_whseno,dor_cprodno,dor_proddesc,dor_pieces,dor_qtycs,dor_qtypc,dor_um,dor_qtyperpc,dor_curqty,dor_docno,dor_orderdate,dor_ponum,dor_podate,dor_username,dor_supplier,dor_class,dor_cusname,dor_allmaxcs,dor_allmaxpc,dor_issugmax,dor_sugmxcs   

mbmsbkpath = ""
m = openfile("bmsvar") && acastillano 02/03/2021
sele bmsvar
locate for varname = "BMSBKPATH"
if found()
   mbmsbkpath = addbs(varvalue) + "DOSFILES\"
endif
m = closedbf("bmsvar")
moutdir = "C:\VFPTEMP\"
if .not. director("&moutdir")
   md &moutdir
endif

*sele maxorder
maxqty = qtyloose(dor_allmaxcs,dor_allmaxpc,0,dor_pieces,0,4)
mmaxproceed = .t.
if allmonths > 1
   mctrmo = 0 - (allmonths - 1)
   mmofr = eom(gomonth(sysparam.transdate,mctrmo))
   mmoto = eom(sysparam.transdate)
   do while mmofr < mmoto
      *mslfile = renfile(mmofr,"SL",val(sysparam.bcode))
      
      mslfile= ""
      meomfl = mbmsbkpath + "EO" + alltr(sysparam.bcode) + prgnamer("my", mmofr) + ".zip"
      if file(meomfl)
      
         wait wind "Accessing EOM " + meomfl nowait
         mslfile= renfile(mmofr,"SL",VAL(sysparam.bcode))
         mslfile2 = moutdir + mslfile
      
         if file("&mslfile2")
            erase &mslfile2
         endif
      
         mtofile = meomfl +" -o" + moutdir+ " " + mslfile
         run 7z x &mtofile
         mslfile = moutdir + mslfile
      endif
      
      if file(mslfile)
         use &mslfile alias mslfile in 0 shared
         dsales = newname("dbf")
         sele mslfile
         copy to &dsales for docid = 1 .and. custkey = dor_custkey .and. cprodno = dor_cprodno
         use &dsales alias dsales in 0 excl
         sele dsales
         sum qtycs,qtypc to sqtycs,sqtypc
         mtotqty = mtotqty + (sqtycs + (sqtypc/dor_pieces))
         m = delfile("dsales")
         m = closedbf("mslfile")
         m = audtrail("BMS-Order Booking","SO",dor_docno,dor_orderdate,dor_custkey,dor_cprodno,0,dor_qtycs,dor_qtypc,0,"DORDERLIMIT2(6) " + mslfile + " found. mtotqty=" + allt(str(mtotqty,12,2)))
      else
         **m = thisform.updatedmaxstat(mqtycs,mqtypc,0,0,"Not OK. " + mslfile + " not found.",.t., mproddesc,mpieces,mdocno,mckey,morderdate,mponum,mwhseno,mum,mqtyperpc,musername) &&gbugarin
         
         && acastiallno 08/20/2019 - transfer to prg
         do upmaxstat with dor_istemp,dor_qtycs,dor_qtypc,0,0,"Not OK. " + mslfile + " not found.",.t., dor_proddesc,dor_pieces,dor_docno,dor_ckey,dor_orderdate,dor_whseno,dor_um,dor_qtyperpc,dor_username,,dor_podate,dor_ponum,dor_custkey,dor_cusname,dor_cprodno,dor_docno,dor_allmaxcs,dor_allmaxpc,dor_issugmax,dor_sugmxcs  &&gbugarin
         m = audtrail("BMS-Order Booking","SO",dor_docno,dor_orderdate,dor_custkey,dor_cprodno,0,dor_qtycs,dor_qtypc,0,"DORDERLIMIT2(1) remarks: Not OK. " + mslfile + " not found.")
         mmaxproceed = .f.
      endif
      mmofr = eom(gomonth(mmofr,1))
   enddo
endif

if mmaxproceed
   if dor_istemp
      mF = maxqty - mtotqty
   else && acastilalno 08/30/2019
     x_order = dor_qtycs + (dor_qtypc/dor_pieces)
     mF = maxqty - (mtotqty - x_order)
   endif
   if mF <= 0 && total orders is now equal to maxorder
      dor_issugmax  = .t.
      *sele tempowkd
      sele (dor_wkdet)
      dele for docno = dor_docno and cprodno = dor_cprodno
      appe blank
      repl docno with dor_docno, supplier with dor_supplier, class with dor_class, prodno with val(dor_cprodno), cprodno with dor_cprodno, qtycs with 0, qtypc with 0

      sele prod4win
      seek dor_cprodno
      mproddesc = proddesc
      mpacksize = packsize
      mpricelist = pricelist
      mtaxrate = taxrate
*!*	      mprcsmax = .t.
      *sele tempowkd
      sele (dor_wkdet)
      repl proddesc with mproddesc, packsize with mpacksize, pricelist with mpricelist, taxrate with mtaxrate
      *m = thisform.updatedmaxstat(mqtycs,mqtypc,0,0,"Not OK. Exceeded Max. Order of " + allt(str(maxqty)) + " CS with TOTAL ORDER of " + allt(str(mtotqty)) + ".",.t., mproddesc,mpieces,mdocno,mckey,morderdate,mponum,mwhseno,mum,mqtyperpc,musername,.t.) &&gbugarin
      && acastiallno 08/20/2019 - transfer to prg
      do upmaxstat with dor_istemp,dor_qtycs,dor_qtypc,0,0,"Not OK. Exceeded Max. Order of " + allt(str(maxqty)) + " CS with TOTAL ORDER of " + allt(str(mtotqty)) + ".",.t., dor_proddesc,dor_pieces,dor_docno,dor_ckey,dor_orderdate,dor_whseno,dor_um,dor_qtyperpc,dor_username,.t.,dor_podate,dor_ponum,dor_custkey,dor_cusname,dor_cprodno,dor_docno,dor_allmaxcs,dor_allmaxpc,dor_issugmax,dor_sugmxcs  &&gbugarin
      m = audtrail("BMS-Order Booking","SO",dor_docno,dor_orderdate,dor_custkey,dor_cprodno,0,dor_qtycs,dor_qtypc,0,"DORDERLIMIT2(2) remarks: Not OK. Exceeded Max. Order of " + allt(str(maxqty)) + " CS with TOTAL ORDER of " + allt(str(mtotqty))) 
   else
      if mF < dor_curqty
         dor_issugmax  = .t.
         
         remcs = int(mF)
         rempc = (mF - int(mF)) * dor_pieces
         
         *sele tempowkd
         if dor_istemp && acastilalno 08/30/2019
            sele (dor_wkdet)
            repl dgetmax with .t. for docno = dor_docno and cprodno = dor_cprodno
         endif
        *m = thisform.updatedmaxstat(mqtycs,mqtypc,remcs,rempc,"Ok but reduced QTY due to order constraints.",.t., mproddesc,mpieces,mdocno,mckey,morderdate,mponum,mwhseno,mum,mqtyperpc,musername,.t.) &&gbugarin
         do upmaxstat with dor_istemp,dor_qtycs,dor_qtypc,remcs,rempc,"Ok but reduced QTY due to order constraints.",.t., dor_proddesc,dor_pieces,dor_docno,dor_ckey,dor_orderdate,dor_whseno,dor_um,dor_qtyperpc,dor_username,.t.,dor_podate,dor_ponum,dor_custkey,dor_cusname,dor_cprodno,dor_docno,dor_allmaxcs,dor_allmaxpc,dor_issugmax,dor_sugmxcs  &&gbugarin
         m = audtrail("BMS-Order Booking","SO",dor_docno,dor_orderdate,dor_custkey,dor_cprodno,0,dor_qtycs,dor_qtypc,0,"DORDERLIMIT2(4) remarks: Ok but reduced QTY to remcs=" + allt(str(remcs))+" rempc=" + allt(str(rempc)))            
      else
         remcs = dor_qtycs
         rempc = dor_qtypc
        *m = thisform.updatedmaxstat(mqtycs,mqtypc,remcs,rempc,"OK. Within the maximum order set for the customer.",.f., mproddesc,mpieces,mdocno,mckey,morderdate,mponum,mwhseno,mum,mqtyperpc,musername) &&gbugarin
         do upmaxstat with dor_istemp,dor_qtycs,dor_qtypc,remcs,rempc,"OK. Within the maximum order set for the customer.",.f., dor_proddesc,dor_pieces,dor_docno,dor_ckey,dor_orderdate,dor_whseno,dor_um,dor_qtyperpc,dor_username,,dor_podate,dor_ponum,dor_custkey,dor_cusname,dor_cprodno,dor_docno,dor_allmaxcs,dor_allmaxpc,dor_issugmax,dor_sugmxcs  &&gbugarin
         m = audtrail("BMS-Order Booking","SO",dor_docno,dor_orderdate,dor_custkey,dor_cprodno,0,dor_qtycs,dor_qtypc,0,"DORDERLIMIT2(5) remarks: OK. Within the maximum order set for the customer remcs=" + allt(str(remcs))+" rempc=" + allt(str(rempc)))
      endif

      sele prod4win
      seek dor_cprodno
      mproddesc = proddesc
      mpacksize = packsize
      mpricelist = pricelist
      mtaxrate = taxrate
      *sele tempowkd
      sele (dor_wkdet)
      repl proddesc with mproddesc, packsize with mpacksize, pricelist with mpricelist, taxrate with mtaxrate          

      mnotok = .f.
   endif
endif
endproc

procedure getfccoschain
param cc_cprodno, cc_consomax2
dconcus = newname("dbf")
sele cust4win
copy to &dconcus for consomax2 = cc_consomax2
use &dconcus alias dconcus in 0 excl
sele dconcus
go top
do while .not. eof()
   concustkey = custkey
   sele maxorder
   seek concustkey+cc_cprodno
   if found()
      allmaxcs = allmaxcs + maxqtycs
      allmaxpc = allmaxpc + maxqtypc    
      allmonths = allmonths + months
      curmaxdate = date
   endif
   sele dconcus
   skip
enddo
m = delfile("dconcus")

endproc