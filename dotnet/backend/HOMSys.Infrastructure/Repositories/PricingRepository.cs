using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class PricingRepository(AppDbContext db) : IPricingRepository
{
    public async Task<decimal?> GetLatestPriceHistoryNpAfVatAsync(int prodNo, DateOnly asOf) =>
        await db.PriceHistories.AsNoTracking()
            .Where(h => h.ProdNo == prodNo && h.Effective != null && h.Effective <= asOf)
            .OrderByDescending(h => h.Effective)
            .Select(h => (decimal?)h.NpAfVat)
            .FirstOrDefaultAsync();

    public async Task<decimal> GetZoneAddOnAsync(string branch, string cProdNo, string cZone, DateOnly asOf) =>
        await db.ZoneAddOns.AsNoTracking()
            .Where(z => z.Branch == branch && z.CProdNo == cProdNo && z.CZone == cZone
                        && (z.EffDate == null || z.EffDate <= asOf))
            .OrderByDescending(z => z.EffDate)
            .Select(z => (decimal?)z.AddOn)
            .FirstOrDefaultAsync() ?? 0m;

    public async Task<decimal> GetZone2AddOnAsync(string branch, string cProdNo, string custKey, string cZone, DateOnly asOf) =>
        await db.Zone2AddOns.AsNoTracking()
            .Where(z => z.Branch == branch && z.CProdNo == cProdNo
                        && (z.CustKey == custKey || z.CustKey == cZone)
                        && (z.EffDate == null || z.EffDate <= asOf))
            .OrderByDescending(z => z.EffDate)
            .Select(z => (decimal?)z.AddOn)
            .FirstOrDefaultAsync() ?? 0m;

    public async Task<(string Branch, string CZone)?> GetCustomerBranchZoneAsync(string custKey)
    {
        var row = await db.Customers.AsNoTracking()
            .Where(z => z.CustKey == custKey)
            .OrderByDescending(z => z.ImportedAt)
            .Select(z => new { z.Branch, z.CZone })
            .FirstOrDefaultAsync();
        return row is null ? null : (row.Branch, row.CZone);
    }

    public async Task<List<(string CustKey, string Branch, string CZone)>> GetCustomerBranchZonesAsync(IReadOnlyCollection<string> custKeys)
    {
        var keys = custKeys.ToList();
        var rows = await db.Customers.AsNoTracking()
            .Where(c => keys.Contains(c.CustKey))
            .Select(c => new { c.CustKey, c.Branch, c.CZone, c.ImportedAt })
            .ToListAsync();
        return rows
            .GroupBy(r => r.CustKey.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(r => r.ImportedAt).First())
            .Select(r => (r.CustKey.Trim(), r.Branch.Trim(), r.CZone.Trim()))
            .ToList();
    }

    public async Task<List<ZoneAddOn>> GetZoneAddOnRowsAsync(IReadOnlyCollection<string> cProdNos,
        IReadOnlyCollection<string> branches, IReadOnlyCollection<string> zones, DateOnly asOf)
    {
        var (p, b, z) = (cProdNos.ToList(), branches.ToList(), zones.ToList());
        return await db.ZoneAddOns.AsNoTracking()
            .Where(x => p.Contains(x.CProdNo) && b.Contains(x.Branch) && z.Contains(x.CZone)
                        && (x.EffDate == null || x.EffDate <= asOf))
            .ToListAsync();
    }

    public async Task<List<Zone2AddOn>> GetZone2AddOnRowsAsync(IReadOnlyCollection<string> cProdNos,
        IReadOnlyCollection<string> branches, IReadOnlyCollection<string> keys, DateOnly asOf)
    {
        var (p, b, k) = (cProdNos.ToList(), branches.ToList(), keys.ToList());
        return await db.Zone2AddOns.AsNoTracking()
            .Where(x => p.Contains(x.CProdNo) && b.Contains(x.Branch) && k.Contains(x.CustKey)
                        && (x.EffDate == null || x.EffDate <= asOf))
            .ToListAsync();
    }

    public async Task<Dictionary<string, decimal>> GetZoneAddOnsAsync(string branch, string cZone, DateOnly asOf)
    {
        var rows = await db.ZoneAddOns.AsNoTracking()
            .Where(z => z.Branch == branch && z.CZone == cZone && (z.EffDate == null || z.EffDate <= asOf))
            .ToListAsync();

        return rows
            .GroupBy(z => z.CProdNo)
            .Select(g => g.OrderByDescending(z => z.EffDate).First())
            .Where(z => z.AddOn != 0)
            .ToDictionary(z => z.CProdNo, z => z.AddOn);
    }

    public async Task<Dictionary<string, decimal>> GetZone2AddOnsAsync(string branch, string custKey, string cZone, DateOnly asOf)
    {
        var rows = await db.Zone2AddOns.AsNoTracking()
            .Where(z => z.Branch == branch
                        && (z.CustKey == custKey || z.CustKey == cZone)
                        && (z.EffDate == null || z.EffDate <= asOf))
            .ToListAsync();

        return rows
            .GroupBy(z => z.CProdNo)
            .Select(g => g.OrderByDescending(z => z.EffDate).First())
            .Where(z => z.AddOn != 0)
            .ToDictionary(z => z.CProdNo, z => z.AddOn);
    }

    public async Task<Dictionary<string, HashSet<string>>> GetPrlistX2RestrictedZonesAsync()
    {
        var rows = await db.PrlistX2Restrictions.AsNoTracking().ToListAsync();
        return rows
            .GroupBy(r => r.CProdNo)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Zone).ToHashSet());
    }

    public async Task<HashSet<string>> GetPrlistXRestrictedProdNosAsync() =>
        (await db.PrlistXRestrictions.AsNoTracking().Select(x => x.CProdNo).ToListAsync()).ToHashSet();

    public async Task<List<string>> GetBranchesWithZonesAsync() =>
        await db.ZoneAddOns.AsNoTracking()
            .Select(z => z.Branch)
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync();

    public async Task<List<string>> GetZonesForBranchAsync(string branch) =>
        await db.ZoneAddOns.AsNoTracking()
            .Where(z => z.Branch == branch)
            .Select(z => z.CZone)
            .Distinct()
            .OrderBy(z => z)
            .ToListAsync();

    public async Task<List<string>> GetZonesForBranchWhseNosAsync(string pricingFolder, IReadOnlyCollection<int> whseNos)
    {
        var custZones = await db.Customers.AsNoTracking()
            .Where(z => z.Branch == pricingFolder && whseNos.Contains(z.WhseNo))
            .Select(z => z.CZone)
            .Distinct()
            .ToListAsync();

        return await db.ZoneAddOns.AsNoTracking()
            .Where(z => z.Branch == pricingFolder && custZones.Contains(z.CZone))
            .Select(z => z.CZone)
            .Distinct()
            .OrderBy(z => z)
            .ToListAsync();
    }

    public async Task<Dictionary<string, string>> GetZoneDescriptionsAsync(string branch) =>
        await db.ZoneMasts.AsNoTracking()
            .Where(z => z.Branch == branch)
            .GroupBy(z => z.CZone)
            .Select(g => g.First())
            .ToDictionaryAsync(z => z.CZone, z => z.CDesc);
}
