* nopath.prg  return filename without path
* vaescaro 03/23/2007
param this_file
local mlen,mthisfile
mthisfile = alltrim(this_file)
mloc = len(mthisfile)
do while mloc>0
   if substr(mthisfile,mloc,1)="\"
      exit
   endif
   mloc=mloc-1
enddo
return substr(mthisfile,mloc+1)
   

