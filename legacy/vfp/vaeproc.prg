* Program Name : vaeproc.prg
* Description  : UDFS-for visual foxpro
* Programmer   : Victor A. Escaro
* Date         : 08/01/98
*
PROCEDURE RGWSALES
* program name : rgwsales.prg
* create sales file from rgw hdr, rgwdet
param XXDATE,XXWH 
MHDR = RENFILE(XXDATE,"JH",XXWH)
MDET = RENFILE(XXDATE,"JD",XXWH)

**mhdr,mdet
IF FILE("&MHDR") .AND. FILE("&MDET")
   *
ELSE   
   mhdr = "RGWHDR.DBF"
   mdet = "RGWDET.DBF"
endif

sale4RGW = newname("d21")
temphdr = newname("db1")
tempdet = newname("db2")
midxhdr = newname("xx1")
midxdet = newname("xdet")
xclose1= openfile("sale4win")
sele sale4win
copy to &sale4RGW stru
m = closetab("SALE4WIN",xclose1)
use &sale4RGW alias sale4RGW in 0 exclusive


use &mhdr alias mhdr in 0 shared
use &mdet alias mdet in 0 shared
sele mhdr
copy to &temphdr for doctype="RGW"
use
sele mdet
copy to &tempdet for doctype="RGW" .and. qtycs + qtypc<>0
use
**************************
xclosecust4win = openfile("cust4win")
sele cust4win
set order to whckey
sele sale4RGW
set rela to str(whseno,2)+ckey into cust4win


use &temphdr alias temphdr in 0 exclusive
use &tempdet alias tempdet in 0 exclusive
sele temphdr
index on docno to &midxhdr
sele tempdet
index on docno to &midxdet
set rela to docno into temphdr
mrec=reccount()
go top
do while .not. eof()
   mrec = mrec-1
   wait window "Creating Sales Records from RGW "+str(mrec,6) nowait
   if .not. empty(temphdr.posted)
      sele sale4RGW
      **** ADDITION TO SALES OF DUS/PMS TRTYPE 2
      appe blank 
      repl trdate with temphdr.posted,ckey with temphdr.ckey,whseno with temphdr.whseno,;
        csman with temphdr.csman, custno with val(substr(ckey,1,4)),custnosfx with val(substr(ckey,5,1))
      repl salesman with val(csman), prodno with val(tempdet.cprodno),cprodno with tempdet.cprodno,;
        supplier with tempdet.supplier,  csupplier with str(tempdet.supplier,2)
      repl qtycs with tempdet.qtycs,qtypc with tempdet.qtypc,netamt with tempdet.netamt
      repl cost with tempdet.amt,amt with tempdet.netamt,stax with tempdet.sptax ,term with temphdr.term
      repl docno with tempdet.docno,docid with 84,docdate with temphdr.docdate
      repl classcode with cust4win.classcode ,trtype with 2,EMPNO WITH temphdr.EMPNO
      mprefix = iif(whseno<40,"1","2")
      repl cusname with cust4win.cusname,ccno with val(mprefix + str(whseno,2)+"06")
      ****** DEDUCTION TO JOBBER TRTYPE 1
      appe blank 
      repl trdate with temphdr.posted,ckey with temphdr.ckey,whseno with temphdr.whseno,;
        csman with temphdr.csman, custno with val(substr(ckey,1,4)),custnosfx with val(substr(ckey,5,1))
      repl salesman with val(csman), prodno with val(tempdet.cprodno),cprodno with tempdet.cprodno,;
        supplier with tempdet.supplier,  csupplier with str(tempdet.supplier,2)
      repl qtycs with tempdet.qtycs,qtypc with tempdet.qtypc,netamt with tempdet.netamt
      repl cost with tempdet.amt,amt with tempdet.netamt,stax with tempdet.sptax ,term with temphdr.term
      repl docno with tempdet.docno,docid with 84,docdate with temphdr.docdate
      repl classcode with cust4win.classcode ,trtype with 1,EMPNO WITH temphdr.EMPNO
      mprefix = iif(whseno<40,"1","2")
      repl cusname with cust4win.cusname,ccno with val(mprefix + str(whseno,2)+"06")
      REPL QTYCS WITH 0-QTYCS,QTYPC WITH 0-QTYPC,AMT WITH 0-AMT,STAX WITH 0-STAX,COST WITH 0-COST
      REPL NETAMT WITH 0-NETAMT
      REPL CSMAN WITH CUST4WIN.CSMAN,SALESMAN WITH VAL(CUST4WIN.CSMAN)    && SALESMAN ASSIGNED TO JOBBER
      
   endif
   sele tempdet
   skip
