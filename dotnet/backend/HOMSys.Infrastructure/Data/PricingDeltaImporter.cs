using HOMSys.Application.DTOs.Pricing;
using HOMSys.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Data;

/// <summary>
/// Applies a pricing delta sent by LegacyMasterWatcher.exe, which now reads
/// and parses F:\ itself (Azure can't reach it) and diffs against its own
/// local snapshot — this class only ever sees rows that actually changed,
/// keyed the same way as PricingDataImporter.cs's row-level diff, so it
/// queries just those keys instead of loading whole tables.
///
/// Sibling to, not a replacement of, PricingDataImporter.cs — that class
/// still backs the manual `import-HoMaster-data` CLI command and its own
/// full-table diff.
/// </summary>
public class PricingDeltaImporter(AppDbContext db)
{
    private static readonly SemaphoreSlim SyncLock = new(1, 1);

    public async Task<PricingImportResult> ApplyAsync(PricingSyncDeltaRequest request, Action<string>? log = null)
    {
        log ??= _ => { };

        if (!await SyncLock.WaitAsync(0))
        {
            log("Another sync already in progress — skipping.");
            return new PricingImportResult { Skipped = true };
        }
        try
        {
            return await ApplyCoreAsync(request, log);
        }
        finally
        {
            SyncLock.Release();
        }
    }

    private async Task<PricingImportResult> ApplyCoreAsync(PricingSyncDeltaRequest request, Action<string> log)
    {
        var result = new PricingImportResult();

        if (request.ProductPrices is { Count: > 0 } productPrices)
            result.ProductsPriced = await ApplyProductPricesAsync(productPrices, log);

        if (request.ProductCategories is { } categories)
            result.ProductCategories = await ApplyProductCategoriesAsync(categories, log);

        if (request.PrlistX2Restrictions is { } prlistX2)
            result.PrlistX2Restrictions = await ApplyPrlistX2RestrictionsAsync(prlistX2, log);

        if (request.PrlistXRestrictions is { } prlistX)
            result.PrlistXRestrictions = await ApplyPrlistXRestrictionsAsync(prlistX, log);

        if (request.PriceHistory is { } priceHistory)
            result.PriceHistoryRows = await ApplyPriceHistoryAsync(priceHistory, log);

        if (request.Branches is { Count: > 0 } branches)
        {
            foreach (var (branch, delta) in branches)
            {
                if (!PricingDataImporter.ActiveBranches.Contains(branch))
                {
                    log($"Branch '{branch}' not in active branch list — skipping.");
                    continue;
                }

                if (delta.ZoneAddOns is { } zone)
                    result.ZoneAddOns += await ApplyZoneAddOnsAsync(branch, zone, log);

                if (delta.Zone2AddOns is { } zone2)
                    result.Zone2AddOns += await ApplyZone2AddOnsAsync(branch, zone2, log);

                if (delta.CustomerBranchZones is { } cust)
                    result.CustomerBranchZones += await ApplyCustomerBranchZonesAsync(branch, cust, log);
            }
        }

        if (result is { Skipped: false, ProductsPriced: 0, ProductCategories: 0, PrlistX2Restrictions: 0,
                PrlistXRestrictions: 0, PriceHistoryRows: 0, ZoneAddOns: 0, Zone2AddOns: 0, CustomerBranchZones: 0 })
            log("Empty delta — nothing to apply.");

        // Even an empty delta means the watcher successfully read F:\ and checked in —
        // that's what "last synced" should reflect, not just whether rows changed.
        await db.RecordSyncAsync(SyncLogSections.PricingMasters);

        return result;
    }

