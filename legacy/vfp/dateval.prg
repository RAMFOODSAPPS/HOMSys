* dateval.prg
* asdbellen 06/08/2017
* allows only 30 days late and 30 days advance from the p_compdate (comparison date)
param p_date,p_datecap,p_compdate,p_rangefr,p_rangeto
pretval = ""
*if empty(p_date)
*   pretval = pretval + "Empty " + p_datecap + chr(13)
*else
if .not. empty(p_date)
   pdiff = p_compdate - p_date
   *wait wind str(pdiff)
   if between(pdiff,p_rangefr,p_rangeto) 
      * okay
   else
      pretval = pretval + "Erroneous value of " + dtoc(p_date) + " for " + p_datecap ;
         + ". System only allows " + allt(str(abs(p_rangefr))) + " days late or " + allt(str(abs(p_rangeto))) + " days advance from " + dtoc(p_compdate) + "."
   endif
endif
return pretval
