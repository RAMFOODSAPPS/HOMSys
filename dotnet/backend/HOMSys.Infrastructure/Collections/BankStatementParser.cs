using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;

namespace HOMSys.Infrastructure.Collections;

/// <summary>
/// Reads the bank portals' transaction-history exports (BDO, BPI, MBTC, PBCOM .xls; LBP .xlsx; CBC, SBC .csv).
/// Columns are found by their header LABEL, not position — one bank's "Current Day" and "Previous Day"
/// files already differ (MBTC). Every cell in these exports is text ("2,012,471.69", "6-Oct-26").
/// Rows whose date doesn't parse (blank padding, "Total", report footers) are skipped.
/// </summary>
public static partial class BankStatementParser
{
    public record Line(DateOnly TxnDate, DateOnly? ValueDate, string? Time, string Description, string? Reference,
        string? Branch, string? CheckNo, decimal Debit, decimal Credit, decimal? Balance, string Fingerprint);

    public record Result(string? AccountNo, List<Line> Lines);

    public static readonly string[] Extensions = [".xls", ".xlsx", ".csv"];

    static BankStatementParser() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static Result Parse(Stream content, string fileName)
    {
        var grid = Path.GetExtension(fileName).Equals(".csv", StringComparison.OrdinalIgnoreCase) ? ReadCsv(content) : ReadExcel(content);
        var headerRow = grid.FindIndex(IsHeader);
        if (headerRow < 0)
            throw new FormatException("No transaction header row found (expected columns like Date, Description, Debit, Credit).");

        var cols = MapColumns(grid[headerRow]);
        var account = FindAccountNo(grid.Take(headerRow));
        var lines = new List<Line>();
        var seen = new Dictionary<string, int>();
        foreach (var row in grid.Skip(headerRow + 1))
        {
            string? Cell(int? i) => i is { } x && x < row.Length && row[x].Length > 0 ? row[x] : null;
            if (ParseDate(Cell(cols.Date)) is not { } date) continue;
            var debit = Math.Abs(ParseAmount(Cell(cols.Debit)) ?? 0);
            var credit = Math.Abs(ParseAmount(Cell(cols.Credit)) ?? 0);
            if (debit == 0 && credit == 0) continue;

            var dateCell = Cell(cols.Date)!;
            var time = Cell(cols.Time) ?? (TimeIn(dateCell) is { } t ? t : null);
            var description = Clip(Cell(cols.Description) ?? "", 300);
            var reference = Join(cols.References.Select(i => Cell(i)), 200);
            var branch = Join(cols.Branches.Select(i => Cell(i)), 100);
            var checkNo = Cell(cols.CheckNo) is { } c && c.Trim('0').Length > 0 ? Clip(c, 30) : null;
            var balance = ParseAmount(Cell(cols.Balance));
            var valueDate = ParseDate(Cell(cols.ValueDate));

            // Re-downloads overlap; identical rows in one file are told apart by their order.
            var key = $"{date:yyyyMMdd}|{valueDate:yyyyMMdd}|{time}|{description}|{reference}|{branch}|{checkNo}|{debit}|{credit}|{balance}";
            seen[key] = seen.GetValueOrDefault(key) + 1;
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{key}#{seen[key]}")));
            lines.Add(new Line(date, valueDate, time, description, reference, branch, checkNo, debit, credit, balance, fingerprint));
        }
        return new Result(account, lines);
    }

    // ── grid readers ────────────────────────────────────────────────────────

    private static List<string[]> ReadExcel(Stream content)
    {
        using var reader = ExcelReaderFactory.CreateReader(content);
        var rows = new List<string[]>();
        do
        {
            while (reader.Read())
            {
                var row = new string[reader.FieldCount];
                for (var i = 0; i < row.Length; i++)
                    row[i] = reader.GetValue(i) switch
                    {
                        null => "",
                        DateTime d => d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                        double n => n.ToString(CultureInfo.InvariantCulture),
                        var v => Squash(v.ToString() ?? ""),
                    };
                rows.Add(row);
            }
        } while (rows.FindIndex(IsHeader) < 0 && reader.NextResult());   // first sheet that has transactions
        return rows;
    }

    private static List<string[]> ReadCsv(Stream content)
    {
        using var sr = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var rows = new List<string[]>();
        var cells = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        int ch;
        while ((ch = sr.Read()) != -1)
        {
            var c = (char)ch;
            if (quoted)
            {
                var next = sr.Peek();
                if (c == '"' && next == '"') { cell.Append('"'); sr.Read(); }
                // A quote ends the field only before a comma, a line break or the end of the file. SBC doesn't escape the
                // quotes inside its Narrative ("DB REF NO. SE-101226-00119"Pymt to PhilHealth "); taking that one as the
                // end swallowed the next rows into one cell (4 lines missing from SBC_10152026.csv, 2026-10-16).
                else if (c == '"' && (next == ',' || next == '\r' || next == '\n' || next == -1)) quoted = false;
                else cell.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { cells.Add(Squash(cell.ToString())); cell.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && sr.Peek() == '\n') sr.Read();
                cells.Add(Squash(cell.ToString())); cell.Clear();
                rows.Add([.. cells]); cells.Clear();
            }
            else cell.Append(c);
        }
        if (cell.Length > 0 || cells.Count > 0) { cells.Add(Squash(cell.ToString())); rows.Add([.. cells]); }
        return rows;
    }