enddo
sele tempdet
set rela to
m = delfile("temphdr")
m = delfile("tempdet")
m = closetab("CUST4WIN",xclosecust4win)
sele SALE4RGW
   

procedure amt2wrds
* PROGRAM NAME : AMT2WRDS.PRG
* udf to CONVERT NUMERIC AMOUNT INTO WORDS
*
*INPUT "NUMBER TO BE CONVERTED " TO CTEMP
param ctemp
********"+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------
private xones,xtens,xodds,xgroups,xtemp,xctr,cwork,xdigit1,xdigit2,xdigit3
xones = "ONE       TWO       THREE     FOUR      FIVE      SIX       SEVEN     EIGHT     NINE      "
xtens = "TEN       TWENTY    THIRTY    FORTY     FIFTY     SIXTY     SEVENTY   EIGHTY    NINETY    "
xodds = "ELEVEN    TWELVE    THIRTEEN  FOURTEEN  FIFTEEN   SIXTEEN   SEVENTEEN EIGHTEEN  NINETEEN  "
xgroups="TRILLION  BILLION   MILLION   THOUSAND  "
xtemp = str(ctemp,18,2)
if val(substr(xtemp,1,15)) = 0
   cwork = " ZERO "
   xctr = 6
else
   cwork = " "
   xctr = 1
endif
do while xctr <= 5
   xset = substr(xtemp,3*xctr-2,3)
   if val(xset) = 0
      xctr = xctr+1
      loop
   endif
   xdigit1 = val(substr(xset,1,1))
   xdigit2 = val(substr(xset,2,1))
   xdigit3 = val(substr(xset,3,1))
   if xdigit1 <> 0  
      cwork = cwork + trim(substr(xones,10*xdigit1-9,10))+" HUNDRED "
   endif
   do case
   case xdigit2=0 .and. xdigit3 = 0
      * DO NOTHING
   case xdigit2 = 0 .and. xdigit3 <> 0
      * VALUE IS 01-09
      cwork = cwork + trim(substr(xones,10*xdigit3-9,10))+" "
   case xdigit2 = 1 .and. xdigit3 <> 0
      * VALUE IS 11 - 19
      cwork = cwork + trim(substr(xodds,10*xdigit3-9,10))+" "
   case 10*xdigit2+xdigit3 = 10*xdigit2
      * VALUE IS 10, 20, 30 ...
      cwork = cwork + trim(substr(xtens,10*xdigit2-9,10))+" "
   otherwise
      * VALUE IS 21-29,31-39....
      cwork = cwork + trim(substr(xtens,10*xdigit2-9,10))+" "
      cwork = cwork + trim(substr(xones,10*xdigit3-9,10))+" "
   endcase
   if xctr <> 5
      cwork = cwork + trim(substr(xgroups,10*xctr-9,10))+" "
   endif
   xctr = xctr+1
enddo
return substr(cwork,2)+"PESOS AND "+substr(xtemp,17,2)+"/100"


**********************************************************************
*                              CHANGEMO                              *
**********************************************************************
PROCEDURE  CHANGEMO

* program name : changemo.prg
* udf change month ( add 1 month)
* vaescaro 05/02/94
param monthadd,changeval
private xxx
xxx=monthadd
do while month(monthadd)=month(xxx)
   monthadd = monthadd + changeval
enddo
return monthadd


**********************************************************************
*                              CHARDATE                              *
**********************************************************************
PROCEDURE  CHARDATE

