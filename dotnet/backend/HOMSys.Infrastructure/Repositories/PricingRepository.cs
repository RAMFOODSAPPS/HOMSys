using HOMSys.Application.Interfaces;
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
        var row = await db.CustomerBranchZones.AsNoTracking()
            .Where(z => z.CustKey == custKey)
            .Select(z => new { z.Branch, z.CZone })
            .FirstOrDefaultAsync();
        return row is null ? null : (row.Branch, row.CZone);
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
}
