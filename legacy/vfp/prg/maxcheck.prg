* maxcheck.prg
* asdbellen 01/29/2014 to update maxodate and to ensure that branch is not re-implementing an outdated version
* asdbellen 01/16/2018 to access maxorder date from motherbms if pod, public var defined in bms.prg
param maction
mretval = .f.

*if file("maxorder.dbf")
if file(pub_dmaxo)
   *mmaxorderdate = fdate("maxorder.dbf")
   mmaxorderdate = fdate(pub_dmaxo)
      
   mclosesysp = openfile("sysparam")
   sele sysparam
   go top
   mcurmaxodate = maxodate 
   if mmaxorderdate > mcurmaxodate
      if maction = 1
         repl maxodate with mmaxorderdate
      endif
   else
      if mmaxorderdate = mcurmaxodate
         mretval = .t.
      endif
   endif
   m = closetab("sysparam",mclosesysp)
endif
   
return mretval