* program name : chardate.prg
* return character date 11/17/97
*
param xdate
return cmonth(xdate)+" "+ltrim(str(day(xdate),2))+", "+str(year(xdate),4)


**********************************************************************
*                              DATEFILE                              *
**********************************************************************
PROCEDURE  DATEFILE

* program name : datefile.prg
* udf to return renamed file per month/year
* 11/13/97
param xprefix,xdate
vtmp1=month(xdate)
vtmp = iif(vtmp1<10,"0"+str(vtmp1,1),str(vtmp1,2))
return xprefix+vtmp+str(year(xdate),4)+".DBF"


**********************************************************************
*                              DECRYPT                               *
**********************************************************************
PROCEDURE  DECRYPT

* program name : decrypt.prg
* udf to decrypt passwords
* Victor A. Escaro 08/06/97
*
param mpass
private mctr,mchar,mcode,mnum,mtext
mctr=0
mtext=""
do while mctr<len(mpass)
   mctr=mctr+1
   mchar = substr(mpass,mctr,1)
   mnum = asc(mchar)
   mcode = chr(mnum-mctr-60)
   mtext=mtext + mcode
enddo
return mtext


**********************************************************************
*                              DELETAG                               *
**********************************************************************
PROCEDURE  DELETAG

* program name : deletag.prg
* return char indicator for selected record
* vaescaro 11/13/97
return iif(deleted(),"-","û")

**********************************************************************
*                              DELETAG2                              *
**********************************************************************
PROCEDURE  DELETAG2

* program name : deletag.prg
* return char indicator for selected record
* vaescaro 11/13/97
return iif(deleted(),"û","-")

**********************************************************************
*                              ENCRYPT                               *
**********************************************************************
PROCEDURE  ENCRYPT

* program name : encrypt.prg
* udf to encrypt text for passwords
* Victor A. Escaro 08/06/97
*
param mtext
private mctr,mchar,mcode,mnum
mctr=0
mpass=""
do while mctr<len(mtext)
   mctr=mctr+1
   mchar = substr(mtext,mctr,1)
   mnum = asc(mchar)
   mcode = chr(mnum+mctr+60)
   mpass = mpass + mcode
enddo
return mpass


**********************************************************************
*                                EOM                                 *
**********************************************************************
PROCEDURE  EOM

* program name : eom.prg
* udf to return end of month date
* vaescaro 06/03/94
param xxxdate
private xsetdate,xdate2,xdate1
if empty(xxxdate)
   xxxdate=date()
endif   
xsetdate = set("DATE")
set date american
xdate2 = xxxdate
if day(xxxdate)<28
   xdate1 = dtoc(xxxdate)
   xdate2 = ctod(substr(xdate1,1,2)+"/28/"+str(year(xxxdate),4))
endif
do while month(xxxdate)=month(xdate2)
   xdate2 = xdate2 + 1
enddo
set date &xsetdate
return xdate2-1


procedure numbers
* program name : numbers.prg
* udf to return all digits in a given variable
* Victor A. Escaro 06/14/97
*
param xData
private xNumber,xReturn,xCtr,xChar
xNumber = trim(ltrim(xData))
xReturn=""
xCtr=0
do while xCtr < len(xNumber)
   xCtr = xCtr+1
   xChar= substr(xNumber,xCtr,1)
   if xChar $ "0123456789"
      Xreturn=xReturn+xChar
   endif
enddo
return xReturn


**********************************************************************
*                              PERCENT                               *
**********************************************************************
PROCEDURE  PERCENT

* percent.prg
param mdividend,mdivisor
return iif(mdivisor=0,0.00,mdividend/mdivisor*100)

PROCEDURE  QOUTIENT
param mdividend,mdivisor
return iif(mdivisor=0,0.00,mdividend/mdivisor)


procedure popkey
* popkey.prg
i=inkey()
do while i=0
   i=inkey()
enddo
return i


procedure sertod
* program name : sertod.prg
* udf to convert serial to data
* Victor A. Escaro 02/06/97
*
param xserial
return  ctod("01/01/1980")+xserial
* Nothing follows..
procedure mmdd
* mmdd.prg
param xdate
return substr(dtoc(xdate),1,5)