    private async Task<int> ApplyProductPricesAsync(List<ProductPriceDelta> deltas, Action<string> log)
    {
        var prodNos = deltas.Select(d => d.ProdNo).ToHashSet();
        var existing = await db.Products.Where(p => prodNos.Contains(p.ProdNo)).ToDictionaryAsync(p => p.ProdNo);

        var updated = 0;
        foreach (var d in deltas)
        {
            if (!existing.TryGetValue(d.ProdNo, out var product))
            {
                log($" ProdNo={d.ProdNo} not found in Products — skipped");
                continue;
            }

            product.NewPrice = d.NewPrice;
            product.PriceFrom = d.PriceFrom;
            product.OldPrice1 = d.OldPrice1;
            product.Srp = d.Srp;
            product.Category = d.Category;
            product.Barcode = d.Barcode;
            product.CaseBarcode = d.CaseBarcode;
            updated++;
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        log($" ProductPrices: updated={updated:N0}");
        return updated;
    }

    private async Task<int> ApplyProductCategoriesAsync(ProductCategoryDeltaSection section, Action<string> log)
    {
        var codes = section.Upserts.Select(u => u.CategoryCode).Concat(section.Deletes).ToHashSet();
        var existing = await db.ProductCategories.Where(x => codes.Contains(x.CategoryCode)).ToDictionaryAsync(x => x.CategoryCode);

        var inserted = 0;
        var updated = 0;
        foreach (var u in section.Upserts)
        {
            if (existing.Remove(u.CategoryCode, out var row))
            {
                row.GroupNo = u.GroupNo;
                row.GroupDesc = u.GroupDesc;
                row.SubCat = u.SubCat;
                row.SeqNo = u.SeqNo;
                updated++;
            }
            else
            {
                db.ProductCategories.Add(new ProductCategory
                {
                    CategoryCode = u.CategoryCode,
                    GroupNo = u.GroupNo,
                    GroupDesc = u.GroupDesc,
                    SubCat = u.SubCat,
                    SeqNo = u.SeqNo,
                });
                inserted++;
            }
        }

        var deleted = 0;
        foreach (var code in section.Deletes)
        {
            if (existing.Remove(code, out var row))
            {
                db.ProductCategories.Remove(row);
                deleted++;
            }
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        log($" ProductCategory: inserted={inserted:N0} updated={updated:N0} deleted={deleted:N0}");
        return inserted + updated + deleted;
    }

    private async Task<int> ApplyPrlistX2RestrictionsAsync(PrlistX2RestrictionDeltaSection section, Action<string> log)
    {
        var cProdNos = section.Adds.Select(a => a.CProdNo).Concat(section.Removes.Select(r => r.CProdNo)).ToHashSet();
        var existing = await db.PrlistX2Restrictions.Where(x => cProdNos.Contains(x.CProdNo))
            .ToDictionaryAsync(x => (x.CProdNo, x.Zone));

        var added = 0;
        foreach (var a in section.Adds)
        {
            if (existing.ContainsKey((a.CProdNo, a.Zone))) continue;
            db.PrlistX2Restrictions.Add(new PrlistX2Restriction { CProdNo = a.CProdNo, Zone = a.Zone });
            added++;
        }

        var removed = 0;
        foreach (var r in section.Removes)
        {
            if (existing.Remove((r.CProdNo, r.Zone), out var row))
            {
                db.PrlistX2Restrictions.Remove(row);
                removed++;
            }
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        log($" PrlistX2Restriction: added={added:N0} removed={removed:N0}");
        return added + removed;
    }

    private async Task<int> ApplyPrlistXRestrictionsAsync(PrlistXRestrictionDeltaSection section, Action<string> log)
    {
        var cProdNos = section.Adds.Concat(section.Removes).ToHashSet();
        var existing = await db.PrlistXRestrictions.Where(x => cProdNos.Contains(x.CProdNo)).ToDictionaryAsync(x => x.CProdNo);

        var added = 0;
        foreach (var cProdNo in section.Adds)
        {
            if (existing.ContainsKey(cProdNo)) continue;
            db.PrlistXRestrictions.Add(new PrlistXRestriction { CProdNo = cProdNo });
            added++;
        }

        var removed = 0;
        foreach (var cProdNo in section.Removes)
        {
            if (existing.Remove(cProdNo, out var row))
            {
                db.PrlistXRestrictions.Remove(row);
                removed++;
            }
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        log($" PrlistXRestriction: added={added:N0} removed={removed:N0}");
        return added + removed;
    }

    private async Task<int> ApplyPriceHistoryAsync(PriceHistoryDeltaSection section, Action<string> log)
    {
        var recNos = section.Upserts.Select(u => u.RecNo).Concat(section.Deletes).ToHashSet();
        var existing = await db.PriceHistories.Where(x => recNos.Contains(x.RecNo)).ToDictionaryAsync(x => x.RecNo);

        var inserted = 0;
        var updated = 0;
        foreach (var u in section.Upserts)
        {
            if (existing.Remove(u.RecNo, out var row))
            {
                row.ProdNo = u.ProdNo;
                row.Effective = u.Effective;
                row.NpAfVat = u.NpAfVat;
                updated++;
            }
            else
            {
                db.PriceHistories.Add(new PriceHistory { RecNo = u.RecNo, ProdNo = u.ProdNo, Effective = u.Effective, NpAfVat = u.NpAfVat });
                inserted++;
            }
        }

        var deleted = 0;
        foreach (var recNo in section.Deletes)
        {
            if (existing.Remove(recNo, out var row))
            {
                db.PriceHistories.Remove(row);
                deleted++;
            }
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        log($" PriceHistory: inserted={inserted:N0} updated={updated:N0} deleted={deleted:N0}");
        return inserted + updated + deleted;
    }

    private async Task<int> ApplyZoneAddOnsAsync(string branch, ZoneAddOnDeltaSection section, Action<string> log)
    {
        var recNos = section.Upserts.Select(u => u.RecNo).Concat(section.Deletes).ToHashSet();
        var existing = await db.ZoneAddOns.Where(x => x.Branch == branch && recNos.Contains(x.RecNo)).ToDictionaryAsync(x => x.RecNo);

        var inserted = 0;
        var updated = 0;
        foreach (var u in section.Upserts)
        {
            if (existing.Remove(u.RecNo, out var row))
            {
                row.CProdNo = u.CProdNo;
                row.CZone = u.CZone;
                row.EffDate = u.EffDate;
                row.AddOn = u.AddOn;
                row.Rate = u.Rate;
                row.FixAmt = u.FixAmt;
                updated++;
            }
            else
            {
                db.ZoneAddOns.Add(new ZoneAddOn
                {
                    Branch = branch,
                    RecNo = u.RecNo,
                    CProdNo = u.CProdNo,
                    CZone = u.CZone,
                    EffDate = u.EffDate,
                    AddOn = u.AddOn,
                    Rate = u.Rate,
                    FixAmt = u.FixAmt,
                });
                inserted++;
            }
        }

        var deleted = 0;
        foreach (var recNo in section.Deletes)
        {
            if (existing.Remove(recNo, out var row))
            {
                db.ZoneAddOns.Remove(row);
                deleted++;
            }
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        log($" {branch} ZoneAddOn: inserted={inserted:N0} updated={updated:N0} deleted={deleted:N0}");
        return inserted + updated + deleted;
    }

    private async Task<int> ApplyZone2AddOnsAsync(string branch, Zone2AddOnDeltaSection section, Action<string> log)
    {
        var recNos = section.Upserts.Select(u => u.RecNo).Concat(section.Deletes).ToHashSet();
        var existing = await db.Zone2AddOns.Where(x => x.Branch == branch && recNos.Contains(x.RecNo)).ToDictionaryAsync(x => x.RecNo);

        var inserted = 0;
        var updated = 0;
        foreach (var u in section.Upserts)
        {
            if (existing.Remove(u.RecNo, out var row))
            {
                row.CustKey = u.CustKey;
                row.CProdNo = u.CProdNo;
                row.EffDate = u.EffDate;
                row.AddOn = u.AddOn;
                row.Rate = u.Rate;
                row.FixAmt = u.FixAmt;
                updated++;
            }
            else
            {
                db.Zone2AddOns.Add(new Zone2AddOn
                {
                    Branch = branch,
                    RecNo = u.RecNo,
                    CustKey = u.CustKey,
                    CProdNo = u.CProdNo,
                    EffDate = u.EffDate,
                    AddOn = u.AddOn,
                    Rate = u.Rate,
                    FixAmt = u.FixAmt,
                });
                inserted++;
            }
        }

        var deleted = 0;
        foreach (var recNo in section.Deletes)
        {
            if (existing.Remove(recNo, out var row))
            {
                db.Zone2AddOns.Remove(row);
                deleted++;
            }
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        log($" {branch} Zone2AddOn: inserted={inserted:N0} updated={updated:N0} deleted={deleted:N0}");
        return inserted + updated + deleted;
    }

    private async Task<int> ApplyCustomerBranchZonesAsync(string branch, CustomerBranchZoneDeltaSection section, Action<string> log)
    {
        var recNos = section.Upserts.Select(u => u.RecNo).Concat(section.Deletes).ToHashSet();
        var existing = await db.CustomerBranchZones.Where(x => x.Branch == branch && recNos.Contains(x.RecNo)).ToDictionaryAsync(x => x.RecNo);

        var inserted = 0;
        var updated = 0;
        foreach (var u in section.Upserts)
        {
            if (existing.Remove(u.RecNo, out var row))
            {
                row.CustKey = u.CustKey;
                row.CZone = u.CZone;
                updated++;
            }
            else
            {
                db.CustomerBranchZones.Add(new CustomerBranchZone
                {
                    Branch = branch,
                    RecNo = u.RecNo,
                    CustKey = u.CustKey,
                    CZone = u.CZone,
                });
                inserted++;
            }
        }

        var deleted = 0;
        foreach (var recNo in section.Deletes)
        {
            if (existing.Remove(recNo, out var row))
            {
                db.CustomerBranchZones.Remove(row);
                deleted++;
            }
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        log($" {branch} CustomerBranchZone: inserted={inserted:N0} updated={updated:N0} deleted={deleted:N0}");
        return inserted + updated + deleted;
    }
}
