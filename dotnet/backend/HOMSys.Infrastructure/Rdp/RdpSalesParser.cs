using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;

namespace HOMSys.Infrastructure.Rdp;

/// <summary>
/// Reads a distributor sales file laid out like "RAM RDP Sales Raw Data Template.xlsx"
/// (sheet "Template- Raw Data RDP STT") — or one of the RDPs' own system exports ("STT RDPs 2026 - For IT").
/// Columns are found by header text, so their order and extra columns don't matter. Handles both template
/// formats: A = one row per invoice line, B = one row per account + SKU + month (no invoice date / number).
/// In a workbook, the sheet whose header row names the most known columns is read (ties: a "raw" sheet).
/// </summary>
public static partial class RdpSalesParser
{
    public static readonly string[] Extensions = [".xlsx", ".xlsb", ".xls", ".csv"];

    static RdpSalesParser() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public record Row(int RowNo, int? CustKey, string RdpName, DateOnly Period, DateOnly? InvoiceDate, string InvoiceNo, string TxnType,
        string AccountCode, string AccountName, string AccountKey, string ChannelLabel, string Province, string City,
        string Salesman, string SkuCode, string ItemDesc, decimal Quantity, string Uom, decimal NetAmount, decimal? Cases);

    /// <param name="Notes">What was skipped or repaired, for the preview ("12 subtotal rows skipped").</param>
    public record Result(List<Row> Rows, List<string> Errors, int ErrorCount, int BlankRows, List<string> Notes);

    /// <summary>Transaction types that are not purchases: left out of sales, volume and buying accounts.</summary>
    public static readonly string[] NonPurchaseTypes = ["Free Goods", "Adjustment"];

    private enum Col
    {
        CustKey, RdpName, Month, Year, Reporting, InvDate, InvNo, Type, AcctCode, AcctName, Channel, Province, City, Salesman,
        Sku, SkuAlt, ItemCode, Item, Qty, PcsQty, CaseQty, QtyCases, Uom, Amount, Price, Pack, Principal,
    }

