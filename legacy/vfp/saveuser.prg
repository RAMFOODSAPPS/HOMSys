* saveuser
param mactivity
xsele=sele()
*dcloseuser=.f.
*if .not. used("userlog")
*   use userlog in 0 shared
*   dcloseuser=.t.
*endif
*sele userlog
*append blank
*repl date with date(),time with time(),activity with mactivity
*if type("appusername")="C"
*   repl username with appusername
*endif   
*if dcloseuser
*   use
*endif
*sele (xsele)
return .t.