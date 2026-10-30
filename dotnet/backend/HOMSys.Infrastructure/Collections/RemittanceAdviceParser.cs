using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;
using HOMSys.Domain.Entities;

namespace HOMSys.Infrastructure.Collections;

/// <summary>
/// Reads a chain's Remittance Advice Excel (same layout for every NKA customer; samples: SVI - LUZON 28571.XLS,
/// SVI - VISMIN 23897.XLS). Header labels in column A, value in B: CHAIN NAME, OR NO., OR DATE, DCR NO., DCR DATE,
/// CHECK DATE, CHECK NO., CHECK AMOUNT, DEPOSIT BANK ACCTNO, then "FOR LUZON|VISMIN PROCESSING". Then a two-row
/// column header (Branch, Custkey, BIR Series, Docno, Docdate, Gross Amount, Nature of Deduction → Dealcode /
/// Remarks for no dealcode / Rfc/Bo/Rtv / Cewit, Amount, Net Amount) and one block per invoice: a row with the
/// custkey, up to six deduction rows and a "Total" row with the net; a final "Total:" row = CHECK AMOUNT.
/// OR NO. / DCR NO. pre-fill the CR's (editable) OR / DCR no. — they match what HOMS holds (OR 21312 / DCR 291262,
/// OR 21764 / DCR 287886). Columns are found by their header label, not position.
/// </summary>
public static partial class RemittanceAdviceParser
{
    public record Deduction(DeductionType Type, decimal Amount, string? Remark);

    public record Block(int RowNo, string CustKey, string? BirSeries, int DocNo, DateOnly? DocDate, decimal Gross,
        List<Deduction> Deductions, decimal Net)
    {
        public decimal Cewt => Deductions.Where(d => d.Type == DeductionType.Ewt2307).Sum(d => d.Amount);
        public decimal RfcBoRtv => Deductions.Where(d => d.Type == DeductionType.RfcBoRtv).Sum(d => d.Amount);
        public decimal Other => Deductions.Where(d => d.Type == DeductionType.Other).Sum(d => d.Amount);
        public bool NetMatches => Math.Abs(Gross - Cewt - RfcBoRtv - Other - Net) <= 0.01m;
    }

    /// <summary>Desk: HOMS DEPTCODE (32 Luzon / HO desk, 31 Visayas-Mindanao desk). Total = the "Total:" row.</summary>
    public record Result(string? ChainName, DateOnly? OrDate, string? CheckNo, DateOnly? CheckDate, decimal? CheckAmount,
        int? DepositGl, int? Desk, string? DeskLabel, decimal? Total, List<Block> Blocks, string? OrNo = null, int? DcrNo = null);

    public static readonly string[] Extensions = [".xls", ".xlsx"];

