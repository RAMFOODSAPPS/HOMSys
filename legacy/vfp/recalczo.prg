param mzofile,meff_date

dclose = openfile("prod4win")
sele prod4win
set order to cprodno

sele (mzofile)
mrec = reccount()
go top
do while .not. eof()
   mrec = mrec - 1
   wait wind "Recalculating RATE ADD-ON please wait..." + str(mrec,6) nowait
   mcprodno = cprodno
   mrate = rate
   mfixamt = fixamt
   madd_on = add_on
   
   sele prod4win
   seek mcprodno
   if meff_date < prod4win.from
      mnewprice = prod4win.oldprice1
   else
      mnewprice = prod4win.newprice
   endif
   
   sele (mzofile)
   if mfixamt <> 0 .or. mrate <> 0
      mnewadd_on = round(((mnewprice + mfixamt) * mrate) + mfixamt,2)
      if mnewadd_on <> madd_on
         repl old_add_on with add_on, eff_date with meff_date
      endif
      repl add_on with mnewadd_on
   endif
   
   sele (mzofile)
   skip
enddo

= closetab("prod4win",dclose)

