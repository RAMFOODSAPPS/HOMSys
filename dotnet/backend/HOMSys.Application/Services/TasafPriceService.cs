using HOMSys.Application.DTOs.Tasaf;
using HOMSys.Application.Interfaces;

namespace HOMSys.Application.Services;

/// <summary>
/// TASAF LSP / case: the price of each SKU for the picked stores (Cust Keys), with the same rule
/// as the Sales Order price lookup (PriceCalculationService) — base price + the store's zone
/// add-on (ZONE) + its customer add-on (ZONE2) — done in bulk so thousands of stores stay fast.
/// Stores are grouped by price; the form needs them all on ONE price per SKU to submit.
/// </summary>
public class TasafPriceService(IPricingRepository pricing, IProductRepository products, PriceCalculationService priceCalc)
{
    /// <summary>LSP / case includes 12% VAT (user decision 2026-10-14).</summary>
    public const bool IncludesVat = true;
    private const decimal Vat = 1.12m;

    /// <summary>Half-up like VFP ROUND() — C# Math.Round defaults to half-even (777.445 → 777.44, BMS 777.45).</summary>
    private static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public async Task<List<TasafSkuPriceDto>> PriceAsync(IEnumerable<string> cProdNos, IEnumerable<string> custKeys, DateOnly asOf)
    {
        var skus = cProdNos.Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var keys = custKeys.Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var result = new List<TasafSkuPriceDto>();
        if (skus.Count == 0) return result;

        // Each store's pricing folder + zone (same fallback as CustomerBranchResolver: unknown → hon, no zone).
        var found = (await pricing.GetCustomerBranchZonesAsync(keys))
            .ToDictionary(x => x.CustKey, StringComparer.OrdinalIgnoreCase);
        var stores = keys.Select(k => found.TryGetValue(k, out var z)
                ? (Key: k, Folder: CustomerBranchResolver.PricingFolder(z.Branch).ToLowerInvariant(), Zone: z.CZone)
                : (Key: k, Folder: CustomerBranchResolver.DefaultBranch, Zone: string.Empty))
            .ToList();
        var folders = stores.Select(s => s.Folder).Distinct().ToList();
        var zones = stores.Select(s => s.Zone).Distinct().ToList();

        // Latest row (EffDate desc, undated last — as the single-quote SQL orders) per key.
        var zoneAddOn = (await pricing.GetZoneAddOnRowsAsync(skus, folders, zones, asOf))
            .GroupBy(z => (B: z.Branch.Trim().ToLowerInvariant(), P: z.CProdNo.Trim(), Z: z.CZone.Trim()))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(z => z.EffDate).First().AddOn);
        var zone2 = (await pricing.GetZone2AddOnRowsAsync(skus, folders, keys.Concat(zones).Distinct().ToList(), asOf))
            .GroupBy(z => (B: z.Branch.Trim().ToLowerInvariant(), P: z.CProdNo.Trim(), K: z.CustKey.Trim()))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(z => z.EffDate).First());

        foreach (var sku in skus)
        {
            var product = await products.GetByCProdNoAsync(sku);
            var basePrice = product is null ? null : await priceCalc.GetBasePriceAsync(product, asOf);
            var dto = new TasafSkuPriceDto { CProdNo = sku, HasPrice = basePrice is not null };
            if (basePrice is decimal bp && stores.Count > 0)
            {
                dto.Groups = stores
                    .Select(s =>
                    {
                        var za = zoneAddOn.GetValueOrDefault((s.Folder, sku, s.Zone));
                        // ZONE2: a row for the store itself or for its zone (chain level), latest wins.
                        zone2.TryGetValue((s.Folder, sku, s.Key), out var byKey);
                        zone2.TryGetValue((s.Folder, sku, s.Zone), out var byZone);
                        var z2 = new[] { byKey, byZone }.Where(z => z is not null).OrderByDescending(z => z!.EffDate).FirstOrDefault();
                        var exVat = Round2(bp + za + (z2?.AddOn ?? 0m));
                        return (s.Key, Price: IncludesVat ? Round2(exVat * Vat) : exVat);
                    })
                    .GroupBy(x => x.Price)
                    .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                    .Select(g => new TasafPriceGroupDto { Price = g.Key, CustKeys = g.Select(x => x.Key).ToList() })
                    .ToList();
            }
            result.Add(dto);
        }
        return result;
    }
}