    static RemittanceAdviceParser() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static Result Parse(Stream content)
    {
        var grid = Read(content);
        string Text(object? v) => v switch
        {
            null => "",
            double d => d.ToString(CultureInfo.InvariantCulture),
            DateTime t => t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => Squash(v.ToString() ?? ""),
        };
        string Norm(object? v) => Letters().Replace(Text(v).ToUpperInvariant(), "");

        // ── header block (label in one cell, value in the next non-empty cell) ──
        object? Value(string label)
        {
            foreach (var row in grid)
                for (var i = 0; i < row.Length; i++)
                    if (Norm(row[i]) == label)
                        return row.Skip(i + 1).FirstOrDefault(x => Text(x).Length > 0);
            return null;
        }
        int? desk = null;
        string? deskLabel = null;
        foreach (var row in grid)
            foreach (var cell in row)
            {
                var t = Norm(cell);
                if (t.StartsWith("FORLUZON")) { desk = 32; deskLabel = "Luzon"; }
                else if (t.StartsWith("FORVISMIN") || t.StartsWith("FORVISAYAS")) { desk = 31; deskLabel = "VisMin"; }
            }

        // ── column header ──
        var headerRow = grid.FindIndex(r => r.Any(c => Norm(c) == "CUSTKEY") && r.Any(c => Norm(c) == "DOCNO"));
        if (headerRow < 0) throw new FormatException("This isn't an RA Excel: no column header with Custkey and Docno.");
        int Col(params string[] labels)
        {
            foreach (var r in grid.Skip(headerRow).Take(2))
                for (var i = 0; i < r.Length; i++)
                    if (labels.Contains(Norm(r[i]))) return i;
            return -1;
        }
        int cCust = Col("CUSTKEY"), cBir = Col("BIRSERIES"), cDoc = Col("DOCNO"), cDate = Col("DOCDATE"),
            cGross = Col("GROSSAMOUNT", "GROSS"), cDeal = Col("DEALCODE"), cRemark = Col("REMARKSFORNODEALCODE", "REMARKS"),
            cRfc = Col("RFCBORTV"), cCewt = Col("CEWIT", "CEWT", "EWT"), cAmt = Col("AMOUNT"), cNet = Col("NETAMOUNT", "NET");
        if (cGross < 0 || cNet < 0) throw new FormatException("The RA Excel has no Gross Amount / Net Amount columns.");

        object? At(object?[] row, int i) => i >= 0 && i < row.Length ? row[i] : null;
        decimal Amount(object?[] row, int i) => Num(At(row, i)) is { } d ? Math.Round(d, 2) : 0m;

        // ── invoice blocks ──
        var blocks = new List<Block>();
        decimal? total = null;
        Block? open = null;
        var deds = new List<Deduction>();
        void Close(decimal? net)
        {
            if (open is null) return;
            var b = open with { Deductions = [.. deds] };
            blocks.Add(b with { Net = net ?? b.Gross - b.Cewt - b.RfcBoRtv - b.Other });
            open = null;
            deds.Clear();
        }
        for (var r = headerRow + 2; r < grid.Count; r++)
        {
            var row = grid[r];
            if (row.Any(c => Norm(c) == "TOTAL" && Text(c).Contains(':')) || (open is null && row.Any(c => Text(c).StartsWith("Total:", StringComparison.OrdinalIgnoreCase))))
            {
                Close(null);
                total = Num(At(row, cNet)) is { } t ? Math.Round(t, 2) : total;
                continue;
            }
            if (row.Any(c => Norm(c) == "TOTAL"))
            {
                Close(Num(At(row, cNet)) is { } net ? Math.Round(net, 2) : null);
                continue;
            }
            var cust = Num(At(row, cCust));
            var doc = Num(At(row, cDoc));
            if (cust is > 0m && doc is > 0m)
            {
                Close(null);
                open = new Block(r + 1, ((long)cust.Value).ToString(CultureInfo.InvariantCulture), Text(At(row, cBir)) is { Length: > 0 } bir ? Clip(bir, 40) : null,
                    (int)doc.Value, Date(At(row, cDate)), Amount(row, cGross), [], 0);
            }
            if (open is null) continue;

            // Deduction cells of this row (the block's first row can carry them too).
            if (Amount(row, cCewt) is > 0m and var ewt) deds.Add(new Deduction(DeductionType.Ewt2307, ewt, "CEWT"));
            var remark = Join(Text(At(row, cDeal)), Text(At(row, cRemark)));
            if (Amount(row, cRfc) is > 0m and var rfc) deds.Add(new Deduction(DeductionType.RfcBoRtv, rfc, remark ?? "RFC/BO/RTV"));
            if (Amount(row, cAmt) is > 0m and var amt)
                deds.Add(new Deduction(remark is not null && RfcRemark().IsMatch(remark) ? DeductionType.RfcBoRtv : DeductionType.Other, amt, remark));
        }
        Close(null);
        if (blocks.Count == 0) throw new FormatException("The RA Excel lists no invoices.");

        return new Result(
            Text(Value("CHAINNAME")) is { Length: > 0 } chain ? Clip(chain, 50) : null,
            Date(Value("ORDATE")),
            Value("CHECKNO") is { } ck ? Clip(Num(ck) is { } n ? ((long)n).ToString(CultureInfo.InvariantCulture) : Text(ck), 20) : null,
            Date(Value("CHECKDATE")),
            Num(Value("CHECKAMOUNT")) is { } amount ? Math.Round(amount, 2) : null,
            Num(Value("DEPOSITBANKACCTNO")) is { } gl ? (int)gl : null,
            desk, deskLabel, total, blocks,
            Value("ORNO") is { } orCell ? Clip(Num(orCell) is { } orNum ? ((long)orNum).ToString(CultureInfo.InvariantCulture) : Text(orCell), 20) : null,
            Num(Value("DCRNO")) is { } dcr && dcr is > 0m and < 2_147_483_647m ? (int)dcr : null);
    }

    private static List<object?[]> Read(Stream content)
    {
        using var reader = ExcelReaderFactory.CreateReader(content);
        var rows = new List<object?[]>();
        while (reader.Read())   // first sheet only
        {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++) row[i] = reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    private static decimal? Num(object? v) => v switch
    {
        null => null,
        double d => (decimal)d,
        decimal m => m,
        int i => i,
        long l => l,
        string s => BankStatementParser.ParseAmount(s),
        _ => BankStatementParser.ParseAmount(v.ToString()),
    };

    private static DateOnly? Date(object? v) => v switch
    {
        DateTime d => d.Year >= 2000 ? DateOnly.FromDateTime(d) : null,
        double serial when serial is > 36526 and < 73051 => DateOnly.FromDateTime(DateTime.FromOADate(serial)),
        string s => BankStatementParser.ParseDate(s),
        _ => null,
    };

    private static string? Join(params string[] parts)
    {
        var s = string.Join(" ", parts.Where(p => p.Length > 0));
        return s.Length == 0 ? null : Clip(s, 100);
    }

    private static string Clip(string s, int max) => s.Length > max ? s[..max] : s;
    private static string Squash(string s) => Spaces().Replace(s, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"[^A-Z0-9]")]
    private static partial Regex Letters();

    /// <summary>A deduction remark naming a return: "RTV", "RFC 123", "BO", "B.O.".</summary>
    [GeneratedRegex(@"^\s*(RTV|RFC|B\.?O\.?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RfcRemark();
}
