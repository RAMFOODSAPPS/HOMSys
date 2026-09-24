* datechk2.prg
* check date validity
* vaescaro 06/30/2003
param x_docdate,x_sysdate
* retvalue  1 = ok
* retvallue 2 = for confirmation - early date
* retvalue  3 = future date - do not accept
local retvalue
if x_docdate <= x_sysdate .and. x_sysdate - x_docdate<30 .and. x_sysdate - x_docdate > 0
  retvalue="1"
else
   if x_docdate = x_sysdate
      retvalue="1"
   else
      retvalue = iif(x_docdate > x_sysdate,"3","2")     
   endif
endif
return retvalue