    // Header text → column, in PRIORITY order: when a file has two headers for the same column (Chase Mega's ITEM CODE and
    // RAM SKU CODE, Bonheur's Customer Code and Cust Code), the one listed first wins. Besides the template's own headers
    // these cover each RDP's export in "STT RDPs 2026 - For IT" (DRB, CTI, Chip Accendo, 1TO1, DRDI, Chase Mega,
    // Eight Prince, Bonheur, GT Visayas, GT MIN).
    private static readonly (string Header, Col Col)[] Aliases =
    [
        ("rdp custkey", Col.CustKey), ("custkey", Col.CustKey), ("rdp code", Col.CustKey), ("distributor code", Col.CustKey),
        ("rdp", Col.RdpName), ("distributor name", Col.RdpName), ("distributor", Col.RdpName),
        ("month", Col.Month), ("months", Col.Month),
        ("year", Col.Year),
        ("reporting month & year", Col.Reporting), ("reporting month and year", Col.Reporting), ("reporting month", Col.Reporting),
        ("period", Col.Reporting),
        ("invoice date", Col.InvDate), ("inv date", Col.InvDate), ("document date", Col.InvDate), ("trans. date", Col.InvDate),
        ("orderdate", Col.InvDate), ("order date", Col.InvDate), ("date", Col.InvDate),
        ("invoice no.", Col.InvNo), ("invoice no", Col.InvNo), ("invoice number", Col.InvNo), ("invoice #", Col.InvNo),
        ("si no.", Col.InvNo), ("si no", Col.InvNo), ("si#/dr#", Col.InvNo), ("si doc.no.", Col.InvNo), ("document no.", Col.InvNo),
        ("document no", Col.InvNo), ("trx no", Col.InvNo), ("num", Col.InvNo), ("ref no.", Col.InvNo), ("customerponumber", Col.InvNo),
        ("transaction type", Col.Type), ("trans. type", Col.Type), ("trx type", Col.Type), ("document type", Col.Type),
        ("doc type", Col.Type), ("trx desc", Col.Type), ("type", Col.Type),
        ("account code", Col.AcctCode), ("cust code", Col.AcctCode), ("customer code", Col.AcctCode), ("outlet code", Col.AcctCode),
        ("store id", Col.AcctCode),
        ("account name", Col.AcctName), ("act. name", Col.AcctName), ("customer name", Col.AcctName), ("stt customer name", Col.AcctName),
        ("outlet name", Col.AcctName), ("store name", Col.AcctName), ("customer", Col.AcctName), ("account", Col.AcctName),
        ("name", Col.AcctName),   // QuickBooks "Name"; when the file also has an account-name column, "Name" is the code (1TO1)
        ("channel type", Col.Channel), ("channel_type", Col.Channel), ("channel", Col.Channel), ("customer type", Col.Channel),
        ("customer class", Col.Channel),
        ("province", Col.Province),
        ("city / municipality", Col.City), ("city/municipality", Col.City), ("city", Col.City), ("municipality", Col.City),
        ("town or city", Col.City), ("town", Col.City), ("cities", Col.City),
        ("area", Col.City),       // only when the file has a single AREA column (GT MIN has two, both regions)
        ("salesman", Col.Salesman), ("salesman name", Col.Salesman), ("salesperson", Col.Salesman), ("salesrep", Col.Salesman),
        ("sales rep", Col.Salesman), ("rep", Col.Salesman), ("dsp", Col.Salesman), ("agent", Col.Salesman),
        ("ram sku code", Col.Sku), ("sku code", Col.Sku), ("item code", Col.Sku), ("product code", Col.Sku), ("itemcode", Col.Sku),
        ("sku", Col.Sku),
        ("lookup code", Col.SkuAlt), ("stockcode", Col.SkuAlt),   // "R9748", "RAM9749": used when the SKU column is blank
        ("item", Col.ItemCode),  // "9318 (RAM SWEET RELISH…)", "RAM FOODS:9052 (…)", "NRF9712.1 (…)"
        ("item description", Col.Item), ("ram sku description", Col.Item), ("product/item description", Col.Item),
        ("product description", Col.Item), ("stock description", Col.Item), ("sub sku description", Col.Item),
        ("sku description", Col.Item), ("ram sku name", Col.Item), ("sku name", Col.Item), ("description", Col.Item), ("memo", Col.Item),
        ("quantity", Col.Qty), ("qty", Col.Qty), ("orig qty", Col.Qty), ("original qty", Col.Qty), ("orderqty", Col.Qty),
        ("qty pcs", Col.PcsQty), ("sold pc", Col.PcsQty), ("pcs", Col.PcsQty),
        ("sold cs", Col.CaseQty), ("case", Col.CaseQty),
        ("qty in case", Col.QtyCases), ("qty in cases", Col.QtyCases), ("qty in cs", Col.QtyCases), ("qty (cs)", Col.QtyCases),
        ("total cases", Col.QtyCases), ("net of cm qty", Col.QtyCases), ("cases", Col.QtyCases),
        ("unit of measure", Col.Uom), ("uom", Col.Uom), ("u/m", Col.Uom), ("original uom", Col.Uom), ("orderuom", Col.Uom), ("unit", Col.Uom),
        ("net amount", Col.Amount), ("net of cm amt", Col.Amount), ("net", Col.Amount), ("amount w/ vat", Col.Amount),
        ("amount", Col.Amount), ("net sales", Col.Amount), ("sales value", Col.Amount),
        ("sales price", Col.Price), ("unit price", Col.Price), ("price", Col.Price),
        ("packing size", Col.Pack), ("pack size", Col.Pack), ("packing", Col.Pack), ("pckg", Col.Pack), ("cou", Col.Pack),
        ("pcs per case", Col.Pack),
        ("principal", Col.Principal), ("supplier name", Col.Principal),
    ];

    private static readonly Dictionary<string, (int Rank, Col Col)> AliasIndex =
        Aliases.Select((a, i) => (a, i)).ToDictionary(x => x.a.Header, x => (x.i, x.a.Col));

    private const int MaxErrors = 50;
    private const int HeaderScanRows = 40;

