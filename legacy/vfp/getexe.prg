* getexe.prg
* udf to identify exe name
mretval = ""
mval = upper(progname())
mlen = len(mval)
do while mlen <> 0
   mchar = substr(mval,mlen,1)
   if mchar <> ","
      mretval = mchar + mretval
   else
      exit
   endif
   mlen = mlen - 1
enddo
return mretval