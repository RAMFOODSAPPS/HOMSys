param xpieces,mxum,xqtyperpc
mxkg = 0
do case 
   case upper(alltr(mxum)) == "G"
      mxkg = 0.001
   case upper(alltr(mxum)) == "GAL"
      mxkg = 3.7854
      
   case upper(alltr(mxum)) == "L"
      mxkg = 1
   case upper(alltr(mxum)) == "LBS"
      mxkg = 0.453592
   case upper(alltr(mxum)) == "ML"
      mxkg = 0.0283495          
   case upper(alltr(mxum)) == "QRT"
      mxkg = 0.9464
   case upper(alltr(mxum)) == "KLS" .or. upper(alltr(mxum)) == "KG"
      mxkg = 1
endcase
mxkg = mxkg * (xqtyperpc * xpieces)
return mxkg