    public static Result Parse(Stream content, string fileName)
    {
        if (!content.CanSeek)
        {
            var copy = new MemoryStream();
            content.CopyTo(copy);
            copy.Position = 0;
            content = copy;
        }
        var csv = Path.GetExtension(fileName).Equals(".csv", StringComparison.OrdinalIgnoreCase);
        var csvRows = csv ? ReadCsv(content) : null;

        // Pass 1: the sheet + row with the best header.
        (int Sheet, int Row, Dictionary<Col, int> Map)? best = null;
        var bestScore = 0;
        var bestRaw = false;
        if (csvRows is not null) Consider(0, "", csvRows.Take(HeaderScanRows));
        else
        {
            using var reader = ExcelReaderFactory.CreateReader(content, new ExcelReaderConfiguration { LeaveOpen = true });
            var sheet = 0;
            do { Consider(sheet++, reader.Name ?? "", Rows(reader).Take(HeaderScanRows)); } while (reader.NextResult());
        }
        void Consider(int sheet, string name, IEnumerable<object?[]> rows)
        {
            var i = 0;
            foreach (var r in rows)
            {
                if (MapColumns(r) is { } m && m.ContainsKey(Col.Amount) && (m.ContainsKey(Col.AcctCode) || m.ContainsKey(Col.AcctName)))
                {
                    var raw = name.Contains("raw", StringComparison.OrdinalIgnoreCase);
                    if (m.Count > bestScore || (m.Count == bestScore && raw && !bestRaw)) { best = (sheet, i, m); bestScore = m.Count; bestRaw = raw; }
                }
                i++;
            }
        }
        if (best is null)
            return new([], ["No header row found. The file needs the template's column headers (ACCOUNT CODE, RAM SKU CODE, NET AMOUNT, …)."], 1, 0, []);
        var (sheetAt, headerAt, cols) = best.Value;

        var errors = new List<string>();
        var errorCount = 0;
        void Error(string msg) { errorCount++; if (errors.Count < MaxErrors) errors.Add(msg); }
        if (!cols.ContainsKey(Col.InvDate) && !cols.ContainsKey(Col.Reporting) && !cols.ContainsKey(Col.Month))
            Error("The file needs MONTH and YEAR, REPORTING MONTH or INVOICE DATE to know which month each row belongs to.");
        if (!cols.ContainsKey(Col.Qty) && !cols.ContainsKey(Col.PcsQty) && !cols.ContainsKey(Col.CaseQty) && !cols.ContainsKey(Col.QtyCases)
            && !cols.ContainsKey(Col.Reporting))
            Error("Column QUANTITY is missing.");
        if (errorCount > 0) return new([], errors, errorCount, 0, []);

        // Pass 2: the data rows, cut to the mapped columns (GT MIN's raw sheet is 650k rows × 50 columns).
        var width = cols.Values.Max() + 1;
        IEnumerable<object?[]> data;
        ExcelDataReader.IExcelDataReader? dataReader = null;
        if (csvRows is not null) data = csvRows.Skip(headerAt + 1);
        else
        {
            content.Position = 0;
            dataReader = ExcelReaderFactory.CreateReader(content, new ExcelReaderConfiguration { LeaveOpen = true });
            for (var s = 0; s < sheetAt; s++) dataReader.NextResult();
            data = Rows(dataReader, width).Skip(headerAt + 1);
        }

        var pool = new Dictionary<string, string>();
        string P(string s) => s.Length == 0 ? "" : pool.TryGetValue(s, out var x) ? x : pool[s] = s;

        var rows = new List<Row>();
        int blank = 0, subtotals = 0, otherPrincipal = 0, rebuilt = 0, badDates = 0, noAccount = 0;
        var groupSku = "";
        var rowNo = headerAt + 1;
        try
        {
            foreach (var cells in data)
            {
                rowNo++;
                object? Raw(Col c) => cols.TryGetValue(c, out var at) && at < cells.Length ? cells[at] : null;
                string Text(Col c) => Squash(Raw(c) switch
                {
                    null => "", double d => d.ToString(CultureInfo.InvariantCulture), DateTime dt => dt.ToString("yyyy-MM-dd"), var v => v.ToString() ?? "",
                });

                if (cols.Values.All(at => at >= cells.Length || string.IsNullOrWhiteSpace(cells[at]?.ToString())))
                {
                    // QuickBooks "sales by item" reports name the item on a line of its own above its rows.
                    if (FirstText(cells) is { } label && !IsTotal(label) && SkuIn(label) is { } g) groupSku = g;
                    blank++;
                    continue;
                }

                var acctCode = Text(Col.AcctCode);
                var acctName = Text(Col.AcctName);
                if (acctCode == "" && acctName == "" && Text(Col.InvDate) == "" && cells.Any(c => c is string s && IsTotal(Squash(s))))
                { subtotals++; continue; }

                var principal = Text(Col.Principal);
                if (principal != "" && !principal.Contains("RAM", StringComparison.OrdinalIgnoreCase)) { otherPrincipal++; continue; }

                int? custKey = null;
                var ck = Text(Col.CustKey);
                if (ck != "")
                {
                    if (int.TryParse(ck.Split('.')[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var k)) custKey = k;
                    else { Error($"Row {rowNo}: RDP CUSTKEY \"{ck}\" is not a number."); continue; }
                }

                // MONTH settles day/month order: these exports mix 07-01-2026, 07/20/2026 and 29/08/2026, and Excel itself
                // stores "02/09/2026" typed for 2 September as 9 February.
                var monthText = Text(Col.Month);
                var monthPeriod = PeriodOf(monthText, Text(Col.Year)) ?? ParsePeriod(Raw(Col.Month));
                var hintMonth = monthPeriod?.Month ?? MonthIndex(monthText);
                var invDateRaw = Raw(Col.InvDate);
                var invDate = ParseDate(invDateRaw, hintMonth);
                if (invDate is { } sw && hintMonth is { } hm && sw.Month != hm && sw.Day == hm) invDate = new DateOnly(sw.Year, hm, sw.Month);
                var period = monthPeriod ?? ParsePeriod(Raw(Col.Reporting))
                             ?? (invDate is { } di ? MonthOfYear(monthText, di) ?? new DateOnly(di.Year, di.Month, 1) : null);
                if (invDate is null && !string.IsNullOrWhiteSpace(invDateRaw?.ToString()))
                {
                    // An impossible date ("9/31/2026") is dropped when MONTH / YEAR already say which month the line is.
                    if (monthPeriod is null) { Error($"Row {rowNo}: INVOICE DATE \"{invDateRaw}\" is not a date (use yyyy-mm-dd or dd-mmm-yyyy)."); continue; }
                    badDates++;
                }
                if (period is null) { Error($"Row {rowNo}: no month — fill MONTH and YEAR (or INVOICE DATE)."); continue; }

                var sku = Text(Col.Sku);
                if (sku.EndsWith(".0")) sku = sku[..^2];
                if (sku == "0") sku = "";
                if (sku == "" && SkuAltCode().Match(Text(Col.SkuAlt)) is { Success: true } alt) sku = alt.Groups[1].Value;
                if (sku == "") sku = SkuIn(Text(Col.ItemCode)) ?? groupSku;
                var city = Text(Col.City);
                if (acctCode == "" && acctName == "") { noAccount++; continue; }

                var type = TxnType(Text(Col.Type));
                // QuickBooks bills a discount as an item of its own ("Disc 5% (Discount 5%)").
                if (sku == "" && (Text(Col.ItemCode) + " " + Text(Col.Item)).Trim().StartsWith("disc", StringComparison.OrdinalIgnoreCase))
                    type = "Discount";

                // Quantity: one QTY column with its unit, or separate case and piece columns (Chase Mega, DRDI, Chip Accendo).
                var uom = Text(Col.Uom).ToUpperInvariant();
                decimal? pack = Number(Raw(Col.Pack), out var pk) && pk > 0 ? pk : null;
                if (uom != "" && decimal.TryParse(uom, NumberStyles.Number, CultureInfo.InvariantCulture, out var uomPack))
                { pack ??= uomPack > 0 ? uomPack : null; uom = ""; }   // DRDI's UOM column holds the pack size
                decimal qty = 0;
                decimal? cases = null;
                var qtyText = Text(Col.Qty);
                if (qtyText != "")
                {
                    if (!Number(Raw(Col.Qty), out qty)) { Error($"Row {rowNo}: QUANTITY \"{qtyText}\" is not a number."); continue; }
                }
                else if (cols.ContainsKey(Col.PcsQty) || cols.ContainsKey(Col.CaseQty))
                {
                    Number(Raw(Col.CaseQty), out var cq);
                    Number(Raw(Col.PcsQty), out var pq);
                    if (cq != 0 && pq != 0 && pack is { } p1) { qty = cq * p1 + pq; uom = "PCS"; cases = cq + pq / p1; }
                    else if (cq != 0 && pq == 0) { qty = cq; uom = "CASE"; cases = cq; }
                    else { qty = pq; uom = "PCS"; cases = pack is { } p2 ? pq / p2 : null; }
                }

                // A typo in AMOUNT (DRB has a lone "`") is rebuilt from QTY × SALES PRICE when the file has a price.
                if (!Number(Raw(Col.Amount), out var amount))
                {
                    if (Text(Col.Amount) == "" && !cols.ContainsKey(Col.Qty)) amount = 0;
                    else if (Number(Raw(Col.Price), out var price) && qty != 0) { amount = qty * price; rebuilt++; }
                    else { Error($"Row {rowNo}: NET AMOUNT \"{Text(Col.Amount)}\" is not a number."); continue; }
                }
                // A product the RDP never matched to a RAM code is still a sale: kept (RdpSalesService tries its description).

                // Cases straight from the file (QTY IN CASE), else quantity ÷ the pack size when the unit is pieces.
                if (Number(Raw(Col.QtyCases), out var qc)) cases = qc;
                if (cases is null && pack is { } p3 && (uom == "" || IsPieceUnit(uom)))
                {
                    cases = qty / p3;
                    if (uom == "") uom = "PCS";
                }
                if (uom == "" && cases is not null) uom = cases == qty ? "CASE" : "PCS";   // no unit column

                // The Reference sheet: credits, returns and discounts reduce net purchases, whatever sign the file used.
                if (type is "Credit Memo" or "Return" or "Discount") amount = -Math.Abs(amount);
                if (type is "Credit Memo" or "Return") { qty = -Math.Abs(qty); cases = cases is { } c ? -Math.Abs(c) : null; }

                var salesman = Text(Col.Salesman).ToUpperInvariant();
                if (salesman == "0") salesman = "";   // a blank lookup exported as 0

                rows.Add(new Row(rowNo, custKey, P(Label(Text(Col.RdpName))), period.Value, invDate, P(Text(Col.InvNo)), type,
                    P(acctCode), P(acctName), P(AccountKey(acctCode, acctName, city)), P(Label(Text(Col.Channel))),
                    P(Text(Col.Province).ToUpperInvariant()), P(city.ToUpperInvariant()), P(salesman), P(sku), P(Text(Col.Item)),
                    qty, P(uom), Math.Round(amount, 2), cases is { } cv ? Math.Round(cv, 4) : null));
            }
        }
        finally { dataReader?.Dispose(); }

        var notes = new List<string>();
        if (subtotals > 0) notes.Add($"{subtotals:N0} subtotal row(s) skipped.");
        if (otherPrincipal > 0) notes.Add($"{otherPrincipal:N0} row(s) of other principals (not RAM) skipped.");
        if (noAccount > 0) notes.Add($"{noAccount:N0} row(s) with no account code or name skipped.");
        if (badDates > 0) notes.Add($"{badDates:N0} row(s) have an impossible invoice date; kept under their MONTH / YEAR, without the date.");
        if (rebuilt > 0) notes.Add($"{rebuilt:N0} row(s) had no usable amount; it was rebuilt from quantity × price.");
        return new(rows, errors, errorCount, blank, notes);
    }

    /// <summary>The outlet's identity within its distributor: its code, else name + city (Data Fields, "Account Code").</summary>
    public static string AccountKey(string code, string name, string city) =>
        code != "" ? code.ToUpperInvariant() : $"~{Label(name)}|{Label(city)}";

    /// <summary>Normalised label: trimmed, upper case, single spaces (RdpChannelMap.Label).</summary>
    public static string Label(string? s) => Squash(s ?? "").ToUpperInvariant();

    /// <summary>Quantity in cases: case-type units as is, piece-type units ÷ the SKU's pack size; null otherwise.</summary>
    public static decimal? Cases(decimal qty, string uom, int? pieces) =>
        IsCaseUnit(uom) ? qty
        : IsPieceUnit(uom) && pieces > 0 ? Math.Round(qty / pieces.Value, 4)
        : null;

    // CS, CASE, CTN, plus the pack size some systems append: "CS48", "CASE12", "CSE6", "CS ( 48 Pcs )".
    private static bool IsCaseUnit(string uom) => CaseUnit().IsMatch(uom);
    private static bool IsPieceUnit(string uom) => PieceUnits.Contains(uom);

    [GeneratedRegex(@"^(CASES?|CSE?|CA|CTN|BOX|BX)\s*(\d+|\(.*\))?$")]
    private static partial Regex CaseUnit();

    private static readonly HashSet<string> PieceUnits =
        ["PC", "PCS", "PIECE", "PIECES", "PACK", "PK", "EA", "EACH", "BOT", "CAN", "TIN", "POUCH"];

    /// <summary>A RAM SKU code inside item text: "9318 (RAM…)", "RAM FOODS:9052 (…)", "NRF9712.1 (…)", "9912RAM (…)".</summary>
    private static string? SkuIn(string text) => SkuInText().Match(text) is { Success: true } m ? m.Groups[1].Value : null;

    [GeneratedRegex(@"(?<![\d.])(\d{4,5})(?:\.\d+|[A-Za-z]+)?\s*\(")]
    private static partial Regex SkuInText();

    [GeneratedRegex(@"^[A-Za-z]{0,4}(\d{4,5})$")]
    private static partial Regex SkuAltCode();

    private static bool IsTotal(string s) => s.StartsWith("total", StringComparison.OrdinalIgnoreCase);

    private static string? FirstText(object?[] cells) =>
        cells.Select(c => c as string).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) is { } s ? Squash(s) : null;

