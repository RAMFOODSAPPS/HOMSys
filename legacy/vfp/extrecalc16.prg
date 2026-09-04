* recalc16.prg from recalc14.prg
* 01172009
* recalculate ending balance of inventory file (temporary file) and update DUSCS and DUSPC from clearing (RFD and INVOICE)
* up to specified date
* param is file and target date
*param xeif,xdate,xclude
param xeif,xdate
moutdir = "C:\VFPTEMP\"
if .not. directory(moutdir)
   md &moutdir
endif
temppr2 = newname("dbf")
closeinactive = .f.
m = openfile("bmsvar")
dgetbmsbkpath = "" && acastillano 02/22/2021
sele bmsvar
locate for varname = "BMSBKPATH"
if found()
   dgetbmsbkpath = addbs(varvalue)+"DOSFILES\"
endif
m = closedbf("bmsvar")

if .not. used("inactive")
   use inactive in 0 shared
   closeinactive=.t.
endif   

closeprod = openfile("prod4win")
closehdr = .f.
closedet = .f.

closesysparam = openfile("sysparam")
sele sysparam
paramwhseno = val(bcode)
paramwhnumbers = trim(whnumbers)

*!*	imhdr = getfname(xdate,"IH")
*!*	imdet = getfname(xdate,"ID")
IMHDR = ""
IMDET = ""
meomfl = dgetbmsbkpath + "EO" + alltr(sysparam.bcode) + prgnamer("my", XDATE) + ".zip"
   
closehdr = openfile("imtr2_hdr")
closedet = openfile("imtr2_det")
closeinact = openfile("inactive")

temphdr = newname("HDR")
tempdet = newname("DET")

temppr2 = newname("dbf")
tempinc2 = newname("dbf")
indxpr = newname("idx")

sele inactive && acastillano 07/27/2020
copy to &tempinc2

sele prod4win
set order to cprodno
copy to &temppr2
use &temppr2 alias temppr2 in 0 exclu
sele temppr2
append from &tempinc2
erase &tempinc2
index on cprodno to &indxpr

if month(sysparam.transdate) = month(xdate) .and. year(sysparam.transdate) = year(xdate)

   &&CURRENT MONTH
   sele imtr2_hdr
   copy to &temphdr for posted<= xdate .and. .not. empty(posted)
   sele imtr2_det
   copy to  &tempdet
   use &temphdr alias temphdr in 0 exclusive
   use &tempdet alias tempdet in 0 exclusive  
else
   if file(meomfl)
      wait wind "Accessing EOM " + meomfl nowait
      IMHDR = renfile(XDATE,"IH2",VAL(sysparam.bcode))
      IMDET = renfile(XDATE,"ID2",VAL(sysparam.bcode)) 
   
      temphdr2 = moutdir + IMHDR
      tempdet2 = moutdir + IMDET  

      if file("&temphdr2") 
         erase &tempdet2
         erase &temphdr2
      endif
      mtofile = meomfl +" -o" + moutdir + " " + IMHDR + " " + IMDET
       
      run 7z x &mtofile
      IMHDR = moutdir + IMHDR
      IMDET = moutdir + IMDET 
   endif
  
   if file("&imhdr") .and. file("&imdet")
* use renamed imtr file if they exist otherwise use current month file
      sele imtr2_hdr
      copy to &temphdr stru
      sele imtr2_det
      copy to &tempdet stru
      use &temphdr alias temphdr in 0 exclusive
      use &tempdet alias tempdet in 0 exclusive
      if file("&IMHDR")
         sele temphdr
         append from &imhdr
      endif
      if file("&IMDET")
         sele tempdet
         append from &imdet
      endif
   else
      sele imtr2_hdr
      copy to &temphdr stru
      sele imtr2_det
      copy to &tempdet stru
      use &temphdr alias temphdr in 0 exclusive
      use &tempdet alias tempdet in 0 exclusive
   endif
endif

idx311 = newname("311")

sele (xeif)
if type("dusinvcs") = "U"
   alter table (xeif) add dusinvcs n(9,2) add dusinvpc n(9,2) add dusrfdcs n(9,2) add dusrfdpc n(9,2) add dusrfdlose n(9,2)
endif
if type("dusrfdlose") = "U"
   alter table (xeif) add dusrfdlose n(9,2)
endif
repl all dusinvcs with 0, dusinvpc with 0, dusrfdcs with 0, dusrfdpc with 0, dusrfdlose with 0
repl all qtycs with begcs,qtypc with begpc
repl all salescs with 0,salespc with 0,othiscs with 0,othispc with 0
repl all rrcs with 0,rrpc with 0,othincs with 0,othinpc with 0
repl all tohcs with 0,tohpc with 0,alloccs with 0,allocpc with 0
*brow
if type("TOHLOOSE")="N"
   repl all inloose with 0,outloose with 0,tohloose with begloose
endif
*index on cprodno+str(class) to &idx311

