using System.Text.Json;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data.Dbf;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Data;

/// <summary>
/// One-off import of BMS reference data, read live from
/// \\Acastillano\setup\ADC\BMSRAM — BMS itself runs against this share, so
/// it is a live source, not a point-in-time copy. (The local
/// C:\CLAUDE\HOMSYS-MAIN\legacy\dbf snapshot, copied 2026-08-18, is kept only
/// as an offline fallback/reference — see legacy/README.md.)
///
/// Sources: cust4win (customer master), prod4win (products).
/// Note CUSTDIR is deliberately NOT imported — it is only the lookup picker in
/// the legacy form; cust4win is the real master. See legacy/vfp/ANALYSIS.md.
///
/// PO log and document class are NOT imported here: DocClass is a hardcoded
/// static lookup (see DocClassRepository — only 4 rows exist, effectively
/// never change), and PoLog is HOMSys-owned going forward — HOMSys writes its
/// own PoLog rows on order create/update (SalesOrderService, mirroring legacy
/// checkponum2) and the bridge writes POFILES back to BMS; there is no
/// separate read-sync of POFILES into HOMSys.
///
/// Run with:  dotnet run --project HOMSys.API -- import-reference-data [path]
/// </summary>
public class ReferenceDataImporter(AppDbContext db)
{
    public const string DefaultDbfPath = @"\\Acastillano\setup\ADC\BMSRAM";

    private const int BatchSize = 5000;

