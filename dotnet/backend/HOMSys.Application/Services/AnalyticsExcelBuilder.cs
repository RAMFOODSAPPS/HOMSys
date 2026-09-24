using System.Globalization;
using ClosedXML.Excel;
using HOMSys.Application.DTOs.Analytics;

namespace HOMSys.Application.Services;

/// <summary>
/// Data Analytics Excel export: one sheet per report/widget with a title,
/// filter summary and provenance header, then TYPED cells (numbers stay
/// numbers, dates stay dates) plus a totals row. Pivots render as a matrix
/// with row/column margins.
/// </summary>
public class AnalyticsExcelBuilder
{
    private const int MaxCells = 1_000_000;
    private const int HeaderRow = 5;
    private static readonly XLColor Burgundy = XLColor.FromHtml("#800000");

    public byte[] Build(string title, string generatedBy, DateTime generatedPh, IReadOnlyList<ExportSheet> sheets)
    {
        using var wb = new XLWorkbook();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sheets)
        {
            var ws = wb.Worksheets.Add(SheetName(s.Title, used));
            ws.Cell(1, 1).Value = s.Title;
            ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(Burgundy);
            ws.Cell(2, 1).Value = "Filters: " + s.FiltersText;
            var meta = $"Generated {generatedPh:yyyy-MM-dd HH:mm} (PH) by {generatedBy}";
            if (s.Result.AsOf is { } asOf) meta += $" · Data as of {asOf.AddHours(8):yyyy-MM-dd HH:mm} (PH)";
            if (s.Result.Scope is { } scope) meta += $" · Branch scope {scope}";
            ws.Cell(3, 1).Value = meta;
            ws.Range(2, 1, 3, 1).Style.Font.SetFontColor(XLColor.Gray);

            var lastRow = s.Pivot && s.Result.RowTotals is not null
                ? WriteMatrix(ws, s.Result)
                : WriteTable(ws, s.Result);

            if (s.Result.Truncated)
                ws.Cell(lastRow + 2, 1).Value = "Note: the row limit was reached — add filters to export the remaining rows.";

            ws.SheetView.FreezeRows(HeaderRow);
            ws.Columns().AdjustToContents(HeaderRow, Math.Min(lastRow, HeaderRow + 500));
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── flat table ───────────────────────────────────────────────────────────

    private static int WriteTable(IXLWorksheet ws, AnalyticsResultDto r)
    {
        var cols = r.Columns;
        for (var i = 0; i < cols.Count; i++)
        {
            var c = cols[i];
            ws.Cell(HeaderRow, i + 1).Value = c.Role == "caption" ? $"{c.Label} Name" : c.Label;
        }
        StyleHeader(ws.Range(HeaderRow, 1, HeaderRow, cols.Count));

        var maxRows = Math.Max(1, MaxCells / Math.Max(1, cols.Count));
        var rows = r.Rows.Concat(r.OthersRows).Take(maxRows).ToList();
        if (rows.Count < r.Rows.Count + r.OthersRows.Count) r.Truncated = true;

        var row = HeaderRow + 1;
        foreach (var data in rows)
        {
            for (var i = 0; i < cols.Count; i++)
                SetCell(ws.Cell(row, i + 1), data[i] ?? (cols[i].Role == "dim" ? "(blank)" : null), cols[i]);
            row++;
        }
        if (r.Total is { } total)
        {
            ws.Cell(row, 1).Value = "Total";
            for (var i = 0; i < cols.Count; i++)
                if (cols[i].Role == "measure") SetCell(ws.Cell(row, i + 1), total[i], cols[i]);
            ws.Range(row, 1, row, cols.Count).Style.Font.SetBold().Border.SetTopBorder(XLBorderStyleValues.Thin);
            row++;
        }
        if (rows.Count > 0) ws.Range(HeaderRow, 1, HeaderRow + rows.Count, cols.Count).SetAutoFilter();
        return row - 1;
    }

    // ── pivot matrix ─────────────────────────────────────────────────────────

    private static int WriteMatrix(IXLWorksheet ws, AnalyticsResultDto r)
    {
        var cols = r.Columns;
        int d0 = Idx(cols, "d0"), d0c = Idx(cols, "d0c"), d1 = Idx(cols, "d1"), d1c = Idx(cols, "d1c");
        var measures = cols.Select((c, i) => (c, i)).Where(x => x.c.Role == "measure").ToList();

        var rowKeys = (r.RowTotals is { Count: > 0 } ? r.RowTotals : r.Rows).Select(x => x[d0]).Distinct(KeyComparer.Instance).ToList();
        var colKeys = (r.ColTotals is { Count: > 0 } ? r.ColTotals : r.Rows).Select(x => x[d1]).Distinct(KeyComparer.Instance).ToList();
        var cell = r.Rows.ToDictionary(x => (K(x[d0]), K(x[d1])));
        var rowTotal = (r.RowTotals ?? []).ToDictionary(x => K(x[d0]));
        var colTotal = (r.ColTotals ?? []).ToDictionary(x => K(x[d1]));
        string Caption(object?[]? src, int keyIdx, int capIdx, object? key, ColumnDto col) =>
            src is not null && capIdx >= 0 && src[capIdx] is string s ? s : Display(key, col);

        ws.Cell(HeaderRow, 1).Value = cols[d0].Label;
        var c = 2;
        foreach (var ck in colKeys)
        {
            var src = r.Rows.FirstOrDefault(x => K(x[d1]) == K(ck)) ?? colTotal.GetValueOrDefault(K(ck));
            var label = Caption(src, d1, d1c, ck, cols[d1]);
            foreach (var (m, _) in measures)
                ws.Cell(HeaderRow, c++).Value = measures.Count > 1 ? $"{label} · {m.Label}" : label;
        }
        foreach (var (m, _) in measures)
            ws.Cell(HeaderRow, c++).Value = measures.Count > 1 ? $"Total · {m.Label}" : "Total";
        var lastCol = c - 1;
        StyleHeader(ws.Range(HeaderRow, 1, HeaderRow, lastCol));

        var row = HeaderRow + 1;
        foreach (var rk in rowKeys)
        {
            var src = r.Rows.FirstOrDefault(x => K(x[d0]) == K(rk)) ?? rowTotal.GetValueOrDefault(K(rk));
            ws.Cell(row, 1).Value = Caption(src, d0, d0c, rk, cols[d0]);
            c = 2;
            foreach (var ck in colKeys)
                foreach (var (m, mi) in measures)
                    SetCell(ws.Cell(row, c++), cell.GetValueOrDefault((K(rk), K(ck)))?[mi], m);
            foreach (var (m, mi) in measures)
                SetCell(ws.Cell(row, c++), rowTotal.GetValueOrDefault(K(rk))?[mi], m);
            row++;
        }

        ws.Cell(row, 1).Value = "Total";
        c = 2;
        foreach (var ck in colKeys)
            foreach (var (m, mi) in measures)
                SetCell(ws.Cell(row, c++), colTotal.GetValueOrDefault(K(ck))?[mi], m);
        foreach (var (m, mi) in measures)
            SetCell(ws.Cell(row, c++), r.Total?[mi], m);
        ws.Range(row, 1, row, lastCol).Style.Font.SetBold().Border.SetTopBorder(XLBorderStyleValues.Thin);
        return row;
    }

    // ── cells ────────────────────────────────────────────────────────────────

    private static void SetCell(IXLCell cell, object? v, ColumnDto col)
    {
        switch (v)
        {
            case null:
                return;
            case string s:
                cell.Value = s;
                return;
            case bool b:
                cell.Value = b ? "Yes" : "No";
                return;
            case DateOnly d:
                cell.Value = d.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = col.Bucket switch
                {
                    "month" => "mmm yyyy",
                    "year" => "yyyy",
                    _ => "yyyy-mm-dd",
                };
                return;
            case DateTime dt:
                cell.Value = dt;
                cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
                return;
        }

        if (col.Type == "bool")
        {
            cell.Value = Convert.ToInt32(v, CultureInfo.InvariantCulture) == 1 ? "Yes" : "No";
            return;
        }
        if (v is IConvertible && col.Type is "int" or "decimal")
        {
            cell.Value = Convert.ToDouble(v, CultureInfo.InvariantCulture);
            cell.Style.NumberFormat.Format = col.Format switch
            {
                "php" or "dec" => "#,##0.00",
                "pct" => "0.0%",
                "int" or "days" or "daysSev" => "#,##0",
                _ => col.Type == "int" ? "0" : "#,##0.00",
            };
            return;
        }
        cell.Value = Convert.ToString(v, CultureInfo.InvariantCulture);
    }

    private static string Display(object? v, ColumnDto col) => v switch
    {
        null => "(blank)",
        DateOnly d => col.Bucket switch
        {
            "month" => d.ToString("MMM yyyy", CultureInfo.InvariantCulture),
            "quarter" => $"{d.Year} Q{(d.Month - 1) / 3 + 1}",
            "year" => d.Year.ToString(CultureInfo.InvariantCulture),
            _ => d.ToString("yyyy-MM-dd"),
        },
        _ when col.Type == "bool" => Convert.ToInt32(v, CultureInfo.InvariantCulture) == 1 ? "Yes" : "No",
        _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "",
    };

    private static void StyleHeader(IXLRange range) =>
        range.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(Burgundy);

    private static int Idx(List<ColumnDto> cols, string key) => cols.FindIndex(c => c.Key == key);

    private static string K(object? v) => v switch
    {
        null => "\0",
        DateOnly d => d.ToString("yyyy-MM-dd"),
        _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "\0",
    };

    private sealed class KeyComparer : IEqualityComparer<object?>
    {
        public static readonly KeyComparer Instance = new();
        public new bool Equals(object? x, object? y) => K(x) == K(y);
        public int GetHashCode(object? obj) => K(obj).GetHashCode();
    }

    /// <summary>Excel sheet names: ≤31 chars, no []:*?/\ , unique.</summary>
    private static string SheetName(string title, HashSet<string> used)
    {
        var clean = new string((string.IsNullOrWhiteSpace(title) ? "Sheet" : title)
            .Select(ch => "[]:*?/\\".Contains(ch) ? '-' : ch).ToArray()).Trim();
        if (clean.Length > 31) clean = clean[..31];
        var name = clean;
        for (var i = 2; !used.Add(name); i++)
        {
            var suffix = $" ({i})";
            name = (clean.Length + suffix.Length > 31 ? clean[..(31 - suffix.Length)] : clean) + suffix;
        }
        return name;
    }
}
