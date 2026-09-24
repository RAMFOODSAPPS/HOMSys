* datechk.prg
* check date validity
* vaescaro 11/05/2001
param x_docdate,x_sysdate
local retvalue
retvalue=""
do case
   case month(x_docdate) = month(x_sysdate) .and. year(x_docdate) = year(x_sysdate) 
      retvalue = "1-Within the Month"
   case x_docdate > eom(x_sysdate) .and. x_docdate <= eom(gomonth(x_sysdate,1))   
      retvalue = "2-Next Month Transaction from "+dtoc(x_sysdate)      
   case x_docdate <= (x_sysdate - day(x_sysdate))
      retvalue = "3-Prior Month from "+dtoc(x_sysdate)
   case x_docdate > eom(gomonth(x_sysdate,1))
      retvalue = "4-More Than 2 Months from "+dtoc(x_sysdate)
endcase
return retvalue      