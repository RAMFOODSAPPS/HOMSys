using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data.Dbf;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text.Json;

namespace HOMSys.Infrastructure.Data;

/// <summary>
/// Imports pricing masters directly from HO's production drive (F:\) —
/// every branch's own zone/zone2/customer-zone files, not one branch's
/// staged copy. Branches feeding this are discovered dynamically from the
/// folders present on disk, so a branch added later needs no code change.
///
/// See C:\Users\RDEGUZMAN\.claude\plans\can-you-see-this-jaunty-puffin.md.
///
/// Run with:  dotnet run --project HOMSys.API -- import-HoMaster-data [root]
/// Default root: F:\ (expects \PMDM, \AUTOPROG\ADDON, \AUTOPROG\CUSTOMER under it).
/// </summary>
public class PricingDataImporter(AppDbContext db)
{
    public const string DefaultRoot = @"F:\";

    private const int BatchSize = 5000;

    /// <summary>
    /// Only these branches are live — confirmed against BMS_auto_Distribution's
    /// branch list (2026-09-03). Every other folder on F:\ (closed branches like
    /// VAL, no-sales branches like SVS, and unrecognized/legacy folders) is
    /// excluded from both ADDON and CUSTOMER sync, even if it has valid DBFs.
    /// </summary>
    internal static readonly HashSet<string> ActiveBranches = new(StringComparer.OrdinalIgnoreCase)
    {
        "bac", "but", "cdo", "ceb", "dav", "dum", "gen", "hon",
        "ilo", "kid", "oza", "rox", "tac", "tgm", "zam", "pan"
    };

