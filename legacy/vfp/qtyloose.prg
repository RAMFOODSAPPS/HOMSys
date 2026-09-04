* qtyloose.prg
* parameter qtycs,qtyper,loosepc,pieces,innerloose,output code
* outcode 1=qtycs,2=qtypc,3=loose,4=all

param L_QTYCS,L_QTYPC,L_LOOSE, L_PIECES,L_INNERLOOSE,L_OUTCODE
local l_quantity,m_cases,totalloose,m_qtypc,m_loose,m_epcs,mretvalue
store 0 to l_quantity,m_cases,totalloose,m_qtypc,m_loose,m_epcs,mretvalue
IF L_OUTCODE <=3
   if L_INNERLOOSE<>0 and L_pieces<>00
      loosepercase =  (L_pieces * L_innerloose)
      totalloose =  (L_qtycs * loosepercase) + (L_qtypc * L_innerloose) + L_Loose
      m_cases = Int(totalloose/loosepercase)
      m_qtypc = totalloose - (m_cases * loosepercase)
      m_qtypc = int(m_qtypc/L_innerloose)      
      m_loose = totalloose - ( m_cases * loosepercase) - (m_qtypc*L_innerloose)   
   else
      if L_pieces=0
         store 0 to m_qtypc,m_loose
         m_cases = L_qtycs
      endif
      if l_pieces<>0 .and. L_innerloose=0
         m_loose=0
         m_epcs = (L_qtycs * L_pieces) + L_qtypc
         m_cases = int(m_epcs/L_pieces)
         m_qtypc = m_epcs - (m_cases * L_pieces)
      endif      
   endif
   do case
      case L_OUTCODE = 3
         mretvalue=m_loose    && loose
      case L_OUTCODE = 2
         mretvalue= m_qtypc   && qtypc
      case L_OUTCODE = 1
         mretvalue= m_cases    && qtycs
   endcase         
ELSE
   if L_INNERLOOSE=0 or L_pieces=0
      mretvalue = iif( L_PIECES=0, L_qtycs, L_QTYCS + (L_QTYPC/L_PIECES) )
   else
      mretvalue = L_QTYCS + ((L_QTYPC+(L_LOOSE/L_INNERLOOSE))/L_PIECES)
   endif
endif

return mretvalue
       