    private static string TxnType(string s)
    {
        var t = s.ToLowerInvariant().Replace("-", " ").Replace(".", "").Trim();
        return t switch
        {
            "" => "",
            _ when t.Contains("return") || t is "ret" or "sr" or "rs" => "Return",
            _ when t.StartsWith("credit") || t is "cm" => "Credit Memo",
            _ when t.StartsWith("disc") => "Discount",
            _ when t.StartsWith("free") || t is "fg" || t.Contains("claim") => "Free Goods",
            _ when t.StartsWith("adj") => "Adjustment",
            _ when t.StartsWith("inv") || t.Contains("invoice") || t.Contains("sales") || t is "si" or "sale" or "dr" or "delivery receipt" => "Invoice",
            _ => s.Length > 20 ? s[..20] : s,
        };
    }

    private static readonly string[] MonthNames = CultureInfo.InvariantCulture.DateTimeFormat.MonthNames;

    private static DateOnly? PeriodOf(string month, string year)
    {
        if (month == "" || !int.TryParse(year.Split('.')[0], out var y) || y < 2000 || y > 2100) return null;
        return MonthIndex(month) is { } m ? new DateOnly(y, m, 1) : null;
    }

    /// <summary>A MONTH column without YEAR ("JUNE"): that month in the invoice date's year (December billed in January → last year).</summary>
    private static DateOnly? MonthOfYear(string month, DateOnly invDate)
    {
        if (MonthIndex(month) is not { } m) return null;
        var y = invDate.Year + (m - invDate.Month > 6 ? -1 : m - invDate.Month < -6 ? 1 : 0);
        return new DateOnly(y, m, 1);
    }

