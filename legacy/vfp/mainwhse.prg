param p_wh, p_branch
mretwh = 0
sele (p_branch)
set order to whseno
seek p_wh
if found()
   mretwh = p_wh
else
   locate for str(p_wh,2) $ allt(slwhseno)
   if found()
      mretwh = whseno
   endif
endif
return mretwh