    // ── header / columns ────────────────────────────────────────────────────

    private static string Norm(string s) => Squash(s).TrimEnd(':').Trim().ToLowerInvariant();

    private static readonly string[] DateLabels = ["transaction date", "posting date", "date"];
    private static readonly string[] CreditLabels = ["credit", "credit amount"];

    private static bool IsHeader(string[] row)
    {
        var labels = row.Select(Norm).ToHashSet();
        return DateLabels.Any(labels.Contains) && CreditLabels.Any(labels.Contains);
    }

    private record Columns(int? Date, int? ValueDate, int? Time, int? Description, int[] References, int[] Branches,
        int? CheckNo, int? Debit, int? Credit, int? Balance);

    private static Columns MapColumns(string[] header)
    {
        var labels = header.Select(Norm).ToArray();
        int? Find(params string[] names) =>
            names.Select(n => Array.IndexOf(labels, n)).Where(i => i >= 0).Select(i => (int?)i).FirstOrDefault();
        int[] All(params string[] names) => names.Select(n => Array.IndexOf(labels, n)).Where(i => i >= 0).ToArray();

        var date = Find(DateLabels);
        // MBTC has both: the transaction date is the deposit; the posting date stands in for the value date.
        var valueDate = Find("value date") ?? (labels[date!.Value] == "transaction date" ? Find("posting date") : null);
        var description = Find("transaction description", "description", "narrative");
        var references = All("reference number", "sequence number", "narrative", "transaction code", "subscriber number/tin number", "other details")
            .Where(i => i != description).ToArray();
        return new Columns(date, valueDate, Find("time"), description, references,
            All("branch", "branch/channel", "branch code", "branch or originating branch", "channel"),
            Find("check number", "check no.", "check no"), Find("debit", "debit amount"), Find(CreditLabels), Find("running balance", "balance"));
    }

    /// <summary>"Account Number: 1033149610" in one cell, or the label with the number a few cells to the right.</summary>
    private static string? FindAccountNo(IEnumerable<string[]> preamble)
    {
        foreach (var row in preamble)
            for (var i = 0; i < row.Length; i++)
            {
                var m = AccountLabel().Match(row[i]);
                if (!m.Success) continue;
                var rest = row[i][(m.Index + m.Length)..].Trim();
                var value = rest.Length > 0 ? rest : row.Skip(i + 1).FirstOrDefault(c => c.Length > 0);
                if (value is not null && value.Any(char.IsDigit)) return value;
            }
        return null;
    }

    [GeneratedRegex(@"account\s*(number|no\.?)\s*:?", RegexOptions.IgnoreCase)]
    private static partial Regex AccountLabel();

    // ── values ──────────────────────────────────────────────────────────────

    private static readonly string[] DateFormats =
    [
        "M/d/yyyy H:mm", "M/d/yyyy H:mm:ss", "M/d/yyyy h:mm tt", "M/d/yyyy",
        // CBC's export since 2026-10-16: "10/15/26 9:04 PM" (two-digit year).
        "M/d/yy h:mm tt", "M/d/yy h:mm:ss tt", "M/d/yy H:mm", "M/d/yy", "MM-dd-yyyy", "M-d-yyyy", "d-MMM-yy", "d-MMM-yyyy",
        "dd MMM yyyy", "yyyy/MM/dd HH:mm", "yyyy/MM/dd HH:mm:ss", "yyyy/MM/dd", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd",
    ];

    public static DateOnly? ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        if (DateTime.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var d))
            return d.Year >= 2000 ? DateOnly.FromDateTime(d) : null;
        // A typed Excel date read as its serial number.
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is > 36526 and < 73051)
            return DateOnly.FromDateTime(DateTime.FromOADate(serial));
        return null;
    }

    private static string? TimeIn(string dateCell) => TimePart().Match(dateCell) is { Success: true } m ? m.Value : null;

    [GeneratedRegex(@"\d{1,2}:\d{2}(:\d{2})?(\s?[AP]M)?", RegexOptions.IgnoreCase)]
    private static partial Regex TimePart();

    /// <summary>"2,012,471.69" / "(1,000.00)" / "--" / "" → amount or null.</summary>
    public static decimal? ParseAmount(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Replace(",", "").Replace("PHP", "", StringComparison.OrdinalIgnoreCase).Trim();
        var negative = t.StartsWith('(') && t.EndsWith(')');
        t = t.Trim('(', ')').Trim();
        return decimal.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? Math.Round(negative ? -v : v, 2) : null;
    }

    private static string Squash(string s) => WhiteSpace().Replace(s, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();

    private static string Clip(string s, int max) => s.Length > max ? s[..max] : s;

    private static string? Join(IEnumerable<string?> parts, int max)
    {
        var s = string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct());
        return s.Length == 0 ? null : Clip(s, max);
    }
}
