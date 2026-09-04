* RENFILE.prg
* get renamed filename 09/20/2001 - vicescaro
param xdate,xprefix,XWH
local mm,yy,mx,XB
XB= iif(XWH<10,"0"+str(XWH,1),str(XWH,2))
mx = month(xdate)
mm= iif(mx <10,"0"+str(mx,1),str(mx,2))
yy = substr(str(year(xdate),4),3,2)
return xprefix+XB+mm+yy+".DBF"
