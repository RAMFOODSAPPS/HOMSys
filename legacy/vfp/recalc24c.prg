* recalc24b.prg
* recalculate ending balance and allocated quantity from oowk
* up to specified date
* param is file and target date
param xeif,xdate
closeprod2=.f.
closehdr2=.f.
closedet2=.f.
CLOSESYSPARAM=.F.
closeinactive = .f. 
IF .NOT. USED("SYSPARAM")
   USE SYSPARAM IN 0 SHARED
   CLOSESYSPARAM=.T.
ENDIF
if .not. used("prod4win")
   use prod4win in 0 shared
   closeprod2=.t.
endif

if .not. used("oowkhdr")
   use oowkhdr in 0 shared
   closehdr2=.t.
endif   
if .not. used("oowkdet")
   use oowkdet in 0 shared
   closedet2=.t.
endif   
if .not. used("inactive")
   use inactive in 0 shared
   closeinactive=.t.
endif   

temppr2 = newname("dbf")
tempinc2 = newname("dbf")
indxpr = newname("idx")
sele inactive
copy to &tempinc2

sele prod4win
set order to cprodno
copy to &temppr2
use &temppr2 alias temppr2 in 0 exclu
sele temppr2
append from &tempinc2
erase &tempinc2

sele temppr2
index on cprodno to &indxpr


idx311 = newname("311")
sele (xeif)    
repl all alloccs with 0,allocpc with 0
repl all qtycs with begcs,qtypc with begpc
*index on cprodno+str(class) to &idx311
    
sele oowkdet
set order to docno
set rela to cprodno into temppr2

sele (xeif)
*repl all tohcs with begcs+rrcs+othincs - (salescs + othiscs)
*repl all tohpc with begpc+rrpc+othinpc - (salespc + othispc)

sele oowkhdr
set order to docno
set filter to substr(status,1,1) $ "23456789" .and. .not. ispsuedoso and not extwhse
go top
** PROCESS ALLOCATED ONLY IF CURRENT MONTH
IF MONTH(XDATE)=MONTH(SYSPARAM.TRANSDATE) .AND. YEAR(XDATE)=YEAR(SYSPARAM.TRANSDATE)
  
do while .not. eof()
    mmdocno=docno
    sele oowkdet
    seek mmdocno
    do while docno=mmdocno .and. .not. eof()
    
       wait window "Recalculating S.O. allocated for prodno "+cprodno nowait
       mqtycs = qtycs
       mqtypc = qtypc
       msupplier=temppr2.supplier
       mcprodno=cprodno
       mclass= iif(oowkdet.class=0,1,oowkdet.class)     &&class        && class in for rsta
       mpieces=temppr2.pieces
       sele (xeif)
       loca for cprodno=mcprodno .and. class=mclass
       *repl qtycs with qtycs - mqtycs,qtypc with qtypc - mqtypc
       repl alloccs with alloccs + mqtycs, allocpc with allocpc + mqtypc
       *m=depcs("qtycs","qtypc",mpieces)
       m=depcs("alloccs","allocpc",mpieces)
       sele oowkdet
       skip
    enddo
    sele oowkhdr
    skip
 enddo
   sele (xeif)
   go top
   do while .not. eof()
      mcprodno = cprodno
      sele temppr2
      seek mcprodno
      
      sele (xeif)
      repl qtycs with tohcs - alloccs, qtypc with tohpc - allocpc
      m=depcs("qtycs","qtypc",temppr2.pieces)
      sele (xeif)
      skip
   enddo

ENDIF     && IF MONTH(XDATE)

if closeprod2
   m=closedbf("prod4win")
endif
if closehdr2
   m=closedbf("oowkhdr")
endif   
if closedet2
   m=closedbf("oowkdet")
endif   
IF CLOSESYSPARAM
   M=CLOSEDBF("SYSPARAM")
ENDIF   
if closeinactive && acastillano 07/27/2020
   M=CLOSEDBF("inactive")
endif
m = delfile("temppr2")
sele (xeif)
set index to
erase &idx311