    public static string MarkerPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "HOMSys", ".last-reference-import.json");

    public async Task<ImportResult> ImportAllAsync(string dbfPath, Action<string>? log = null)
    {
        log ??= _ => { };

        var result = new ImportResult
        {
            Customers = await ImportCustomersAsync(dbfPath, log),
            Products = await ImportProductsAsync(dbfPath, log)
        };

        SaveMarker(result);

        return result;
    }

    private static void SaveMarker(ImportResult result)
    {
        var marker = new ReferenceImportMarker
        {
            LastRunUtc = DateTime.UtcNow,
            Customers = result.Customers,
            Products = result.Products
        };

        var path = MarkerPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(marker));
    }

    public static ReferenceImportMarker? LoadMarker()
    {
        var path = MarkerPath;
        if (!File.Exists(path)) return null;

        try
        {
            return JsonSerializer.Deserialize<ReferenceImportMarker>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<int> ImportCustomersAsync(string dbfPath, Action<string> log)
    {
        var file = Path.Combine(dbfPath, "cust4win.DBF");
        RequireFile(file);

        log($"Importing customers from {file} ...");
        await db.Customers.ExecuteDeleteAsync();

        using var reader = new DbfReader(file);
        log($"  {reader.RecordCount:N0} records in source");

        // cust4win has a small number of duplicate CUSTKEY rows (stale/superseded
        // records, e.g. a BlockInv flag later flipped) — keep the last-occurring
        // row per key, matching file scan order.
        var byKey = new Dictionary<string, Customer>();
        var nonEmptyKeyRows = 0;

        foreach (var r in reader.Records())
        {
            var custKey = r.GetString("CUSTKEY");
            if (string.IsNullOrWhiteSpace(custKey)) continue;
            nonEmptyKeyRows++;

            byKey[custKey] = new Customer
            {
                CustKey = custKey,
                CKey = r.GetString("CKEY"),
                CusName = r.GetString("CUSNAME"),
                AddrLn1 = r.GetString("ADDRLN1"),
                AddrLn2 = r.GetString("ADDRLN2"),
                DelAddrLn1 = r.GetString("DELADDRLN1"),
                DelAddrLn2 = r.GetString("DELADDRLN2"),
                DelArea = r.GetString("DELAREA"),
                WhseNo = r.GetInt("WHSENO"),
                CustWhse = r.GetInt("CUSTWHSE"),
                ServeWh = r.GetInt("SERVEWH"),
                DelWhse = r.GetInt("DELWHSE"),
                Salesman = r.GetInt("SALESMAN"),
                CsMan = r.GetString("CSMAN"),
                Term = r.GetInt("TERM"),
                TermDays = r.GetInt("TERMDAYS"),
                CZone = r.GetString("CZONE"),
                VatId = r.GetString("VATID"),
                Subd = r.GetString("SUBD"),
                Tpc = r.GetBool("TPC"),
                Offshore = r.GetBool("OFFSHORE"),
                ExBranch = r.GetBool("EX_BRANCH"),
                CCode = r.GetInt("CCODE"),
                OldCCode = r.GetInt("OLDCCODE"),
                IEffDate = r.GetDate("IEFFDATE"),
                BlockInv = r.GetBool("BLOCKINV"),
                Tin = r.GetString("TIN"),
                AliasKey = r.GetString("ALIASKEY"),
                ConsoMax2 = r.GetString("CONSOMAX2")
            };
        }

        if (byKey.Count != nonEmptyKeyRows)
            log($"  {nonEmptyKeyRows - byKey.Count:N0} duplicate CUSTKEY rows collapsed (last row per key kept)");

        var batch = new List<Customer>(BatchSize);
        var total = 0;

        foreach (var customer in byKey.Values)
        {
            batch.Add(customer);
            total = await FlushIfFullAsync(batch, total, log);
        }

        return await FlushAsync(batch, total, log);
    }

    /// <summary>
    /// Diffs Products by ProdNo instead of truncate+reload. Category/Barcode/
    /// CaseBarcode/NewPrice/PriceFrom/OldPrice1/Srp are owned by
    /// PricingDataImporter (F:\PMDM\PROD4WIN.DBF) — a truncate here used to
    /// wipe them on every reference sync, since that importer only re-fills
    /// them when its own file-mtime gate sees a change. Never touch those
    /// fields in this method.
    /// </summary>
    public async Task<int> ImportProductsAsync(string dbfPath, Action<string> log)
    {
        var file = Path.Combine(dbfPath, "PROD4WIN.DBF");
        RequireFile(file);

        log($"Diffing products from {file} ...");

        var existing = await db.Products.ToDictionaryAsync(p => p.ProdNo);

        using var reader = new DbfReader(file);
        log($"  {reader.RecordCount:N0} records in source");

        var seen = new HashSet<int>();
        var inserted = 0;
        var updated = 0;

        foreach (var r in reader.Records())
        {
            var cProdNo = r.GetString("CPRODNO");
            if (string.IsNullOrWhiteSpace(cProdNo)) continue;

            var prodNo = r.GetInt("PRODNO");
            seen.Add(prodNo);

            if (existing.TryGetValue(prodNo, out var product))
            {
                product.CProdNo = cProdNo;
                product.ProdDesc = r.GetString("PRODDESC");
                product.PackSize = r.GetString("PACKSIZE");
                product.Pieces = r.GetInt("PIECES");
                product.QtyPerPc = r.GetInt("QTYPERPC");
                product.InnerQty = r.GetInt("INNERQTY");
                product.Um = r.GetString("UM");
                product.Supplier = r.GetInt("SUPPLIER");
                product.PriceList = r.GetBool("PRICELIST");
                product.TaxRate = r.GetDecimal("TAXRATE");
                product.Brand = r.GetString("BRAND");
                product.SBrand = r.GetString("SBRAND");
                product.PhOut = r.GetBool("PHOUT");
                updated++;
            }
            else
            {
                db.Products.Add(new Product
                {
                    CProdNo = cProdNo,
                    ProdNo = prodNo,
                    ProdDesc = r.GetString("PRODDESC"),
                    PackSize = r.GetString("PACKSIZE"),
                    Pieces = r.GetInt("PIECES"),
                    QtyPerPc = r.GetInt("QTYPERPC"),
                    InnerQty = r.GetInt("INNERQTY"),
                    Um = r.GetString("UM"),
                    Supplier = r.GetInt("SUPPLIER"),
                    PriceList = r.GetBool("PRICELIST"),
                    TaxRate = r.GetDecimal("TAXRATE"),
                    Brand = r.GetString("BRAND"),
                    SBrand = r.GetString("SBRAND"),
                    PhOut = r.GetBool("PHOUT")
                });
                inserted++;
            }
        }

        var removed = existing.Values.Where(p => !seen.Contains(p.ProdNo)).ToList();
        if (removed.Count > 0)
            db.Products.RemoveRange(removed);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        log($"  inserted={inserted:N0} updated={updated:N0} removed={removed.Count:N0}");
        return inserted + updated;
    }

    private async Task<int> FlushIfFullAsync<T>(List<T> batch, int total, Action<string> log)
        where T : class
    {
        if (batch.Count < BatchSize) return total;
        return await FlushAsync(batch, total, log);
    }

    private async Task<int> FlushAsync<T>(List<T> batch, int total, Action<string> log)
        where T : class
    {
        if (batch.Count == 0) return total;

        db.ChangeTracker.AutoDetectChangesEnabled = false;
        db.Set<T>().AddRange(batch);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        db.ChangeTracker.AutoDetectChangesEnabled = true;

        total += batch.Count;
        batch.Clear();
        log($"  {total:N0} rows imported");
        return total;
    }

    private static void RequireFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Staged DBF not found: {path}. Expected the legacy snapshots under " +
                $"{DefaultDbfPath}.", path);
    }
}

public class ImportResult
{
    public int Customers { get; set; }
    public int Products { get; set; }

    public override string ToString() =>
        $"Customers={Customers:N0}  Products={Products:N0}";
}

public class ReferenceImportMarker
{
    public DateTime LastRunUtc { get; set; }
    public int Customers { get; set; }
    public int Products { get; set; }
}
