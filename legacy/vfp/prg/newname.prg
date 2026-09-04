* newname.prg
* get new filename
param mextension
msele = select()
create cursor xxstru (CNAME C(10))
sele xxstru
mdbf=dbf()
use
sele (msele)
return substr(mdbf,1,len(mdbf)-3)+mextension
