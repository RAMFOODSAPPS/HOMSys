* rpebusca 02/03/2009
* convert qty into cases/pieces
* mpack shld be in pieces, mpieces=pieces, mid=1(cases) mid=2(pieces)

param mpack,mpieces,mid    
return iif(mid=1,int(mpack/mpieces),mpack-((int(mpack/mpieces))*mpieces) )
