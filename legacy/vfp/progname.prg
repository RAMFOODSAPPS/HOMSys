* PROGNAME : return calling program name
* vaescaro 03/23/2007
function progname
local mmprog,m_currentlevel,mmprogname
m_currentlevel = program(-1) -1
mmprogname=""
do while m_currentlevel >0
   mmprog = sys(16,m_currentlevel)   
   if len(mmprogname)>0
      mmprogname = mmprogname+","+nopath(mmprog)   &&program(m_currentlevel)
   else
      mmprogname= nopath(mmprog)
   endif
   m_currentlevel=m_currentlevel-1
enddo   
return mmprogname
