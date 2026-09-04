using HOMSys.Application.DTOs.Pricing;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;

namespace HOMSys.Application.Services;

/// <summary>
/// Price Per Case lookup for the Sales Order encode grid â€” display-only,
/// never written to SalesOrderLine.Price/Amt/NetAmt (BMS-owned, filled by a
/// future Python bridge). LP w/ VAT itself is derived client-side from
/// PricePerCase * QtyCs * 1.12. Formula/lookup rules follow the Pricing
/// Adjustment subsystem's own documented source of truth
/// (C:\claude\pricing\CLAUDE.md) â€” reused verbatim, not re-derived.
/// See C:\Users\RDEGUZMAN\.claude\plans\can-you-see-this-jaunty-puffin.md Part 2.
/// </summary>
public class PriceCalculationService(
    IProductRepository productRepo,
    CustomerBranchResolver branchResolver,
    IPricingRepository pricingRepo)
{
    public async Task<PriceQuoteDto> GetQuoteAsync(string cProdNo, string? custKey)
    {
        var product = await productRepo.GetByCProdNoAsync(cProdNo.Trim());
        if (product is null) return new PriceQuoteDto { HasPrice = false };

        var today = DateOnly.FromDateTime(DateTime.Now);
        var basePrice = await GetBasePriceAsync(product, today);
        if (basePrice is null) return new PriceQuoteDto { HasPrice = false };

        var (branch, cZone) = await branchResolver.ResolveAsync(custKey);
        custKey = custKey?.Trim();

        var zoneAddOn = await pricingRepo.GetZoneAddOnAsync(branch, product.CProdNo, cZone, today);
        var zone2AddOn = string.IsNullOrEmpty(custKey)
            ? 0m
            : await pricingRepo.GetZone2AddOnAsync(branch, product.CProdNo, custKey, cZone, today);

        var pricePerCase = basePrice.Value + zoneAddOn + zone2AddOn;
        return new PriceQuoteDto { HasPrice = true, PricePerCase = Math.Round(pricePerCase, 2) };
    }

    /// <summary>
    /// Ex-VAT case price as of the given date: the current NewPrice if it's
    /// already effective, else the latest PriceHistory row on or before that
    /// date (NpAfVat is with-VAT, so divide back out). Public so bulk
    /// consumers (PricelistExportService) apply the identical fallback rule.
    /// </summary>
    public async Task<decimal?> GetBasePriceAsync(Product product, DateOnly asOf)
    {
        if (product.NewPrice is not null && product.PriceFrom is not null && product.PriceFrom <= asOf)
            return product.NewPrice;

        var hist = await pricingRepo.GetLatestPriceHistoryNpAfVatAsync(product.ProdNo, asOf);
        return hist is null ? null : hist.Value / 1.12m;
    }
}
