* program name : CLOSE.PRG
* program desc : udf program to close open file
* ECEbusca 03/17/2000
*
param malias
mused=.f.
if !used("&malias")
   use &malias in 0 share
   mused=.t.
endif
sele &malias
return mused