* termdays - 07/31/2001 - return termcode in days
param xterm
local xdays
xdays = xterm
DO CASE
   CASE xTERM = 21
      xDAYS = 15
   CASE xTERM = 31
      xDAYS = 30
   CASE xTERM = 10
      xDAYS = 7
   CASE xTERM <= 19
      xDAYS=0
ENDCASE   
return xdays