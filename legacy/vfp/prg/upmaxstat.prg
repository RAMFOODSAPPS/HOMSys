param xistemp, xorigcs, xorigpc, xnewcs, xnewpc, xremarks, xprocessed , xproddesc , xpieces, xso, xckey, xorderdate, xwhseno, xum , xqtyperpc, xusername,xprcsmax,xpodate1,xponum1,xcustkey,xcusname,xcprodno,xdocno,xallmaxcs,xallmaxpc,xissugmax,xsugmxcs  &&gbugarin

*!*	mpodate1 = thisform.txtpodate.value
*!*	mponum1 = thisform.txtponum.value

sele dmaxstat
appe blank
repl docno with xdocno, custkey with xcustkey, cusname with xcusname, cprodno with xcprodno
repl origcs with xorigcs, origpc with xorigpc, newcs with xnewcs, newpc with xnewpc
repl remarks with xremarks, processed with xprocessed, servedcs with xnewcs, pono with xponum1, podate with xpodate1 &&pono with xponum,
repl servedpc with xnewpc, maxqtycs with xnewcs, maxqtypc with xnewpc, prodno with val(xcprodno) && num to cha
repl proddesc with xproddesc, pieces with xpieces, sono with xso, ckey with xckey, orderdate with xorderdate, whseno with xwhseno
repl um with xum, qtyperpc with xqtyperpc, username with xusername, sysdate with sysparam.transdate, prcsmax with xprcsmax
repl bmsversion with sysparam.bmsversion,curmaxcs with xallmaxcs
if xissugmax  && acastillano 08/01/2019
   repl sugmaxcs with xsugmxcs
endif

if xistemp && acastillano 08/29/2019
   sele tempowkd
   repl getmaxcs with xnewcs, getmaxpc with xnewpc for docno = xdocno and cprodno = xcprodno
else
   sele oowkdet && acastillano 08/29/2019
   repla qtycs with xnewcs, qtypc with xnewpc for docno = xdocno and cprodno = xcprodno  
endif

mresmoe = xprocessed

if xprocessed
   sele tempmoe
   appe blank
   repl whseno with xwhseno, ckey with xckey, custkey with xcustkey, cusname with xcusname
   repl pono with xponum1, sono with xdocno, orderdate with xorderdate
   repl prodno with val(xcprodno), cprodno with xcprodno, proddesc with xproddesc, pieces with xpieces
   repl qtyperpc with xqtyperpc, um with xum
   repl origcs with xorigcs, origpc with xorigpc, servedcs with xnewcs, servedpc with xnewpc
   repl maxqtycs with xallmaxcs, maxqtypc with xallmaxpc, sysdate with datetime(), exename with getexe()
   repl bmsversion with fdate(exename), remarks with xremarks
   m = datauser()
endif