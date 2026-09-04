* openxclu.prg
param xxfile
errexclu=.f.
on error do errvic with error()
use (xxfile) in 0 exclusive
on error
return .not. errexclu

