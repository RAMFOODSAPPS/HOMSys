using ClosedXML.Excel;
using HOMSys.Domain.Entities;

namespace HOMSys.Application.Services;

/// <summary>
/// Adds the two "Diff (Value)"/"Diff (%)" comparison sheets (against a baseline
/// customer) ahead of the raw grouped-pricelist sheet PricelistExcelBuilder
/// already built. Only case price (LP w/ VAT) is compared — matches the
/// reference report format validated 2026-09-08 (CDO/Primus, then Isabela).
/// </summary>
public class GroupedPricelistExcelBuilder
{
    public byte[] AddDiffSheets(byte[] rawWorkbookBytes, PricelistExportResult result, Customer baseline)
    {
        using var wb = new XLWorkbook(new MemoryStream(rawWorkbookBytes));
        var baselineLabel = $"({baseline.CustKey}) {baseline.CusName}";
        var representativeColumns = result.Customers;

        // result.Groups already only contains products PricelistExportService
        // considered (ProductRepository.GetPriceListWithCategoryAsync filters
        // PriceList=true, a real Brand, Pieces>0, !PhOut, and excludes delisted
        // "99x" — the same "active product" rule normal pricelist generation
        // uses), so no separate active-product filtering is needed here.
        var skuRows = result.Groups
            .SelectMany(g => g.Rows)
            .Select(r => new
            {
                r.CProdNo,
                Description = $"{r.ProdDesc} ({r.Pieces} x {r.PackSize})",
                Baseline = r.ByCustKey.GetValueOrDefault(baseline.CustKey)?.CasePriceWithVat,
                Values = representativeColumns.Select(c => r.ByCustKey.GetValueOrDefault(c.CustKey)?.CasePriceWithVat).ToList()
            })
            .Where(r => r.Baseline is not null)
            .ToList();

        var lastCol = 3 + representativeColumns.Count;

        void WriteDiffSheet(IXLWorksheet ws, string title, bool percent)
        {
            ws.Cell(1, 1).Value = title;
            ws.Range(1, 1, 1, lastCol).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            const int headerRow = 2;
            ws.Cell(headerRow, 1).Value = "SKU";
            ws.Cell(headerRow, 2).Value = "DESCRIPTION";
            ws.Cell(headerRow, 3).Value = baselineLabel;
            for (var i = 0; i < representativeColumns.Count; i++)
                ws.Cell(headerRow, 4 + i).Value = representativeColumns[i].CusName;
            ws.Row(headerRow).Style.Font.Bold = true;
            ws.Row(headerRow).Style.Alignment.WrapText = true;

            var r = headerRow + 1;
            foreach (var row in skuRows)
            {
                ws.Cell(r, 1).Value = int.TryParse(row.CProdNo, out var n) ? n : row.CProdNo;
                ws.Cell(r, 2).Value = row.Description;
                ws.Cell(r, 3).Value = row.Baseline!.Value;
                for (var i = 0; i < row.Values.Count; i++)
                {
                    var cell = ws.Cell(r, 4 + i);
                    if (row.Values[i] is not decimal v) continue;
                    var diff = v - row.Baseline.Value;
                    if (percent)
                    {
                        cell.Value = diff / row.Baseline.Value;
                        cell.Style.NumberFormat.Format = "0.00%";
                    }
                    else
                    {
                        cell.Value = Math.Round(diff, 2);
                    }
                    cell.Style.Font.FontColor = diff > 0
                        ? XLColor.FromArgb(0, 128, 0)
                        : diff < 0 ? XLColor.FromArgb(192, 0, 0) : XLColor.Black;
                }
                r++;
            }

            var tableRange = ws.Range(headerRow, 1, r - 1, lastCol);
            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            tableRange.Style.Border.OutsideBorderColor = XLColor.Black;
            tableRange.Style.Border.InsideBorderColor = XLColor.Black;

            // Columns 1-3 (SKU/Description/Baseline) fit their own content, but
            // group columns get a fixed width — a long "& N more" header would
            // otherwise stretch that one column way out (AdjustToContents sizes
            // to the longest unwrapped line). Grow the header row's height instead
            // (ClosedXML's row AdjustToContents doesn't reliably size for wrap).
            ws.Columns(1, 3).AdjustToContents();
            ws.Columns(4, lastCol).Width = 18;
            ws.Row(headerRow).Height = 48;
            ws.SheetView.FreezeRows(2);
            ws.SheetView.FreezeColumns(3);
        }

        // Two separate sheets, placed ahead of the raw grouped sheet so the
        // summary is the first thing opened — value first, then percent.
        var valueWs = wb.Worksheets.Add("Diff (Value)", 1);
        WriteDiffSheet(valueWs, $"DIFFERENCE (LP w/ VAT) vs {baselineLabel}", percent: false);
        var pctWs = wb.Worksheets.Add("Diff (%)", 2);
        WriteDiffSheet(pctWs, $"% DIFFERENCE vs {baselineLabel}", percent: true);

        using var outStream = new MemoryStream();
        wb.SaveAs(outStream);
        return outStream.ToArray();
    }
}
