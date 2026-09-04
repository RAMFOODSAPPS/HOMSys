* program : prgnamer.prg
* by      : adj
* set     : general utility
* end     : get file name
para xmdy,xdate
xmdy =ltrim(rtrim(lower(xmdy)))
xname=""
xctr =0
do while xctr<len(xmdy)
   xctr=xctr+1
   xlet=subs(xmdy,xctr,1)
   do case
   case xlet$"m"
      xname=xname+subs(str(month(xdate)+100,3),2,2)
   case xlet$"d"
      xname=xname+subs(str(day(xdate)+100,3),2,2)
   case xlet$"y"
      xname=xname+subs(str(year(xdate)+10000,5),4,2)
   case xlet$"zn"
      xname=xname+subs(str(year(xdate)+10000,5),2,4)
   endcase
enddo
return xname
