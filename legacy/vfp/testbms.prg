param mform,mparam,mprg
release appusername,mbmscolor,pub_exeid,mfullname,msupervisor
public appusername,mbmscolor,pub_exeid,mfullname,msupervisor
release pub_socutoff
public pub_socutoff

release pub_workpod,pub_motherpath && acastillano 05/16/2017
public pub_workpod,pub_motherpath

pub_socutoff = ctod("03/01/2021")

m = openfile("sysparam")
sele sysparam
pub_workpod = sysparam.workpod
pub_motherpath = sysparam.motherpath
if sysparam.ccode = 3
   mbmscolor = "128,0,0"
else
   if sysparam.ccode = 1
      mbmscolor = "0,64,128"
   else 
      mbmscolor = "0,128,64"
   endif
endif
release pub_dmaxo,pub_dinvb,pub_dretb,pub_dmaxr,pub_ddusb
public pub_dmaxo,pub_dinvb,pub_dretb,pub_dmaxr,pub_ddusb
if pub_workpod
   pub_dmaxo = addbs(pub_motherpath)+"MAXORDER.DBF"
   pub_dinvb = addbs(pub_motherpath)+"INVBLOCK.DBF"
   pub_dretb = addbs(pub_motherpath)+"RETBLOCK.DBF"
   pub_dmaxr = addbs(pub_motherpath)+"MAXRETRN.DBF"
   pub_ddusb = addbs(pub_motherpath)+"DUSUNBLK.DBF"   
else
   pub_dmaxo = "MAXORDER.DBF"
   pub_dinvb = "INVBLOCK.DBF"
   pub_dretb = "RETBLOCK.DBF"
   pub_dmaxr = "MAXRETRN.DBF"
   pub_ddusb = "DUSUNBLK.DBF"
endif
m = closedbf("sysparam")
mfullname = "AILEEN SOCORRO D. BELLEN"
msupervisor = "AILEEN SOCORRO D. BELLEN"
appusername = "ABELLEN"
appuserlevel = "3"
pub_exeid = "BMS05232014"
* HARAGO 06/21/2018
release pub_whsize, pub_ccnosize, pub_docnosize
public pub_whsize, pub_ccnosize, pub_docnosize

m = openfile("bmsvar")
sele bmsvar
locate for varname = "WHFSIZE"
IF found()
   pub_whsize = val(varvalue)
endif
locate for varname = "DOCNOFSIZE"
IF found()
   pub_docnosize = val(varvalue)
endif
locate for varname = "CCNOFSIZE"
IF found()
   pub_ccnosize = val(varvalue)
endif

m = closedbf("bmsvar")
* harago 06/21/2018

if .not. empty(mform)
   if empty(mparam)
      do form &mform
   else
      do form &mform with mparam
   endif
else
   do &mprg
endif
*do form a1138aa
*do form a1148
*do form c2021
*do form s209
*do form a1110
*do form a1120
*do form docnum