procedure dtoser
* program name : dtoser.prg
* udf to convert date to serial no.
* Victor A. Escaro 02/06/97
*
param xdate
return  xdate-ctod("01/01/1980")
* Nothing follows..


procedure firstday
* program name : firstday.prg
* return first day of the month
* 05/03/98
param xd
*private msetdate,xdate2
*msetdate=SET("DATE")
*set date american
*xdate2 =  ctod(str(month(xd),2)+"/01/"+str(year(xd),4))
*set date &msetdate
return (xd - day(xd))+1

   procedure testmode
   on key label F5 activate window trace
   on key label F6 activate window debug
   on key label F7 set sysmenu to default
   on key label f8 clear events
   endproc
   
   procedure SaveEnv
   param apprefix
   gecBell = set("BELL")
   gecCentury = set("CENTURY")
   gecConfirm = set("CONFIRM")
   gecDate = set("DATE")
   gecDecimals = set("DECIMALS")
   gecDeleted = set("DELETED")
   gecDirectory = set("DIRECTORY")
   gecExact = set("EXACT")
   gecExclusive = set("EXCLUSIVE")
   gecFdow = set("Fdow")
   gecFixed = set("Fixed")
   gecFweek = set("FWEEK")
   gecHours = set("HOURS")
   gecLock = set("LOCK")
   gecMultilocks = set("MULTILOCKS")
   gecNear = set("NEAR")
   gecRefresh = set("REFRESH")
   gecSafety = set("SAFETY")
   gecSeconds = set("SECONDS")
   gecStatus = set("STATUS BAR")
   gecSticky = set("STICKY")
   gecTalk = set("TALK")
   pcEnvFile = Apprefix+"ENV"
   save all like gec* to (pcEnvFile)
   endproc
   
   procedure SetEnv
   set bell off
   set century on
   set confirm off
   set date American
*   set decimals to 2
   set deleted on
   set exact off
   set exclusive off
   set fdow to 2
   set fixed on
   set fweek to 1
   set hours to 12
   set lock off
   set multilocks on
   set near off
   set refresh to 60
   set reprocess to 5 seconds
   set safety off
   set seconds off
   set status bar off
   set sticky on
   set talk off
   endproc
   
   procedure RestoreEnv
   param apprefix
   on key label F5
   on key label F6
   on key label F7
   on key label F8
   set sysmenu to default
   
   pcEnvFile = Apprefix+"ENV"
   restore from (pcEnvFile) Additive
   set bell &gecbell
   set century &gecCentury
   set confirm &gecCOnfirm
   set date &gecDate
   set decimals to &gecDecimals
   set deleted &gecDeleted
   set exact &gecExact
   set exclusive &gecExclusive
   set fdow to &gecfdow
   set fixed &gecfixed
   set fweek to &gecfweek
   set hours to &gechours
   set lock &geclock
   set multilocks &gecmultilocks
   set near &gecnear
   set refresh to gecRefresh
   set reprocess to 0
   set safety &gecsafety
   set seconds &gecseconds
   set status bar &gecstatus
   set sticky on &gecsticky
   set talk off &gectalk
   endproc
   

procedure ordinal
* program name : ordinal.prg
* get ordinal number
param xnumber
private xdesc,xlastnum,xint
xlastnum = substr(str(xnumber,11),11,1)
xint = ltrim(str(xnumber,11))
do case
   case xnumber=0
      xdesc=""
   case xnumber=11 .or. xnumber=12 .or. xnumber=13
      xdesc= xint+"th"
   case xlastnum="1"
      xdesc= xint+"st"
   case xlastnum="2"
      xdesc= xint+"nd"
   case xlastnum="3"
      xdesc=xint+"rd"
   otherwise
      xdesc= xint+"th"
endcase
return xdesc

