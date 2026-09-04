* Exc_box
* draw box
param thisrange,mxlNone,mxlContinuous,mxlThin,mxlAutomatic,mxldiagonalDown,mxlDiagonalup,mxlEdgeleft,mxlEdgetop,MxlEdgebottom,mxlEdgeright,mxlInsideVertical,mxlInsideHorizontal

loExcel.range(thisrange).select
loexcel.Application.CutCopyMode = .f.      &&False
loexcel.Selection.Borders(mxlDiagonalDown).LineStyle = mxlNone
loexcel.Selection.Borders(mxlDiagonalUp).LineStyle = mxlNone
loexcel.Selection.Borders(mxlEdgeLeft).LineStyle = mxlContinuous
loexcel.Selection.Borders(mxlEdgeLeft).Weight = mxlThin
loexcel.Selection.Borders(mxlEdgeLeft).ColorIndex = mxlAutomatic
loExcel.Selection.Borders(mxlEdgeTop).LineStyle = mxlContinuous
loExcel.Selection.Borders(mxlEdgeTop).Weight = mxlThin
loExcel.Selection.Borders(mxlEdgeTop).ColorIndex = mxlAutomatic
loExcel.Selection.Borders(mxlEdgeBottom).LineStyle = mxlContinuous
loExcel.Selection.Borders(mxlEdgeBottom).Weight = mxlThin
loExcel.Selection.Borders(mxlEdgeBottom).ColorIndex = mxlAutomatic
loExcel.Selection.Borders(mxlEdgeRight).LineStyle = mxlContinuous
loExcel.Selection.Borders(mxlEdgeRight).Weight = mxlThin
loExcel.Selection.Borders(mxlEdgeRight).ColorIndex = mxlAutomatic
loExcel.Selection.Borders(mxlInsideVertical).LineStyle = mxlContinuous
loExcel.Selection.Borders(mxlInsideVertical).Weight = mxlThin
loExcel.Selection.Borders(mxlInsideVertical).ColorIndex = mxlAutomatic
loExcel.Selection.Borders(mxlInsideHorizontal).LineStyle = mxlContinuous
loExcel.Selection.Borders(mxlInsideHorizontal).Weight = mxlThin
loExcel.Selection.Borders(mxlInsideHorizontal).ColorIndex = mxlAutomatic
