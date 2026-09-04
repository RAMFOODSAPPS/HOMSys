* getprog.prg
* get source program
param msys16,origcaption
local mloc,mscreen
*fullpath
*mloc = at(":",msys16)
*mfullpath = substr(msys16 ,mloc-1)
*mloc = at(".",mfullpath)
*mloc1 = mloc
*do while mloc >0
*   mloc = mloc-1
*   if substr(mfullpath,mloc,1)="\"
*      mloc=mloc+1
*      exit
*   endif
*enddo   
*mloc = right("\",msys16)
*return substr(mfullpath,mloc,mloc1-mloc)+" - "+origcaption
mloc = len(msys16)
mscreen =""
do while mloc>0
   mscreen = substr(msys16,mloc,1) + mscreen
   mloc = mloc-1
  if substr(msys16,mloc,1)="\"
      exit
  endif
enddo
** remove extension
mloc = at(".",mscreen)
if mloc>1
   mscreen = substr(mscreen,1,mloc-1)
endif   

return mscreen+" - "+origcaption