procedure numword
* PROGRAM NAME : numword.PRG
* udf to CONVERT NUMERIC AMOUNT INTO WORDS
*
*INPUT "NUMBER TO BE CONVERTED " TO CTEMP
param ctemp
********"+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------
private xones,xtens,xodds,xgroups,xtemp,xctr,cwork,xdigit1,xdigit2,xdigit3
xones = "ONE       TWO       THREE     FOUR      FIVE      SIX       SEVEN     EIGHT     NINE      "
xtens = "TEN       TWENTY    THIRTY    FOURTY    FIFTY     SIXTY     SEVENTY   EIGHTY    NINETY    "
xodds = "ELEVEN    TWELVE    THIRTEEN  FOURTEEN  FIFTEEN   SIXTEEN   SEVENTEEN EIGHTEEN  NINETEEN  "
xgroups="TRILLION  BILLION   MILLION   THOUSAND  "
xtemp = str(ctemp,18,2)
if val(substr(xtemp,1,15)) = 0
   cwork = " ZERO "
   xctr = 6
else
   cwork = " "
   xctr = 1
endif
do while xctr <= 5
   xset = substr(xtemp,3*xctr-2,3)
   if val(xset) = 0
      xctr = xctr+1
      loop
   endif
   xdigit1 = val(substr(xset,1,1))
   xdigit2 = val(substr(xset,2,1))
   xdigit3 = val(substr(xset,3,1))
   if xdigit1 <> 0  
      cwork = cwork + trim(substr(xones,10*xdigit1-9,10))+" HUNDRED "
   endif
   do case
   case xdigit2=0 .and. xdigit3 = 0
      * DO NOTHING
   case xdigit2 = 0 .and. xdigit3 <> 0
      * VALUE IS 01-09
      cwork = cwork + trim(substr(xones,10*xdigit3-9,10))+" "
   case xdigit2 = 1 .and. xdigit3 <> 0
      * VALUE IS 11 - 19
      cwork = cwork + trim(substr(xodds,10*xdigit3-9,10))+" "
   case 10*xdigit2+xdigit3 = 10*xdigit2
      * VALUE IS 10, 20, 30 ...
      cwork = cwork + trim(substr(xtens,10*xdigit2-9,10))+" "
   otherwise
      * VALUE IS 21-29,31-39....
      cwork = cwork + trim(substr(xtens,10*xdigit2-9,10))+" "
      cwork = cwork + trim(substr(xones,10*xdigit3-9,10))+" "
   endcase
   if xctr <> 5
      cwork = cwork + trim(substr(xgroups,10*xctr-9,10))+" "
   endif
   xctr = xctr+1
enddo

mm= substr(cwork,2)        &&+"PESOS AND "+substr(xtemp,17,2)+"/100"
return trim(mm)

procedure charyear
* program name : charyear.prg
* character year
param xdate
myear= year(xdate)
firsttwo = val(substr(str(myear,4),1,2))
lasttwo = val(substr(str(myear,4),3,2))

mretval =  proper(numword(firsttwo))+" Hundred"
if lasttwo> 0
   mretval2 = proper(numword(lasttwo))
   mretval = mretval + " and " +mretval2
endif   
return mretval
      

procedure closedbf
param xfile
mclose=.f.
if used(xfile)
   sele (xfile)
   use
   mclose=.t.
endif
return mclose


procedure errvic
* program name : errvic.prg
* error handler
*
*ON ERROR DO errhand WITH ;
*	ERROR( ), MESSAGE( ), MESSAGE(1), PROGRAM( ), LINENO( )
*** The next line should cause an error ***
*USE nodatabase
*ON ERROR  && restore system error handler
*PROCEDURE errhand
PARAMETER merror
errexclu=.t.
*wait window "with error"
do case
   case merror = 1705    && file access is denied
      errexclu = .t.
   case merror = 1881    && error while loading data environment
      errexclu = .t.
   CASE MERROR = 1562    && CANNOT FIND OBJECT  ______ IN DATABASE
      errexclu = .t.
   CASE MERROR = 1537    && CANNOT ADD THIS TABLE IT BELONGS TO DATABASE ____
      errexclu = .t.
endcase
return


procedure dmin
* dmin function
param xvalue1,xvalue2
return iif(xvalue2>0,min(xvalue1,xvalue2),xvalue1)