    /// <summary>1–12 from "JUNE", "Jun" or "6"; null otherwise.</summary>
    private static int? MonthIndex(string month)
    {
        if (int.TryParse(month.Split('.')[0], out var n)) return n is >= 1 and <= 12 ? n : null;
        if (month.Length < 3 || !char.IsLetter(month[0])) return null;
        var idx = Array.FindIndex(MonthNames, x => x != "" && x.StartsWith(month[..3], StringComparison.OrdinalIgnoreCase));
        return idx >= 0 ? idx + 1 : null;
    }

    private static DateOnly? ParsePeriod(object? raw)
    {
        if (ParseDate(raw) is { } d) return new DateOnly(d.Year, d.Month, 1);
        var s = Squash(raw?.ToString() ?? "");
        return DateTime.TryParseExact(s, ["yyyy-MM", "yyyy/MM", "MMM yyyy", "MMMM yyyy", "MMM-yyyy", "MM/yyyy", "MMMM, yyyy", "MMM, yyyy"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var p) ? new DateOnly(p.Year, p.Month, 1) : null;
    }

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy/MM/dd", "dd-MMM-yyyy", "d-MMM-yyyy", "dd-MMM-yy", "d-MMM-yy",
        "dd MMM yyyy", "MMM d, yyyy", "M/d/yyyy", "M-d-yyyy", "d/M/yyyy", "d-M-yyyy", "M/d/yy"];

