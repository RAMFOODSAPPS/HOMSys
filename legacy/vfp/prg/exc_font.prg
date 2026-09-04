* exc_font
* set fonts in Excel
param thisrange,thisfontsize,thisfontname,thisalignment,thisfontbold
loExcel.Range(thisrange).font.bold=thisfontbold
LoExcel.Range(thisrange).font.size = thisfontsize
loExcel.Range(thisrange).font.Name = thisfontname
loExcel.Range(thisrange).HorizontalAlignment= thisalignment