PROCEDURE hconvert
PARAM xpack,xpieces,xid    && mpieces=pr->pieces   id=1 (cases)  id=2 (pieces)
RETURN IIF(xid=1,INT(xpack/xpieces),xpack-((INT(xpack/xpieces))*xpieces) )


procedure nexsup
**nexsup
skip 
if !eof()
	nexsup=supplier_A
else
	nexsup=0
endif
skip -1
return nexsup

procedure msrp
    if ADD_ON = 0
       mamt=newprice
    else
       mamt=newprice+ADD_ON
    endif
    mtax=mamt*taxrate
    mvat=mamt+mtax
    mperpc=mvat/pieces
*   msrp=((mvat+(mvat*srp))/pieces)
    msrp=(((mperpc*srp)/100)+mperpc)
    *********** round srp to the nearest .25 cent for cebu
    *** round srp to the nearest .05 cent /AAS 10.29.99
    if mround
       mdecimal = msrp - int(msrp)
       mdec  = str(mdecimal,5,2)
       mdec1=right(mdec,1)
**       MESSAGEBOX(MDEC+"  "+mdec1)
       mdecimal = val(mdec)-val(".0"+mdec1)
       do case
		case val(mdec1)>0 .and. val(mdec1)<=5
			mdecimal=mdecimal+.05
		case val(mdec1)>5
			mdecimal=mdecimal+.10
	   endcase	
*      do case
*         case mdecimal > 0 .and. mdecimal <= 0.05
*            msrp = int(msrp)+0.05
*         case mdecimal >= 0.06 .and. mdecimal <= 0.10
*            msrp = int(msrp) + 0.10
*         case mdecimal >= 0.41 .and. mdecimal <= 0.70
*            msrp = int(msrp) + 0.50
*         case mdecimal >= 0.71 .and. mdecimal <= 0.90
*            msrp = int(msrp) + 0.75
*         case mdecimal >= 0.91 .and. mdecimal <= 0.99
*            msrp = int(msrp) + 1
*      endcase
		msrp = int(msrp) + mdecimal
    endif
    RETURN str(MSRP,8,2)
    ***********

procedure closetab
param xfile,xclose
mclose=.f.
if xclose
   if used(xfile)
      sele (xfile)
      use
      mclose=.t.
   endif
endif
return mclose

procedure delfile
* delfile.prg
* delete file
param malias
mretval=.f.
if used(malias)
   sele (malias)
   mdbf=dbf()
   midx = ndx(1,malias)
   use
   erase &mdbf
   if len(midx)>0
      erase &midx
   endif
   mloc = at(".",mdbf)
   mbak = substr(mdbf,1,mloc)+"bak"
   if file("&mbak")
      erase &mbak
   endif
   
   mretval=.t.
endif
return mretval
   
   
procedure openfile
param malias
mused=.f.
if .not. used("&malias")
   use &malias in 0 share
   mused=.t.
endif
sele &malias
return mused

procedure newname
* newname.prg
* get new filename
param mextension
msele = select()
create cursor xxstru (CNAME C(10))
sele xxstru
mdbf=dbf()
use
sele (msele)
return substr(mdbf,1,len(mdbf)-3)+mextension


procedure amt2usd
* PROGRAM NAME : AMT2WRDS.PRG
* udf to CONVERT NUMERIC AMOUNT INTO WORDS
*
*INPUT "NUMBER TO BE CONVERTED " TO CTEMP
param ctemp
********"+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------+---------
private xones,xtens,xodds,xgroups,xtemp,xctr,cwork,xdigit1,xdigit2,xdigit3
xones = "ONE       TWO       THREE     FOUR      FIVE      SIX       SEVEN     EIGHT     NINE      "
xtens = "TEN       TWENTY    THIRTY    FORTY     FIFTY     SIXTY     SEVENTY   EIGHTY    NINETY    "
xodds = "ELEVEN    TWELVE    THIRTEEN  FOURTEEN  FIFTEEN   SIXTEEN   SEVENTEEN EIGHTEEN  NINETEEN  "
xgroups="TRILLION  BILLION   MILLION   THOUSAND  "
xtemp = str(ctemp,18,2)
if val(substr(xtemp,1,15)) = 0
   cwork = " ZERO "
   xctr = 6
