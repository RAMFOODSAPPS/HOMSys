* docnum.prg
* udf to save/get document number
* vaescaro 05/26/99
param xdoctype,xdocno,maction
xsele=sele()
xclose=.f.
if .not. used("docnum")
   use docnum in 0 shared
   xclose=.t.
endif
sele docnum
do case
   case upper(maction) = "GETSAVE"
      loca for upper(doctype)=upper(xdoctype)
      if .not. found()
         append blank
         repl doctype with upper(xdoctype)
      endif
      do while .t.
         wait window "Attempting to lock Document No. series" nowait
         mlock = rlock()
         if mlock         
            exit
         endif
      enddo
      REPL LASTNUM WITH LASTNUM+1
      MRETVAL=LASTNUM
      UNLOCK
      WAIT WINDOW "Record Unlocked" nowait
   case upper(maction)="SAVE"
      loca for upper(doctype)=upper(xdoctype)
      if .not. found()
         append blank
         repl doctype with upper(xdoctype)
      endif
      if xdocno>lastnum
         repl lastnum with xdocno
      endif
      mretval=xdocno
   otherwise     && 'GET' param
      loca for upper(doctype)=upper(xdoctype)
      mretval=lastnum
endcase
if xclose
   use
endif
sele (xsele)
return mretval      