using System.Data;
using HOMSys.Application.DTOs.Rdp;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;

namespace HOMSys.Infrastructure.Rdp;

/// <summary>
/// RDP (RAM Distributor Partner) sales files: preview + import, upload history, the RDP list and channel mapping.
/// An import replaces every month of each distributor that the file contains (the template asks distributors to
/// "always send the complete month again" when correcting), so re-uploading a file is safe.
/// </summary>
public class RdpSalesService(AppDbContext db)
{
    /// <summary>GT MIN's year-to-date workbook is 37 MB (.xlsb).</summary>
    public const long MaxFileBytes = 60 * 1024 * 1024;

    private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);
    private static string Ym(DateOnly d) => d.ToString("yyyy-MM");

    // ── upload ──────────────────────────────────────────────────────────────

    private record Prepared(RdpUploadPreviewDto Preview, List<RdpSalesLine> Lines);

    private async Task<(Prepared? p, string? error)> PrepareAsync(int? distributorId, string fileName, Stream content, long length)
    {
        if (length <= 0) return (null, "The file is empty.");
        if (length > MaxFileBytes) return (null, $"The file is larger than {MaxFileBytes / 1024 / 1024} MB. Split it by month.");
        if (!RdpSalesParser.Extensions.Contains(Path.GetExtension(fileName).ToLowerInvariant()))
            return (null, "Upload an Excel file (.xlsx / .xlsb / .xls) or a .csv with the template's columns.");

        RdpSalesParser.Result parsed;
        try { parsed = RdpSalesParser.Parse(content, fileName); }
        catch (Exception ex) { return (null, $"The file could not be read: {ex.Message}"); }

        var distributors = await db.RdpDistributors.AsNoTracking().ToListAsync();
        var picked = distributorId is { } pid ? distributors.FirstOrDefault(d => d.Id == pid) : null;
        if (distributorId is not null && picked is null) return (null, "Pick an RDP from the list.");
        var byKey = distributors.ToDictionary(d => d.CustKey);
        var byName = new Dictionary<string, RdpDistributor?>();
        var unknownNames = new Dictionary<string, int>();
        var errors = parsed.Errors.ToList();
        var errorCount = parsed.ErrorCount;
        void Error(string msg) { errorCount++; if (errors.Count < 50) errors.Add(msg); }

        var productList = await db.Products.AsNoTracking()
            .Select(p => new RdpSkuMatcher.Product(p.CProdNo, p.ProdDesc, p.Pieces, p.QtyPerPc, p.Um)).ToListAsync();
        var products = productList.GroupBy(p => p.CProdNo).ToDictionary(g => g.Key, g => g.First());
        var matcher = new RdpSkuMatcher(productList);
        var matchedByDesc = 0;
        var maps = await db.RdpChannelMaps.AsNoTracking().Select(m => m.Label).ToListAsync();
        var mapped = maps.ToHashSet();

        var lines = new List<RdpSalesLine>(parsed.Rows.Count);
        foreach (var r in parsed.Rows)
        {
            RdpDistributor? d;
            if (r.CustKey is { } ck)
            {
                if (!byKey.TryGetValue(ck, out d)) { Error($"Row {r.RowNo}: RDP CUSTKEY {ck} is not in the RDP list."); continue; }
                if (picked is not null && d.Id != picked.Id)
                { Error($"Row {r.RowNo}: RDP CUSTKEY {ck} ({d.Name}) is not the RDP you picked ({picked.CustKey} {picked.Name})."); continue; }
            }
            else if (r.RdpName != "")
            {
                if (!byName.TryGetValue(r.RdpName, out d)) byName[r.RdpName] = d = ResolveName(distributors, r.RdpName);
                // A distributor the RDP list doesn't have (GT MIN carries a few) is left out and listed, not an error.
                if (d is null) { unknownNames[r.RdpName] = unknownNames.GetValueOrDefault(r.RdpName) + 1; continue; }
                if (picked is not null && d.Id != picked.Id)
                { Error($"Row {r.RowNo}: RDP \"{r.RdpName}\" ({d.CustKey} {d.Name}) is not the RDP you picked ({picked.CustKey} {picked.Name})."); continue; }
            }
            else if (picked is not null) d = picked;
            else { Error($"Row {r.RowNo}: RDP CUSTKEY is blank — fill it, or pick the RDP before uploading."); continue; }

            var sku = r.SkuCode;
            if (sku == "" && r.ItemDesc != "" && r.TxnType != "Discount" && matcher.Match(r.ItemDesc) is { } m) { sku = m; matchedByDesc++; }
            var product = products.GetValueOrDefault(sku);
            lines.Add(new RdpSalesLine
            {
                DistributorId = d.Id, Period = r.Period, InvoiceDate = r.InvoiceDate, InvoiceNo = Cut(r.InvoiceNo, 50), TxnType = r.TxnType,
                AccountCode = Cut(r.AccountCode, 50), AccountName = Cut(r.AccountName, 200), AccountKey = Cut(r.AccountKey, 260),
                ChannelLabel = Cut(r.ChannelLabel, 100), Province = Cut(r.Province, 100), City = Cut(r.City, 100), Salesman = Cut(r.Salesman, 100),
                SkuCode = Cut(sku, 20), ItemDesc = Cut(r.ItemDesc, 200), Quantity = r.Quantity, Uom = Cut(r.Uom, 20),
                Cases = r.Cases ?? RdpSalesParser.Cases(r.Quantity, r.Uom, product?.Pieces), NetAmount = r.NetAmount,
            });
        }
        if (lines.Count == 0 && errorCount == 0) Error("The file has no data rows.");

        var purchases = lines.Where(l => !RdpSalesParser.NonPurchaseTypes.Contains(l.TxnType)).ToList();
        var pairs = lines.GroupBy(l => l.DistributorId).ToDictionary(g => g.Key, g => g.Select(l => l.Period).Distinct().OrderBy(p => p).ToList());
        var existing = new Dictionary<int, int>();
        foreach (var (did, periods) in pairs)
            existing[did] = await db.RdpSalesLines.CountAsync(l => l.DistributorId == did && periods.Contains(l.Period));

        var perDistributor = lines.GroupBy(l => l.DistributorId).Select(g =>
        {
            var d = distributors.First(x => x.Id == g.Key);
            return new RdpPreviewDistributorDto(d.Id, d.CustKey, d.Name, pairs[g.Key].Select(Ym).ToList(), g.Count(),
                g.Where(l => !RdpSalesParser.NonPurchaseTypes.Contains(l.TxnType)).Sum(l => l.NetAmount),
                g.Select(l => l.AccountKey).Distinct().Count(), existing[g.Key]);
        }).OrderBy(x => x.Name).ToList();

        var unknownSkus = lines.Where(l => l.SkuCode != "" && !products.ContainsKey(l.SkuCode)).GroupBy(l => l.SkuCode)
            .Select(g => new RdpCodeCountDto(g.Key, g.Select(l => l.ItemDesc).FirstOrDefault(s => s != "") ?? "", g.Count()))
            .OrderByDescending(x => x.Lines).ToList();
        var unmapped = lines.Where(l => l.ChannelLabel != "" && !mapped.Contains(l.ChannelLabel)).GroupBy(l => l.ChannelLabel)
            .Select(g => new RdpCodeCountDto(g.Key, "", g.Count())).OrderByDescending(x => x.Lines).ToList();

        var notes = parsed.Notes.ToList();
        var stillNoSku = lines.Count(l => l.SkuCode == "" && l.TxnType != "Discount");
        if (matchedByDesc > 0)
            notes.Add($"{matchedByDesc:N0} line(s) without a SKU code were matched to a RAM SKU by description (name + pieces + size).");
        if (stillNoSku > 0) notes.Add($"{stillNoSku:N0} line(s) have no RAM SKU code; they are kept and show as UNTAGGED.");
        if (unknownNames.Count > 0)
            notes.Add("Not in the RDP list, left out (add the RDP or its file name on the RDP list tab, then upload again): "
                      + string.Join(", ", unknownNames.OrderByDescending(x => x.Value).Select(x => $"{x.Key} ({x.Value:N0} lines)")) + ".");

        var preview = new RdpUploadPreviewDto(fileName, lines.Count, parsed.BlankRows, purchases.Sum(l => l.NetAmount),
            lines.Select(l => (l.DistributorId, l.AccountKey)).Distinct().Count(), perDistributor, errors, errorCount,
            unknownSkus, unmapped, lines.Count(l => l.ChannelLabel == ""), lines.Count(l => l.AccountCode == ""),
            lines.Count(l => l.Cases is null && l.SkuCode != ""), existing.Values.Sum(), notes);
        return (new Prepared(preview, lines), null);
    }

    /// <summary>
    /// A distributor named in the file: one of its FileNames, its full name, or a first word only one RDP has
    /// ("BERTAHAN" → BERTAHAN DISTRIBUTION CORPORATION). Null when unknown or ambiguous.
    /// </summary>
    private static RdpDistributor? ResolveName(List<RdpDistributor> all, string name)
    {
        var hits = all.Where(d => RdpSalesParser.Label(d.Name) == name
            || d.FileNames.Split(';', StringSplitOptions.RemoveEmptyEntries).Any(f => RdpSalesParser.Label(f) == name)).ToList();
        if (hits.Count == 0) hits = all.Where(d => RdpSalesParser.Label(d.Name).StartsWith(name + " ")).ToList();
        return hits.Count == 1 ? hits[0] : null;
    }

    private static string Cut(string s, int max) => s.Length > max ? s[..max] : s;

    public async Task<(RdpUploadPreviewDto? data, string? error)> PreviewAsync(int? distributorId, string fileName, Stream content, long length)
    {
        var (p, error) = await PrepareAsync(distributorId, fileName, content, length);
        return (p?.Preview, error);
    }

    public async Task<(RdpUploadDto? data, string? error)> UploadAsync(int? distributorId, string fileName, Stream content, long length, string me)
    {
        var (p, error) = await PrepareAsync(distributorId, fileName, content, length);
        if (p is null) return (null, error);
        if (p.Preview.ErrorCount > 0) return (null, $"The file has {p.Preview.ErrorCount} error(s); fix them and upload again. First: {p.Preview.Errors.FirstOrDefault()}");

        var upload = new RdpSalesUpload
        {
            FileName = Cut(Path.GetFileName(fileName), 260),
            CustKeys = Cut(string.Join(",", p.Preview.Distributors.Select(d => d.CustKey)), 500),
            Periods = Cut(string.Join(",", p.Lines.Select(l => Ym(l.Period)).Distinct().Order()), 500),
            LineCount = p.Lines.Count, NetAmount = p.Preview.NetAmount, UploadedBy = me,
        };

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var replaced = 0;
            foreach (var g in p.Lines.GroupBy(l => l.DistributorId))
            {
                var periods = g.Select(l => l.Period).Distinct().ToList();
                replaced += await db.RdpSalesLines.Where(l => l.DistributorId == g.Key && periods.Contains(l.Period)).ExecuteDeleteAsync();
            }
            upload.ReplacedLines = replaced;
            db.RdpSalesUploads.Add(upload);
            await db.SaveChangesAsync();
            foreach (var l in p.Lines) l.UploadId = upload.Id;
            await BulkInsertAsync(p.Lines, tx);
            await tx.CommitAsync();
        });
        RdpUbaEngine.Warm(db.Database.GetConnectionString());   // the UBA dashboard's data changed
        return (await UploadDtoAsync(upload.Id), null);
    }

    private async Task BulkInsertAsync(List<RdpSalesLine> lines, IDbContextTransaction tx)
    {
        var table = new DataTable();
        foreach (var (name, type) in new (string, Type)[]
        {
            ("UploadId", typeof(int)), ("DistributorId", typeof(int)), ("Period", typeof(DateTime)), ("InvoiceDate", typeof(DateTime)),
            ("InvoiceNo", typeof(string)), ("TxnType", typeof(string)), ("AccountCode", typeof(string)), ("AccountName", typeof(string)),
            ("AccountKey", typeof(string)), ("ChannelLabel", typeof(string)), ("Province", typeof(string)), ("City", typeof(string)),
            ("Salesman", typeof(string)), ("SkuCode", typeof(string)), ("ItemDesc", typeof(string)), ("Quantity", typeof(decimal)),
            ("Uom", typeof(string)), ("Cases", typeof(decimal)), ("NetAmount", typeof(decimal)),
        }) table.Columns.Add(name, type);
        var conn = (SqlConnection)db.Database.GetDbConnection();
        using var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.CheckConstraints, (SqlTransaction)tx.GetDbTransaction())
        {
            DestinationTableName = "RdpSalesLines", BatchSize = 5000, BulkCopyTimeout = 600,
        };
        foreach (DataColumn c in table.Columns) bulk.ColumnMappings.Add(c.ColumnName, c.ColumnName);

        // In chunks, so a 650k-line workbook never sits in one DataTable.
        foreach (var chunk in lines.Chunk(50_000))
        {
            table.Clear();
            foreach (var l in chunk)
                table.Rows.Add(l.UploadId, l.DistributorId, l.Period.ToDateTime(TimeOnly.MinValue),
                    l.InvoiceDate is { } d ? d.ToDateTime(TimeOnly.MinValue) : DBNull.Value, l.InvoiceNo, l.TxnType, l.AccountCode, l.AccountName,
                    l.AccountKey, l.ChannelLabel, l.Province, l.City, l.Salesman, l.SkuCode, l.ItemDesc, l.Quantity, l.Uom,
                    l.Cases is { } c ? c : DBNull.Value, l.NetAmount);
            await bulk.WriteToServerAsync(table);
        }
    }

    public async Task<List<RdpUploadDto>> ListUploadsAsync()
    {
        var ids = await db.RdpSalesUploads.OrderByDescending(u => u.Id).Select(u => u.Id).Take(500).ToListAsync();
        return await UploadDtosAsync(ids);
    }

    private async Task<RdpUploadDto> UploadDtoAsync(int id) => (await UploadDtosAsync([id])).Single();

    private async Task<List<RdpUploadDto>> UploadDtosAsync(List<int> ids)
    {
        var uploads = await db.RdpSalesUploads.AsNoTracking().Where(u => ids.Contains(u.Id)).OrderByDescending(u => u.Id).ToListAsync();
        var current = await CachedAsync($"upload-lines|{string.Join(",", ids)}", () =>
            db.RdpSalesLines.Where(l => ids.Contains(l.UploadId)).GroupBy(l => l.UploadId)
                .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N));
        var names = await db.RdpDistributors.AsNoTracking().ToDictionaryAsync(d => d.CustKey.ToString(), d => d.Name);
        return uploads.Select(u => new RdpUploadDto(u.Id, u.FileName,
            string.Join(", ", u.CustKeys.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(k => names.TryGetValue(k, out var n) ? $"{n} ({k})" : k)),
            u.Periods, u.LineCount, u.NetAmount, u.ReplacedLines, current.GetValueOrDefault(u.Id), Utc(u.UploadedAt), u.UploadedBy)).ToList();
    }

    public const int MaxLinesPage = 500;

    /// <summary>
    /// The imported lines in the template's columns, in file order (Id). Filters: upload, RDP, month (yyyy-MM) and a
    /// text search over invoice no., account, SKU, item and salesman. Totals cover every matching line, not just the page.
    /// </summary>
    public async Task<(RdpLinesDto? data, string? error)> ListLinesAsync(int? uploadId, int? distributorId, string? period,
        string? search, int skip, int take)
    {
        var q = db.RdpSalesLines.AsNoTracking();
        if (uploadId is int u) q = q.Where(l => l.UploadId == u);
        if (distributorId is int d) q = q.Where(l => l.DistributorId == d);
        if (!string.IsNullOrWhiteSpace(period))
        {
            if (!DateOnly.TryParseExact(period.Trim() + "-01", "yyyy-MM-dd", out var p)) return (null, "Month must be yyyy-MM.");
            q = q.Where(l => l.Period == p);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(l => l.InvoiceNo.Contains(s) || l.AccountCode.Contains(s) || l.AccountName.Contains(s)
                          || l.SkuCode.Contains(s) || l.ItemDesc.Contains(s) || l.Salesman.Contains(s) || l.City.Contains(s));
        }

        // The totals cover every matching line (a scan of up to a million); kept per filter until the next upload / delete,
        // so paging and coming back to the tab don't recount.
        var totals = await CachedAsync($"lines|{uploadId}|{distributorId}|{period}|{search?.Trim()}", async () =>
        {
            var t = await q.GroupBy(_ => 1)
                .Select(g => new { N = g.Count(), Qty = g.Sum(l => l.Quantity), Net = g.Sum(l => l.NetAmount) }).FirstOrDefaultAsync();
            return (N: t?.N ?? 0, Qty: t?.Qty ?? 0m, Net: t?.Net ?? 0m);
        });
        var rows = await q.OrderBy(l => l.Id).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, MaxLinesPage))
            .Join(db.RdpDistributors, l => l.DistributorId, dist => dist.Id, (l, dist) => new RdpLineDto(l.Id, dist.CustKey, dist.Name,
                l.Period, l.InvoiceDate, l.InvoiceNo, l.TxnType, l.AccountCode, l.AccountName, l.ChannelLabel, l.Province, l.City,
                l.Salesman, l.SkuCode, l.ItemDesc, l.Quantity, l.Uom, l.NetAmount))
            .ToListAsync();
        return (new RdpLinesDto(totals.N, totals.Qty, totals.Net, rows), null);
    }

    // Line counts and totals, which only change when lines do (upload / delete): keyed by the upload version.
    private static readonly MemoryCache LineStats = new(new MemoryCacheOptions { SizeLimit = 300 });

    private async Task<T> CachedAsync<T>(string key, Func<Task<T>> run)
    {
        var v = await db.RdpSalesUploads.GroupBy(_ => 1).Select(g => new { N = g.Count(), Max = g.Max(x => x.Id) }).FirstOrDefaultAsync();
        key = $"{v?.N}.{v?.Max}|{key}";
        if (LineStats.TryGetValue(key, out T? hit)) return hit!;
        var value = await run();
        LineStats.Set(key, value, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });
        return value;
    }

    /// <summary>Removes an upload and whatever of its lines are still current (months a later file replaced are already gone).</summary>
    public async Task<(bool data, string? error)> DeleteUploadAsync(int id)
    {
        var upload = await db.RdpSalesUploads.FindAsync(id);
        if (upload is null) return (false, "Upload not found.");
        await db.RdpSalesLines.Where(l => l.UploadId == id).ExecuteDeleteAsync();
        db.RdpSalesUploads.Remove(upload);
        await db.SaveChangesAsync();
        RdpUbaEngine.Warm(db.Database.GetConnectionString());   // the UBA dashboard's data changed
        return (true, null);
    }

    // ── RDP list ────────────────────────────────────────────────────────────

    public async Task<List<RdpDistributorDto>> ListDistributorsAsync()
    {
        var stats = await CachedAsync("distributor-lines", () => db.RdpSalesLines.GroupBy(l => l.DistributorId)
            .Select(g => new { g.Key, N = g.Count(), First = g.Min(l => l.Period), Last = g.Max(l => l.Period) })
            .ToDictionaryAsync(x => x.Key));
        var list = await db.RdpDistributors.AsNoTracking().Include(d => d.Site).OrderBy(d => d.Name).ThenBy(d => d.CustKey).ToListAsync();
        return list.Select(d =>
        {
            var s = stats.GetValueOrDefault(d.Id);
            return new RdpDistributorDto(d.Id, d.CustKey, d.Name, d.Rdm, d.Area, d.Territory, d.SiteId, d.Site?.Name, d.IsActive,
                s?.N ?? 0, s is null ? null : Ym(s.First), s is null ? null : Ym(s.Last), d.FileNames);
        }).ToList();
    }

    public async Task<(RdpDistributorDto? data, string? error)> SaveDistributorAsync(int? id, SaveRdpDistributorDto dto, string me)
    {
        var name = (dto.Name ?? "").Trim().ToUpperInvariant();
        if (dto.CustKey <= 0) return (null, "RDP CUSTKEY is required.");
        if (name == "") return (null, "Distributor name is required.");
        if (await db.RdpDistributors.AnyAsync(d => d.CustKey == dto.CustKey && d.Id != id))
            return (null, $"RDP CUSTKEY {dto.CustKey} is already in the list.");
        if (dto.SiteId is { } sid && !await db.Sites.AnyAsync(s => s.Id == sid)) return (null, "Servicing branch not found.");

        RdpDistributor? d;
        if (id is null) db.RdpDistributors.Add(d = new RdpDistributor());
        else if ((d = await db.RdpDistributors.FindAsync(id)) is null) return (null, "RDP not found.");
        else if (d.CustKey != dto.CustKey && await db.RdpSalesLines.AnyAsync(l => l.DistributorId == d.Id))
            return (null, "The CUSTKEY of an RDP that has uploaded sales can't change. Add a new RDP instead.");

        d.CustKey = dto.CustKey;
        d.Name = name;
        d.Rdm = (dto.Rdm ?? "").Trim().ToUpperInvariant();
        d.Area = (dto.Area ?? "").Trim().ToUpperInvariant();
        d.Territory = (dto.Territory ?? "").Trim();
        d.SiteId = dto.SiteId;
        d.FileNames = Cut(string.Join("; ", (dto.FileNames ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)), 500);
        d.IsActive = dto.IsActive;
        d.UpdatedAt = DateTime.UtcNow;
        d.UpdatedBy = me;
        await db.SaveChangesAsync();
        RdpUbaEngine.Warm(db.Database.GetConnectionString());   // the UBA dashboard's data changed
        return ((await ListDistributorsAsync()).First(x => x.Id == d.Id), null);
    }

    // ── channel mapping ─────────────────────────────────────────────────────

    public async Task<RdpChannelsDto> ListChannelsAsync()
    {
        var used = await db.RdpSalesLines.Where(l => l.ChannelLabel != "").GroupBy(l => l.ChannelLabel)
            .Select(g => new { g.Key, N = g.Count(), D = g.Select(l => l.DistributorId).Distinct().Count() }).ToListAsync();
        var maps = await db.RdpChannelMaps.AsNoTracking().OrderBy(m => m.Channel).ThenBy(m => m.Label).ToListAsync();
        var mapped = maps.Select(m => m.Label).ToHashSet();
        var counts = used.ToDictionary(u => u.Key, u => u.N);
        return new RdpChannelsDto(RdpModel.Channels.ToList(),
            maps.Select(m => new RdpChannelMapDto(m.Id, m.Label, m.Channel, counts.GetValueOrDefault(m.Label))).ToList(),
            used.Where(u => !mapped.Contains(u.Key)).OrderByDescending(u => u.N).Select(u => new RdpUnmappedLabelDto(u.Key, u.N, u.D)).ToList());
    }

    public async Task<(RdpChannelMapDto? data, string? error)> SaveChannelMapAsync(SaveRdpChannelMapDto dto, string me)
    {
        var label = RdpSalesParser.Label(dto.Label);
        if (label == "") return (null, "Label is required.");
        if (label.Length > 100) return (null, "Label is longer than 100 characters.");
        if (!RdpModel.Channels.Contains(dto.Channel)) return (null, "Pick a channel type from RAM's standard list.");
        var map = await db.RdpChannelMaps.FirstOrDefaultAsync(m => m.Label == label);
        if (map is null) db.RdpChannelMaps.Add(map = new RdpChannelMap { Label = label });
        map.Channel = dto.Channel;
        map.UpdatedAt = DateTime.UtcNow;
        map.UpdatedBy = me;
        await db.SaveChangesAsync();
        RdpUbaEngine.Warm(db.Database.GetConnectionString());   // the UBA dashboard's data changed
        return (new RdpChannelMapDto(map.Id, map.Label, map.Channel, await db.RdpSalesLines.CountAsync(l => l.ChannelLabel == label)), null);
    }

    public async Task<(bool data, string? error)> DeleteChannelMapAsync(int id)
    {
        var map = await db.RdpChannelMaps.FindAsync(id);
        if (map is null) return (false, "Mapping not found.");
        db.RdpChannelMaps.Remove(map);
        await db.SaveChangesAsync();
        RdpUbaEngine.Warm(db.Database.GetConnectionString());   // the UBA dashboard's data changed
        return (true, null);
    }
}
