using ClosedXML.Excel;

namespace HOMSys.Application.Services;

/// <summary>
/// Renders a PricelistExportResult into the one-sheet, per-customer-column
/// comparison workbook, styled after the original branch pricelist export
/// (C:\XLSFILES1\PRICELIST FOR ....xls): bold Calibri 10pt header block,
/// 2-row merged column headers, bold category header rows, matching column
/// widths for the fixed columns (A-E).
/// </summary>
public class PricelistExcelBuilder
{
    public byte[] Build(PricelistExportResult result, List<PricelistCustomerColumn>? customers = null)
    {
        customers ??= result.Customers;

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("PRICELIST");
        ws.Style.Font.FontSize = 8;

        const int fixedCols = 6; // SKU, DESCRIPTION, PACKING, PIECES, CASE BARCODE, BARCODE
        var lastCol = fixedCols + customers.Count * 3;

        // Header block
        ws.Cell(1, 1).Value = "RAM FOOD PRODUCTS, INC.";
        ws.Cell(2, 1).Value = $"Effectivity Date: {result.EffectivityDate:MMMM d, yyyy}";
        for (var r = 1; r <= 2; r++)
        {
            ws.Row(r).Style.Font.Bold = true;
            ws.Row(r).Style.Font.FontName = "Calibri";
            ws.Row(r).Style.Font.FontSize = 10;
        }

        // 2-row column headers, starting row 4
        const int headerRow1 = 4;
        const int headerRow2 = 5;

        ws.Cell(headerRow1, 1).Value = "SKU";
        ws.Cell(headerRow1, 2).Value = "PRODUCT DESCRIPTION";
        ws.Cell(headerRow1, 3).Value = "PACKING";
        ws.Cell(headerRow1, 4).Value = "PIECES";
        ws.Cell(headerRow1, 5).Value = "CASE BARCODE";
        ws.Cell(headerRow1, 6).Value = "BARCODE";
        for (var c = 1; c <= fixedCols; c++)
            ws.Range(headerRow1, c, headerRow2, c).Merge();

        var col = fixedCols + 1;
        foreach (var cust in customers)
        {
            ws.Range(headerRow1, col, headerRow1, col + 2).Merge();
            ws.Cell(headerRow1, col).Value = $"({cust.CustKey}) {cust.CusName}";
            ws.Cell(headerRow2, col).Value = "LIST PRICE with VAT (PERCASE)";
            ws.Cell(headerRow2, col + 1).Value = "LIST PRICE with VAT (PER UNIT)";
            ws.Cell(headerRow2, col + 2).Value = "SRP";
            col += 3;
        }

        var headerRange = ws.Range(headerRow1, 1, headerRow2, lastCol);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.WrapText = true;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        var bodyStartRow = headerRow2 + 2; // one blank spacer row, like the original
        var row = bodyStartRow;
        foreach (var group in result.Groups)
        {
            if (!string.IsNullOrEmpty(group.Header))
            {
                ws.Cell(row, 1).Value = group.Header;
                ws.Row(row).Style.Font.Bold = true;
                row++;
            }

            foreach (var line in group.Rows)
            {
                if (int.TryParse(line.CProdNo, out var prodNo))
                    ws.Cell(row, 1).Value = prodNo;
                else
                    ws.Cell(row, 1).Value = line.CProdNo;
                ws.Cell(row, 2).Value = line.ProdDesc;
                ws.Cell(row, 3).Value = line.PackSize;
                ws.Cell(row, 4).Value = line.Pieces;
                ws.Cell(row, 5).Value = line.CaseBarcode;
                ws.Cell(row, 6).Value = line.Barcode;

                var c = fixedCols + 1;
                foreach (var cust in customers)
                {
                    var value = line.ByCustKey.GetValueOrDefault(cust.CustKey);
                    if (value?.CasePriceWithVat is decimal casePrice)
                        ws.Cell(row, c).Value = casePrice;
                    if (value?.UnitPriceWithVat is decimal unitPrice)
                        ws.Cell(row, c + 1).Value = unitPrice;
                    if (value?.Srp is decimal srp)
                        ws.Cell(row, c + 2).Value = srp;
                    c += 3;
                }

                row++;
            }
        }

        if (row > bodyStartRow)
        {
            var bodyRange = ws.Range(bodyStartRow, 1, row - 1, lastCol);
            bodyRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            bodyRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        ws.Columns(2, lastCol).AdjustToContents();
        ws.Column(1).Width = 8.43;
        ws.Column(4).Width = 4.57;
        ws.Column(5).Width = 14.14;
        ws.Column(6).Width = 14.14;
        ws.Column(7).Width = 10.00;
        ws.Column(8).Width = 10.00;

        ws.SheetView.FreezeRows(headerRow2);
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