else
   cwork = " "
   xctr = 1
endif
do while xctr <= 5
   xset = substr(xtemp,3*xctr-2,3)
   if val(xset) = 0
      xctr = xctr+1
      loop
   endif
   xdigit1 = val(substr(xset,1,1))
   xdigit2 = val(substr(xset,2,1))
   xdigit3 = val(substr(xset,3,1))
   if xdigit1 <> 0  
      cwork = cwork + trim(substr(xones,10*xdigit1-9,10))+" HUNDRED "
   endif
   do case
   case xdigit2=0 .and. xdigit3 = 0
      * DO NOTHING
   case xdigit2 = 0 .and. xdigit3 <> 0
      * VALUE IS 01-09
      cwork = cwork + trim(substr(xones,10*xdigit3-9,10))+" "
   case xdigit2 = 1 .and. xdigit3 <> 0
      * VALUE IS 11 - 19
      cwork = cwork + trim(substr(xodds,10*xdigit3-9,10))+" "
   case 10*xdigit2+xdigit3 = 10*xdigit2
      * VALUE IS 10, 20, 30 ...
      cwork = cwork + trim(substr(xtens,10*xdigit2-9,10))+" "
   otherwise
      * VALUE IS 21-29,31-39....
      cwork = cwork + trim(substr(xtens,10*xdigit2-9,10))+" "
      cwork = cwork + trim(substr(xones,10*xdigit3-9,10))+" "
   endcase
   if xctr <> 5
      cwork = cwork + trim(substr(xgroups,10*xctr-9,10))+" "
   endif
   xctr = xctr+1
enddo
return substr(cwork,2)+"US DOLLARS AND "+substr(xtemp,17,2)+"/100"


procedure addstru
param mf1,mf2,mf3,mf4
*loca for field_name=mf1
*if .not. found()
   append blank
*endif
repl field_name with mf1,field_type with mf2,field_len with mf3,field_dec with mf4
return
   
procedure updcdx
* updating cdx 
if file("prod4win.dbf")
   if openxclu("prod4win")
      wait window "Updating Index of PROD4WIN.dbf" nowait
      sele prod4win
      index on prodno tag prodno of prod4win
      index on cprodno tag cprodno of prod4win
      index on proddesc tag proddesc of prod4win
      wait window "Updating Index of PROD4WIN.dbf completed" nowait
      m = closedbf("prod4win")
   endif
endif

* repldata
PROCEDURE REPLDATA
param dalias,mfield,mvalue,xkey1,xkey2
msele = sele()

sele (dalias)
mrecno=recno()
xxkey1 = &xkey1
xxkey2 = &xkey2
*mdocno=docno
*mdoctype=doctype
GO TOP
DO WHILE &xkey1 = xxkey1 .and. &xkey2 = xxkey2 .AND. .NOT. EOF()
   repl &mfield with mvalue   && for &xkey1 = xxkey1 .and. &xkey2 = xxkey2
   SKIP
ENDDO   
if mrecno>0
   goto mrecno
else
   go top
endif      
sele (msele)


DEFINE CLASS mycombo AS combobox

   cFieldName  = ""
   cFieldCode  = ""
   cAlias      = ""

   PROCEDURE Init(cFldName, cFldCode, cAls)
      This.cFieldName = cFldName
      This.cFieldCode = cFldCode
      This.cAlias = cAls
   ENDPROC

   PROCEDURE InteractiveChange
      local nIdx, cDesc, cCode

      nIdx  = This.ListIndex
      cDesc = This.Value
      cCode = This.List(nIdx, 2)

      if !empty(cDesc)
         sele (This.cAlias)
         replace (This.cAlias+"."+This.cFieldName) with cDesc
         replace (This.cAlias+"."+This.cFieldCode) with cCode
         ThisForm.Grid1.Refresh()
      endif
   ENDPROC
ENDDEFINE