    private static string PmdmPath(string root) => Path.Combine(root, "PMDM");
    private static string AddonRoot(string root) => Path.Combine(root, "AUTOPROG", "ADDON");
    private static string CustomerRoot(string root) => Path.Combine(root, "AUTOPROG", "CUSTOMER");
    /// <summary>
    /// F:\ is read-only (see the pricing subsystem's own golden rule) — the
    /// marker lives in HOMSys's own app data, not on the production drive.
    /// JSON, not the old single-timestamp .txt: tracks a per-file mtime so
    /// each file's import can be skipped independently instead of an
    /// all-or-nothing gate that forced every branch to truncate+reload
    /// whenever any one branch's file changed.
    /// </summary>
    /// <summary>Always resolves to the same file regardless of root — the marker lives in
    /// %ProgramData%, not on F:\, since that drive is read-only.</summary>
    public static string MarkerPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "HOMSys", ".last-pricing-import.json");

    /// <summary>
    /// Guards against overlapping syncs — the watcher's 5-min timer and a
    /// manual trigger (or two watcher instances) can otherwise both truncate
    /// the same table at once and either crash on a duplicate key or, worse,
    /// silently double-insert rows in tables with no unique constraint.
    /// </summary>
    private static readonly SemaphoreSlim SyncLock = new(1, 1);

    public async Task<PricingImportResult> ImportAllAsync(string root, Action<string>? log = null)
    {
        log ??= _ => { };

        if (!await SyncLock.WaitAsync(0))
        {
            log("Another sync is already in progress — skipping.");
            return new PricingImportResult { Skipped = true };
        }
        try
        {
            return await ImportAllCoreAsync(root, log);
        }
        finally
        {
            SyncLock.Release();
        }
    }

    private async Task<PricingImportResult> ImportAllCoreAsync(string root, Action<string> log)
    {
        var allAddonFolders = Directory.GetDirectories(AddonRoot(root)).Select(Path.GetFileName).OfType<string>().ToArray();
        var addonBranches = allAddonFolders
            .Where(b => ActiveBranches.Contains(b)
                     && File.Exists(Path.Combine(AddonRoot(root), b, "ZONE.DBF"))
                     && File.Exists(Path.Combine(AddonRoot(root), b, "ZONE2.DBF")))
            .ToArray();
        LogSkippedFolders(log, "ADDON", allAddonFolders, addonBranches);

        var allCustomerFolders = Directory.GetDirectories(CustomerRoot(root)).Select(Path.GetFileName).OfType<string>().ToArray();
        var customerBranches = allCustomerFolders
            .Where(b => ActiveBranches.Contains(b)
                     && File.Exists(Path.Combine(CustomerRoot(root), b, "CUST4WIN.DBF")))
            .ToArray();
        LogSkippedFolders(log, "CUSTOMER", allCustomerFolders, customerBranches);

        var prod4win = Path.Combine(PmdmPath(root), "PROD4WIN.DBF");
        var prchst = Path.Combine(PmdmPath(root), "PRCHST.DBF");
        var prodcat = Path.Combine(PmdmPath(root), "prodcat.dbf");
        var prlistx2 = Path.Combine(PmdmPath(root), "PRLISTX2.DBF");
        var prlistx = Path.Combine(PmdmPath(root), "PRLISTX.DBF");
        RequireFile(prod4win);
        RequireFile(prchst);
        RequireFile(prodcat);
        RequireFile(prlistx2);
        RequireFile(prlistx);

        var zoneFiles = addonBranches.Select(b => (Branch: b, File: Path.Combine(AddonRoot(root), b, "ZONE.DBF"))).ToArray();
        var zone2Files = addonBranches.Select(b => (Branch: b, File: Path.Combine(AddonRoot(root), b, "ZONE2.DBF"))).ToArray();
        var custFiles = customerBranches.Select(b => (Branch: b, File: Path.Combine(CustomerRoot(root), b, "CUST4WIN.DBF"))).ToArray();
        foreach (var file in zoneFiles.Concat(zone2Files).Concat(custFiles).Select(x => x.File))
            RequireFile(file);

        var markerPath = MarkerPath;
        var marker = LoadMarker(markerPath);

        var branchesChanged = !SetEquals(marker.AddonBranches, addonBranches)
                            || !SetEquals(marker.CustomerBranches, customerBranches);

        bool Changed(string file) =>
            !marker.FileMtimes.TryGetValue(file, out var prev) || File.GetLastWriteTimeUtc(file) > prev;

        var anyChange = branchesChanged || Changed(prod4win) || Changed(prchst) || Changed(prodcat) || Changed(prlistx2) || Changed(prlistx)
            || zoneFiles.Any(x => Changed(x.File)) || zone2Files.Any(x => Changed(x.File)) || custFiles.Any(x => Changed(x.File));

        if (!anyChange)
        {
            log("No changes since last sync — skipping.");
            return new PricingImportResult { Skipped = true };
        }

        var result = new PricingImportResult();

        if (Changed(prod4win))
            result.ProductsPriced = await ImportProductPricesAsync(root, log);
        else
            log("PROD4WIN.DBF unchanged — skipping product price update.");

        if (Changed(prchst))
            result.PriceHistoryRows = await ImportPriceHistoryAsync(root, log);
        else
            log("PRCHST.DBF unchanged — skipping price history reload.");

        if (Changed(prodcat))
            result.ProductCategories = await ImportProductCategoriesAsync(root, log);
        else
            log("prodcat.dbf unchanged — skipping product category reload.");

        if (Changed(prlistx2))
            result.PrlistX2Restrictions = await ImportPrlistX2RestrictionsAsync(root, log);
        else
            log("PRLISTX2.DBF unchanged — skipping pricelist restriction reload.");

        if (Changed(prlistx))
            result.PrlistXRestrictions = await ImportPrlistXRestrictionsAsync(root, log);
        else
            log("PRLISTX.DBF unchanged — skipping pricelist exclusion reload.");

        log($"Discovered {addonBranches.Length} ADDON branch folder(s): {string.Join(", ", addonBranches)}");
        foreach (var (branch, file) in zoneFiles)
        {
            if (Changed(file))
                result.ZoneAddOns += await ImportZoneAddOnsAsync(root, branch, log);
        }
        foreach (var (branch, file) in zone2Files)
        {
            if (Changed(file))
                result.Zone2AddOns += await ImportZone2AddOnsAsync(root, branch, log);
        }

        log($"Discovered {customerBranches.Length} CUSTOMER branch folder(s): {string.Join(", ", customerBranches)}");
        foreach (var (branch, file) in custFiles)
        {
            if (Changed(file))
                result.CustomerBranchZones += await ImportCustomerBranchZonesAsync(root, branch, log);
        }

        if (branchesChanged)
        {
            var staleZone = await db.ZoneAddOns.Where(z => !addonBranches.Contains(z.Branch)).ExecuteDeleteAsync();
            var staleZone2 = await db.Zone2AddOns.Where(z => !addonBranches.Contains(z.Branch)).ExecuteDeleteAsync();
            var staleCustomerBranchZone = await db.CustomerBranchZones.Where(z => !customerBranches.Contains(z.Branch)).ExecuteDeleteAsync();
            if (staleZone + staleZone2 + staleCustomerBranchZone > 0)
                log($"Purged stale rows for branches no longer in the active list: ZoneAddOns={staleZone}, Zone2AddOns={staleZone2}, CustomerBranchZones={staleCustomerBranchZone}");
        }

        var newMarker = new ImportMarker
        {
            AddonBranches = addonBranches.ToList(),
            CustomerBranches = customerBranches.ToList()
        };
        foreach (var file in new[] { prod4win, prchst, prodcat, prlistx2, prlistx }.Concat(zoneFiles.Select(x => x.File))
                     .Concat(zone2Files.Select(x => x.File)).Concat(custFiles.Select(x => x.File)))
            newMarker.FileMtimes[file] = File.GetLastWriteTimeUtc(file);

        SaveMarker(markerPath, newMarker);
        await db.RecordSyncAsync(SyncLogSections.PricingMasters);

        return result;
    }

    private static bool SetEquals(IEnumerable<string> a, IEnumerable<string> b) =>
        new HashSet<string>(a, StringComparer.OrdinalIgnoreCase).SetEquals(new HashSet<string>(b, StringComparer.OrdinalIgnoreCase));

    private static ImportMarker LoadMarker(string markerPath)
    {
        if (!File.Exists(markerPath)) return new ImportMarker();
        try
        {
            return JsonSerializer.Deserialize<ImportMarker>(File.ReadAllText(markerPath)) ?? new ImportMarker();
        }
        catch (JsonException)
        {
            return new ImportMarker();
        }
    }

    private static void SaveMarker(string markerPath, ImportMarker marker)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(markerPath)!);
        File.WriteAllText(markerPath, JsonSerializer.Serialize(marker));
    }

    /// <summary>
    /// Per-file last-write times (UTC) plus the branch lists seen on the
    /// previous run — a file/branch not present here, or newer than the
    /// stored time, is what actually gets re-imported.
    /// </summary>
    private class ImportMarker
    {
        public Dictionary<string, DateTime> FileMtimes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> AddonBranches { get; set; } = [];
        public List<string> CustomerBranches { get; set; } = [];
    }

    /// <summary>
    /// Updates existing Product rows (matched on ProdNo) with base price
    /// fields. Update-only: it never inserts a new Product row.
    /// SaveChangesAsync's change tracking only emits an UPDATE for a row
    /// whose pricing columns actually changed — no rework needed here for
    /// row-level sync.
    /// </summary>
    public async Task<int> ImportProductPricesAsync(string root, Action<string> log)
    {
        var file = Path.Combine(PmdmPath(root), "PROD4WIN.DBF");
        RequireFile(file);

        log($"Diffing product prices from {file} ...");

        var byProdNo = await db.Products.ToDictionaryAsync(p => p.ProdNo);

        using var reader = new DbfReader(file);
        log($"  {reader.RecordCount:N0} records in source");

        var updated = 0;
        var notFound = 0;

        foreach (var r in reader.Records())
        {
            var prodNo = r.GetInt("PRODNO");
            if (!byProdNo.TryGetValue(prodNo, out var product))
            {
                notFound++;
                continue;
            }

            var newPrice = r.GetDecimalOrNull("NEWPRICE");
            var priceFrom = r.GetDate("FROM");
            var oldPrice1 = r.GetDecimalOrNull("OLDPRICE1");
            var srp = r.GetDecimalOrNull("SRP");
            var category = r.GetString("CATEGORY");
            var barcode = r.GetString("BARCODE");
            var caseBarcode = r.GetString("CBARCODE");

            if (product.NewPrice == newPrice && product.PriceFrom == priceFrom && product.OldPrice1 == oldPrice1
                && product.Srp == srp && product.Category == category && product.Barcode == barcode
                && product.CaseBarcode == caseBarcode)
                continue;

            log($"  update ProdNo={prodNo} NewPrice={product.NewPrice}->{newPrice} PriceFrom={product.PriceFrom}->{priceFrom}");

            product.NewPrice = newPrice;
            product.PriceFrom = priceFrom;
            product.OldPrice1 = oldPrice1;
            product.Srp = srp;
            product.Category = category;
            product.Barcode = barcode;
            product.CaseBarcode = caseBarcode;
            updated++;
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        if (notFound > 0)
            log($"  {notFound:N0} prod4win rows had no matching Product (ProdNo not in Products table) — skipped");
        log($"  updated={updated:N0}");

        return updated;
    }

    /// <summary>
    /// Diffs ProductCategory (keyed by CategoryCode, a true unique key — no
    /// duplicate-row complication) against prodcat.dbf: inserts new categories,
    /// updates changed ones, deletes ones no longer in the file. Only actually
    /// changed rows are written to SQL.
    /// </summary>
    public async Task<int> ImportProductCategoriesAsync(string root, Action<string> log)
    {
        var file = Path.Combine(PmdmPath(root), "prodcat.dbf");
        RequireFile(file);

        log($"Diffing product categories from {file} ...");

        var existing = await db.ProductCategories.ToDictionaryAsync(x => x.CategoryCode);

        using var reader = new DbfReader(file);
        log($"  {reader.RecordCount:N0} records in source");

        var inserted = 0;
        var updated = 0;

        foreach (var r in reader.Records())
        {
            var categoryCode = r.GetString("CATEGORY");
            if (string.IsNullOrWhiteSpace(categoryCode)) continue;

            var groupNo = r.GetInt("GROUP");
            var groupDesc = r.GetString("GROUPDESC");
            var subCat = r.GetString("SUBCAT");
            var seqNo = r.GetInt("SEQNO");

            if (existing.Remove(categoryCode, out var row))
            {
                if (row.GroupNo != groupNo || row.GroupDesc != groupDesc || row.SubCat != subCat || row.SeqNo != seqNo)
                {
                    log($"  update CategoryCode={categoryCode} GroupNo={row.GroupNo}->{groupNo} GroupDesc={row.GroupDesc}->{groupDesc}");
                    row.GroupNo = groupNo;
                    row.GroupDesc = groupDesc;
                    row.SubCat = subCat;
                    row.SeqNo = seqNo;
                    updated++;
                }
            }
            else
            {
                log($"  insert CategoryCode={categoryCode} GroupNo={groupNo} GroupDesc={groupDesc}");
                db.ProductCategories.Add(new ProductCategory
                {
                    CategoryCode = categoryCode,
                    GroupNo = groupNo,
                    GroupDesc = groupDesc,
                    SubCat = subCat,
                    SeqNo = seqNo
                });
                inserted++;
            }
        }

        // whatever's left in `existing` is no longer in the file
        var deleted = existing.Count;
        if (deleted > 0)
        {
            foreach (var row in existing.Values)
                log($"  delete CategoryCode={row.CategoryCode}");
            db.ProductCategories.RemoveRange(existing.Values);
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        log($"  inserted={inserted:N0} updated={updated:N0} deleted={deleted:N0}");
        return inserted + updated + deleted;
    }

    /// <summary>
    /// Diffs PrlistX2Restriction (keyed by CProdNo+Zone, a true unique key —
    /// one row per allowed zone) against PRLISTX2.DBF: inserts new allow-rows,
    /// deletes ones no longer in the file. Rows never change in place since
    /// the key covers every stored column, so there is no "update" case.
    /// </summary>
    public async Task<int> ImportPrlistX2RestrictionsAsync(string root, Action<string> log)
    {
        var file = Path.Combine(PmdmPath(root), "PRLISTX2.DBF");
        RequireFile(file);

        log($"Diffing pricelist restrictions from {file} ...");

        var existing = await db.PrlistX2Restrictions.ToDictionaryAsync(x => (x.CProdNo, x.Zone));

        using var reader = new DbfReader(file);
        log($"  {reader.RecordCount:N0} records in source");

        var inserted = 0;
        // CProdNo+Zone can repeat across multiple PRLISTX2.DBF records (e.g. the
        // same CProdNo appears in more than one row, each listing overlapping
        // zones) — track what's already been queued this run so we don't try to
        // insert the same key twice and hit the unique index.
        var seenThisRun = new HashSet<(string CProdNo, string Zone)>();

        foreach (var r in reader.Records())
        {
            var cProdNo = r.GetString("CPRODNO");
            var zoneField = r.GetString("ZONE");
            if (string.IsNullOrWhiteSpace(cProdNo) || string.IsNullOrWhiteSpace(zoneField)) continue;

            // ZONE is a comma-separated list of allowed zone codes, not a single value.
            foreach (var rawZone in zoneField.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var key = (cProdNo, rawZone);
                var alreadyInDb = existing.Remove(key);
                if (!seenThisRun.Add(key) || alreadyInDb) continue;

                log($"  insert CProdNo={cProdNo} Zone={rawZone}");
                db.PrlistX2Restrictions.Add(new PrlistX2Restriction { CProdNo = cProdNo, Zone = rawZone });
                inserted++;
            }
        }

        // whatever's left in `existing` is no longer in the file
        var deleted = existing.Count;
        if (deleted > 0)
        {
            foreach (var row in existing.Values)
                log($"  delete CProdNo={row.CProdNo} Zone={row.Zone}");
            db.PrlistX2Restrictions.RemoveRange(existing.Values);
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        log($"  inserted={inserted:N0} deleted={deleted:N0}");
        return inserted + deleted;
    }

    /// <summary>
    /// Diffs PrlistXRestrictions against PRLISTX.DBF — a flat CProdNo list with
    /// no zone column, unlike PRLISTX2. Any CProdNo here is excluded from the
    /// pricelist export for every account, no exception.
    /// </summary>
    public async Task<int> ImportPrlistXRestrictionsAsync(string root, Action<string> log)
    {
        var file = Path.Combine(PmdmPath(root), "PRLISTX.DBF");
        RequireFile(file);

        log($"Diffing pricelist exclusions from {file} ...");

        var existing = await db.PrlistXRestrictions.ToDictionaryAsync(x => x.CProdNo);

        using var reader = new DbfReader(file);
        log($"  {reader.RecordCount:N0} records in source");

        var inserted = 0;
        var seenThisRun = new HashSet<string>();

        foreach (var r in reader.Records())
        {
            var cProdNo = r.GetString("CPRODNO");
            if (string.IsNullOrWhiteSpace(cProdNo)) continue;

            var alreadyInDb = existing.Remove(cProdNo);
            if (!seenThisRun.Add(cProdNo) || alreadyInDb) continue;

            log($"  insert CProdNo={cProdNo}");
            db.PrlistXRestrictions.Add(new PrlistXRestriction { CProdNo = cProdNo });
            inserted++;
        }

        // whatever's left in `existing` is no longer in the file
        var deleted = existing.Count;
        if (deleted > 0)
        {
            foreach (var row in existing.Values)
                log($"  delete CProdNo={row.CProdNo}");
            db.PrlistXRestrictions.RemoveRange(existing.Values);
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        log($"  inserted={inserted:N0} deleted={deleted:N0}");
        return inserted + deleted;
    }

    /// <summary>
    /// Diffs PriceHistory against PRCHST.DBF, keyed by RecNo — NOT
    /// ProdNo+Effective, which turns out to repeat legitimately in production
    /// data (confirmed: e.g. ProdNo 4805 has two rows both dated 9/1/2013).
    /// </summary>
    public async Task<int> ImportPriceHistoryAsync(string root, Action<string> log)
    {
        var file = Path.Combine(PmdmPath(root), "PRCHST.DBF");
        RequireFile(file);

        log($"Diffing price history from {file} ...");

        var legacyRows = await db.PriceHistories.Where(z => z.RecNo == 0).CountAsync();
        var isFullRewrite = legacyRows > 1;
        if (isFullRewrite)
        {
            log($"  First diff since RecNo backfill — reconciling {legacyRows:N0} pre-RecNo row(s), full rewrite this run (not logging individual rows)");
            await db.PriceHistories.Where(z => z.RecNo == 0).ExecuteDeleteAsync();
        }

        var existing = await db.PriceHistories.ToDictionaryAsync(x => x.RecNo);

        using var reader = new DbfReader(file);
        log($"  {reader.RecordCount:N0} records in source");

        var toInsert = new List<PriceHistory>(BatchSize);
        var inserted = 0;
        var updated = 0;

        foreach (var r in reader.Records())
        {
            var prodNo = r.GetInt("PRODNO");
            var effective = r.GetDate("EFFECTIVE");
            var npAfVat = r.GetDecimal("NPAFVAT");

            if (existing.Remove(r.RecNo, out var row))
            {
                if (row.ProdNo != prodNo || row.Effective != effective || row.NpAfVat != npAfVat)
                {
                    if (!isFullRewrite)
                        log($"  update RecNo={r.RecNo} ProdNo={row.ProdNo}->{prodNo} Effective={row.Effective}->{effective} NpAfVat={row.NpAfVat}->{npAfVat}");
                    row.ProdNo = prodNo;
                    row.Effective = effective;
                    row.NpAfVat = npAfVat;
                    updated++;
                }
            }
            else
            {
                if (!isFullRewrite)
                    log($"  insert RecNo={r.RecNo} ProdNo={prodNo} Effective={effective} NpAfVat={npAfVat}");
                toInsert.Add(new PriceHistory { RecNo = r.RecNo, ProdNo = prodNo, Effective = effective, NpAfVat = npAfVat });
                inserted++;
                await FlushInsertsIfFullAsync(toInsert, log);
            }
        }

        var deleted = existing.Count;
        if (deleted > 0)
        {
            if (!isFullRewrite)
                foreach (var row in existing.Values)
                    log($"  delete RecNo={row.RecNo} ProdNo={row.ProdNo} Effective={row.Effective}");
            db.PriceHistories.RemoveRange(existing.Values);
        }

        await FlushInsertsAsync(toInsert, log);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        log($"  inserted={inserted:N0} updated={updated:N0} deleted={deleted:N0}");
        return inserted + updated + deleted;
    }

    public async Task<int> ImportZoneAddOnsAsync(string root, string branch, Action<string> log)
    {
        var file = Path.Combine(AddonRoot(root), branch, "ZONE.DBF");
        RequireFile(file);

        log($"Diffing zone add-ons for branch '{branch}' from {file} ...");

        // Rows written before RecNo existed all share RecNo=0 — can't diff those
        // by RecNo (duplicate key), so purge them once and let every DBF row
        // insert fresh with its real RecNo. Self-healing; only happens once.
        var legacyRows = await db.ZoneAddOns.Where(z => z.Branch == branch && z.RecNo == 0).CountAsync();
        var isFullRewrite = legacyRows > 1;
        if (isFullRewrite)
        {
            log($"  First diff since RecNo backfill — reconciling {legacyRows:N0} pre-RecNo row(s), full rewrite this run (not logging individual rows)");
            await db.ZoneAddOns.Where(z => z.Branch == branch && z.RecNo == 0).ExecuteDeleteAsync();
        }

        var existing = await db.ZoneAddOns.Where(z => z.Branch == branch).ToDictionaryAsync(x => x.RecNo);

        using var reader = new DbfReader(file);
        log($"  {reader.RecordCount:N0} records in source");

        var toInsert = new List<ZoneAddOn>(BatchSize);
        var inserted = 0;
        var updated = 0;

        foreach (var r in reader.Records())
        {
            var addOn = r.GetDecimal("ADD_ON");
            var rate = r.GetDecimal("RATE");
            var fixAmt = r.GetDecimal("FIXAMT");

            if (existing.Remove(r.RecNo, out var row))
            {
                var cProdNo = r.GetString("CPRODNO");
                var cZone = r.GetString("CZONE");
                var effDate = r.GetDate("EFF_DATE");
                if (row.CProdNo != cProdNo || row.CZone != cZone || row.EffDate != effDate
                    || row.AddOn != addOn || row.Rate != rate || row.FixAmt != fixAmt)
                {
                    if (!isFullRewrite)
                        log($"  update {branch} RecNo={r.RecNo} CProdNo={cProdNo} CZone={cZone} AddOn={row.AddOn}->{addOn} Rate={row.Rate}->{rate} FixAmt={row.FixAmt}->{fixAmt}");
                    row.CProdNo = cProdNo;
                    row.CZone = cZone;
                    row.EffDate = effDate;
                    row.AddOn = addOn;
                    row.Rate = rate;
                    row.FixAmt = fixAmt;
                    updated++;
                }
            }
            else
            {
                var cProdNo = r.GetString("CPRODNO");
                var cZone = r.GetString("CZONE");
                if (!isFullRewrite)
                    log($"  insert {branch} RecNo={r.RecNo} CProdNo={cProdNo} CZone={cZone} AddOn={addOn} Rate={rate} FixAmt={fixAmt}");
                toInsert.Add(new ZoneAddOn
                {
                    Branch = branch,
                    RecNo = r.RecNo,
                    CProdNo = cProdNo,
                    CZone = cZone,
                    EffDate = r.GetDate("EFF_DATE"),
                    AddOn = addOn,
                    Rate = rate,
                    FixAmt = fixAmt
                });
                inserted++;
                await FlushInsertsIfFullAsync(toInsert, log);
            }
        }

        var deleted = existing.Count;
        if (deleted > 0)
        {
            if (!isFullRewrite)
                foreach (var row in existing.Values)
                    log($"  delete {branch} RecNo={row.RecNo} CProdNo={row.CProdNo} CZone={row.CZone}");
            db.ZoneAddOns.RemoveRange(existing.Values);
        }

        await FlushInsertsAsync(toInsert, log);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        log($"  inserted={inserted:N0} updated={updated:N0} deleted={deleted:N0}");
        return inserted + updated + deleted;
    }

    public async Task<int> ImportZone2AddOnsAsync(string root, string branch, Action<string> log)
    {
        var file = Path.Combine(AddonRoot(root), branch, "ZONE2.DBF");
        RequireFile(file);

        log($"Diffing zone2 add-ons for branch '{branch}' from {file} ...");

        var legacyRows = await db.Zone2AddOns.Where(z => z.Branch == branch && z.RecNo == 0).CountAsync();
        var isFullRewrite = legacyRows > 1;
        if (isFullRewrite)
        {
            log($"  First diff since RecNo backfill — reconciling {legacyRows:N0} pre-RecNo row(s), full rewrite this run (not logging individual rows)");
            await db.Zone2AddOns.Where(z => z.Branch == branch && z.RecNo == 0).ExecuteDeleteAsync();
        }

        var existing = await db.Zone2AddOns.Where(z => z.Branch == branch).ToDictionaryAsync(x => x.RecNo);

        using var reader = new DbfReader(file);
        log($"  {reader.RecordCount:N0} records in source");

        var toInsert = new List<Zone2AddOn>(BatchSize);
        var inserted = 0;
        var updated = 0;

        foreach (var r in reader.Records())
        {
            var addOn = r.GetDecimal("ADD_ON");
            var rate = r.GetDecimal("RATE");
            var fixAmt = r.GetDecimal("FIXAMT");

            if (existing.Remove(r.RecNo, out var row))
            {
                var custKey = r.GetString("CUSTKEY");
                var cProdNo = r.GetString("CPRODNO");
                var effDate = r.GetDate("EFF_DATE");
                if (row.CustKey != custKey || row.CProdNo != cProdNo || row.EffDate != effDate
                    || row.AddOn != addOn || row.Rate != rate || row.FixAmt != fixAmt)
                {
                    if (!isFullRewrite)
                        log($"  update {branch} RecNo={r.RecNo} CustKey={custKey} CProdNo={cProdNo} AddOn={row.AddOn}->{addOn} Rate={row.Rate}->{rate} FixAmt={row.FixAmt}->{fixAmt}");
                    row.CustKey = custKey;
                    row.CProdNo = cProdNo;
                    row.EffDate = effDate;
                    row.AddOn = addOn;
                    row.Rate = rate;
                    row.FixAmt = fixAmt;
                    updated++;
                }
            }
            else
            {
                var custKey = r.GetString("CUSTKEY");
                var cProdNo = r.GetString("CPRODNO");
                if (!isFullRewrite)
                    log($"  insert {branch} RecNo={r.RecNo} CustKey={custKey} CProdNo={cProdNo} AddOn={addOn} Rate={rate} FixAmt={fixAmt}");
                toInsert.Add(new Zone2AddOn
                {
                    Branch = branch,
                    RecNo = r.RecNo,
                    CustKey = custKey,
                    CProdNo = cProdNo,
                    EffDate = r.GetDate("EFF_DATE"),
                    AddOn = addOn,
                    Rate = rate,
                    FixAmt = fixAmt
                });
                inserted++;
                await FlushInsertsIfFullAsync(toInsert, log);
            }
        }

        var deleted = existing.Count;
        if (deleted > 0)
        {
            if (!isFullRewrite)
                foreach (var row in existing.Values)
                    log($"  delete {branch} RecNo={row.RecNo} CustKey={row.CustKey} CProdNo={row.CProdNo}");
            db.Zone2AddOns.RemoveRange(existing.Values);
        }

        await FlushInsertsAsync(toInsert, log);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        log($"  inserted={inserted:N0} updated={updated:N0} deleted={deleted:N0}");
        return inserted + updated + deleted;
    }

    public async Task<int> ImportCustomerBranchZonesAsync(string root, string branch, Action<string> log)
    {
        var file = Path.Combine(CustomerRoot(root), branch, "CUST4WIN.DBF");
        RequireFile(file);

        log($"Diffing customer zones for branch '{branch}' from {file} ...");

        var legacyRows = await db.CustomerBranchZones.Where(z => z.Branch == branch && z.RecNo == 0).CountAsync();
        var isFullRewrite = legacyRows > 1;
        if (isFullRewrite)
        {
            log($"  First diff since RecNo backfill — reconciling {legacyRows:N0} pre-RecNo row(s), full rewrite this run (not logging individual rows)");
            await db.CustomerBranchZones.Where(z => z.Branch == branch && z.RecNo == 0).ExecuteDeleteAsync();
        }

        var existing = await db.CustomerBranchZones.Where(z => z.Branch == branch).ToDictionaryAsync(x => x.RecNo);

        using var reader = new DbfReader(file);
        log($"  {reader.RecordCount:N0} records in source");

        var toInsert = new List<CustomerBranchZone>(BatchSize);
        var inserted = 0;
        var updated = 0;

        foreach (var r in reader.Records())
        {
            var custKey = r.GetString("CUSTKEY");
            if (string.IsNullOrWhiteSpace(custKey))
            {
                existing.Remove(r.RecNo);
                continue;
            }

            var cZone = r.GetString("CZONE");

            if (existing.Remove(r.RecNo, out var row))
            {
                if (row.CustKey != custKey || row.CZone != cZone)
                {
                    if (!isFullRewrite)
                        log($"  update {branch} RecNo={r.RecNo} CustKey={custKey} CZone={row.CZone}->{cZone}");
                    row.CustKey = custKey;
                    row.CZone = cZone;
                    updated++;
                }
            }
            else
            {
                if (!isFullRewrite)
                    log($"  insert {branch} RecNo={r.RecNo} CustKey={custKey} CZone={cZone}");
                toInsert.Add(new CustomerBranchZone { Branch = branch, RecNo = r.RecNo, CustKey = custKey, CZone = cZone });
                inserted++;
                await FlushInsertsIfFullAsync(toInsert, log);
            }
        }

        var deleted = existing.Count;
        if (deleted > 0)
        {
            if (!isFullRewrite)
                foreach (var row in existing.Values)
                    log($"  delete {branch} RecNo={row.RecNo} CustKey={row.CustKey} CZone={row.CZone}");
            db.CustomerBranchZones.RemoveRange(existing.Values);
        }

        await FlushInsertsAsync(toInsert, log);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        log($"  inserted={inserted:N0} updated={updated:N0} deleted={deleted:N0}");
        return inserted + updated + deleted;
    }

    /// <summary>Batches the insert side of a diff so a branch with many genuinely
    /// new rows doesn't build one giant pending changeset in memory.</summary>
    private async Task FlushInsertsIfFullAsync<T>(List<T> toInsert, Action<string> log) where T : class
    {
        if (toInsert.Count < BatchSize) return;
        await FlushInsertsAsync(toInsert, log);
    }

    private async Task FlushInsertsAsync<T>(List<T> toInsert, Action<string> log) where T : class
    {
        if (toInsert.Count == 0) return;
        db.Set<T>().AddRange(toInsert);
        await db.SaveChangesAsync();
        toInsert.Clear();
    }

    /// <summary>
    /// Not every folder on F:\ is a complete branch — some are placeholder/
    /// incomplete (e.g. missing ZONE.DBF). Skip those with a visible log line
    /// rather than failing the whole run; nothing silently vanishes.
    /// </summary>
    private static void LogSkippedFolders(Action<string> log, string kind, string[] all, string[] usable)
    {
        var skipped = all.Except(usable).ToArray();
        if (skipped.Length > 0)
            log($"{kind}: skipping {skipped.Length} folder(s) not in the active branch list or missing expected file(s): {string.Join(", ", skipped)}");
    }

    private static void RequireFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Production pricing DBF not found: {path}. Expected HO's own " +
                $"F:\\PMDM / F:\\AUTOPROG\\ADDON\\{{branch}} / F:\\AUTOPROG\\CUSTOMER\\{{branch}} layout.", path);
    }
}

public class PricingImportResult
{
    public bool Skipped { get; set; }
    public int ProductsPriced { get; set; }
    public int PriceHistoryRows { get; set; }
    public int ProductCategories { get; set; }
    public int PrlistX2Restrictions { get; set; }
    public int PrlistXRestrictions { get; set; }
    public int ZoneAddOns { get; set; }
    public int Zone2AddOns { get; set; }
    public int CustomerBranchZones { get; set; }

    public override string ToString() =>
        Skipped
            ? "Skipped (no changes since last sync)"
            : $"ProductsPriced={ProductsPriced:N0}  PriceHistory={PriceHistoryRows:N0}  " +
              $"ProductCategories={ProductCategories:N0}  PrlistX2Restrictions={PrlistX2Restrictions:N0}  " +
              $"PrlistXRestrictions={PrlistXRestrictions:N0}  " +
              $"ZoneAddOns={ZoneAddOns:N0}  Zone2AddOns={Zone2AddOns:N0}  CustomerBranchZones={CustomerBranchZones:N0}";
}