midxdet = newname("XD1")
midxhdr = newname("XH1")

temprsta = newname("dbf")
sele tempdet
alter table &tempdet add ec n(1)
repl ec with 2 for trim(doctype) $ "INVOICE,STN,RTS,MIS,MISS,STAO,PTAO,STMW"
repl ec with 1 for ec = 0

copy to &temprsta for doctype = "RSTA"
use &temprsta alias temprsta in 0 excl
sele temprsta
repl all class with classout, ec with 2

sele tempdet
appe from &temprsta
m = delfile("temprsta")

sele tempdet
index on doctype+str(docno,pub_docnosize) to  &midxdet
*set order to doctype
set rela to cprodno into temppr2
*************
sele temphdr
repl all servewh with val(sysparam.bcode)

index on doctype+str(docno,pub_docnosize) to &midxhdr
set filter to posted <= xdate .and. .not. empty(posted)

mctr=0
go top
do while .not. eof()
    mctr=mctr+1
    mpostdate=posted
    mdoctype=doctype
    mdocno=docno
    mkey=mdoctype+str(mdocno,pub_docnosize)
    mdocclass = docclass
************************
    servingwhseno=0
    if type("SERVEWH")="N"
       servingwhseno=servewh
    endif
    oktocompute=.t.
    sele tempdet
    seek mkey
    do while doctype=mdoctype .and. docno=mdocno .and. .not. eof() .and. oktocompute
       if class = 3 .or. class = 2
          skip
          loop
       endif
       wait window "Recalculating Inventory for prodno "+cprodno+" posted "+dtoc(mpostdate) nowait

***********
       mqtycs = qtycs
       mqtypc = qtypc
       mqq = qtycs
       mpp = qtypc
       if type("LOOSEPC")="N"
          mloose = loosepc
          mqloose = loosepc
       else
          mloose = 0
          mqloose = 0
       endif
       mkey = cprodno+str(class,1)
       msupplier = temppr2.supplier
       mcprodno = cprodno
       mclass = class        && class in for rsta
       mclassout = classout
       mpieces = temppr2.pieces
       muc = uc
       mec = ec
       if ec <> 1
           mqtycs = 0-qtycs
           mqtypc = 0-qtypc
           mloose = mloose* -1
       endif

       sele (xeif)
       loca for cprodno=mcprodno .and. class=mclass
*       seek mkey
       if .not. found()
          append blank
          repl cprodno with mcprodno,class with mclass,supplier with msupplier
       endif
       repl qtycs with qtycs + mqtycs,qtypc with qtypc + mqtypc
       if mdocclass = "1" && DUS Clearing
          if mdoctype = "INVOICE"
             repl dusinvcs with dusinvcs + abs(mqtycs), dusinvpc with dusinvpc + abs(mqtypc)
          else
             repl dusrfdcs with dusrfdcs + abs(mqtycs), dusrfdpc with dusrfdpc + abs(mqtypc), dusrfdlose with dusrfdlose + abs(mloose)
          endif
       endif
       if type("TOHLOOSE")="N"
          repl tohloose with tohloose+ mloose
       endif
       m=depcs("qtycs","qtypc",mpieces)
       m=depcs("dusinvcs","dusinvpc",mpieces)
       m=depcs("dusrfdcs","dusrfdpc",mpieces)
** update other fields
       *if trim(mdoctype) $ "INVOICE,STN,RTS,MIS,MISS,STAO,PTAO,"
       if mec <> 1
          if trim(mdoctype)="INVOICE"
             repl salescs with salescs + mqq,salespc with salespc + mpp
             m=depcs("salescs","salespc",mpieces)
          else
             repl othiscs with othiscs + mqq,othispc with othispc + mpp
             m=depcs("othiscs","othispc",mpieces)
             if type("TOHLOOSE")="N"
                repl outloose with outloose+mqloose
             endif
          endif
       else
          if trim(mdoctype)="RR"
             repl rrcs with rrcs + mqq,rrpc with rrpc + mpp
             m=depcs("rrcs","rrpc",mpieces)
          else
             repl othincs with othincs + mqq,othinpc with othinpc + mpp
             m=depcs("othincs","othinpc",mpieces)
             if type("TOHLOOSE")="N"
                repl inloose with inloose+mqloose
             endif
          endif
       endif
       
       if mqtycs+mqtypc>0
          repl uc with muc
       endif
       sele tempdet
       skip
    enddo
    sele temphdr
    skip
 enddo
*wait window "total records press a key processed= "+str(mctr,6) +"press a key"
m=closetab("prod4win",closeprod)
m=closetab("imtr2_hdr",closehdr)
m=closetab("imtr2_det",closedet)
m=closetab("sysparam",closesysparam)
m=closetab("inactive", closeinact)
if closeinactive
   M=CLOSEDBF("inactive")
endif
m=delfile("TEMPHDR")
m=delfile("TEMPDET")
m = delfile("temppr2")
sele (xeif)
set index to
erase &idx311