    /// <summary>A date cell. Text like 02/09/2026 is ambiguous: the reading in <paramref name="hintMonth"/> wins, else month first.</summary>
    private static DateOnly? ParseDate(object? raw, int? hintMonth = null)
    {
        switch (raw)
        {
            case DateTime dt: return DateOnly.FromDateTime(dt);
            case double n: return n is > 36000 and < 73000 ? DateOnly.FromDateTime(DateTime.FromOADate(n)) : null;   // Excel serial, 1998–2099
            case string s:
                s = Squash(s);
                // Also accept a trailing time ("2026-09-14 00:00:00").
                DateOnly? first = null;
                foreach (var candidate in new[] { s, s.Split(' ')[0] })
                    foreach (var f in DateFormats)
                        if (DateTime.TryParseExact(candidate, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                        {
                            if (hintMonth is null || d.Month == hintMonth) return DateOnly.FromDateTime(d);
                            first ??= DateOnly.FromDateTime(d);
                        }
                return first;
            default: return null;
        }
    }

    private static bool Number(object? raw, out decimal value)
    {
        value = 0;
        switch (raw)
        {
            case double d: value = (decimal)d; return true;
            case null: return false;
        }
        var s = Squash(raw.ToString() ?? "").Replace(",", "").Replace("₱", "").Replace("PHP", "", StringComparison.OrdinalIgnoreCase).Trim();
        var negative = s.StartsWith('(') && s.EndsWith(')');
        if (negative) s = s[1..^1];
        if (!decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
        if (negative) value = -value;
        return true;
    }

    /// <summary>Known columns of a header row; the best-ranked header wins when two name the same column.</summary>
    private static Dictionary<Col, int>? MapColumns(object?[] row)
    {
        var found = new Dictionary<Col, (int Rank, int At)>();
        var texts = row.Select(c => c is string s ? Squash(s).ToLowerInvariant() : "").ToArray();
        int? nameAt = null;
        for (var i = 0; i < texts.Length; i++)
        {
            if (!AliasIndex.TryGetValue(texts[i], out var a)) continue;
            if (texts[i] == "area" && texts.Count(t => t == "area") > 1) continue;
            if (texts[i] == "name") { nameAt ??= i; continue; }
            if (!found.TryGetValue(a.Col, out var cur) || a.Rank < cur.Rank) found[a.Col] = (a.Rank, i);
        }
        if (nameAt is { } n)
        {
            if (!found.ContainsKey(Col.AcctName)) found[Col.AcctName] = (int.MaxValue, n);
            else if (!found.ContainsKey(Col.AcctCode)) found[Col.AcctCode] = (int.MaxValue, n);
        }
        return found.Count >= 3 ? found.ToDictionary(x => x.Key, x => x.Value.At) : null;
    }

    // ── grid readers ────────────────────────────────────────────────────────

    private static IEnumerable<object?[]> Rows(IExcelDataReader reader, int width = int.MaxValue)
    {
        while (reader.Read())
        {
            var row = new object?[Math.Min(reader.FieldCount, width)];
            for (var i = 0; i < row.Length; i++) row[i] = reader.GetValue(i);
            yield return row;
        }
    }

    private static List<object?[]> ReadCsv(Stream content)
    {
        using var sr = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var rows = new List<object?[]>();
        while (sr.ReadLine() is { } line) rows.Add(SplitCsv(line).Cast<object?>().ToArray());
        return rows;
    }

    private static List<string> SplitCsv(string line)
    {
        var cells = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else sb.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { cells.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        cells.Add(sb.ToString());
        return cells;
    }

    private static string Squash(string s) => Spaces().Replace(s, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
