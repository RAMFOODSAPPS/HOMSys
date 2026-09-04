using HOMSys.Application.Interfaces;

namespace HOMSys.Application.Services;

public record PricelistCustomerColumn(string CustKey, string CusName);

public record PricelistCustomerValue(decimal? CasePriceWithVat, decimal? UnitPriceWithVat, decimal? Srp);

public record PricelistRow(
    string CProdNo,
    string ProdDesc,
    string PackSize,
    int Pieces,
    string CaseBarcode,
    string Barcode,
    Dictionary<string, PricelistCustomerValue> ByCustKey);

public record PricelistCategoryGroup(string? Header, List<PricelistRow> Rows);

public record PricelistExportResult(
    DateOnly EffectivityDate,
    decimal SrpMarkupPercent,
    List<PricelistCustomerColumn> Customers,
    List<PricelistCategoryGroup> Groups);

/// <summary>
/// Builds the one-sheet, per-customer-column-pair pricelist comparison
/// (List Price/Unit w/ VAT + SRP per selected customer), grouped by category
/// in the same GroupNo/SeqNo order as F:\PMDM\prodcat.dbf. Reuses the same
/// pricing rules as the SO encode grid's live quote
/// (PriceCalculationService.GetBasePriceAsync + ZoneAddOn/Zone2AddOn), just
/// batched across every SKU per customer instead of one call per SKU.
/// </summary>
public class PricelistExportService(
    IProductRepository productRepo,
    ICustomerRepository customerRepo,
    IPricingRepository pricingRepo,
    CustomerBranchResolver branchResolver,
    PriceCalculationService priceCalc)
{
    public async Task<PricelistExportResult> BuildAsync(
        IEnumerable<string> custKeys, DateOnly effectivityDate, decimal srpMarkupPercent)
    {
        var keys = custKeys.Select(k => k.Trim()).Where(k => k.Length > 0).Distinct().ToList();
        if (keys.Count == 0)
            throw new ArgumentException("At least one customer must be selected.", nameof(custKeys));

        var customers = await customerRepo.GetByCustKeysAsync(keys);
        var columns = keys
            .Select(k => new PricelistCustomerColumn(k, customers.TryGetValue(k, out var c) ? c.CusName : k))
            .ToList();

        // Per-customer branch/zone resolution + bulk add-on dictionaries, once each.
        var addOnsByCustKey = new Dictionary<string, (Dictionary<string, decimal> Zone, Dictionary<string, decimal> Zone2)>();
        var cZoneByCustKey = new Dictionary<string, string>();
        foreach (var custKey in keys)
        {
            var (branch, cZone) = await branchResolver.ResolveAsync(custKey);
            var zoneAddOns = await pricingRepo.GetZoneAddOnsAsync(branch, cZone, effectivityDate);
            var zone2AddOns = await pricingRepo.GetZone2AddOnsAsync(branch, custKey, cZone, effectivityDate);
            addOnsByCustKey[custKey] = (zoneAddOns, zone2AddOns);
            cZoneByCustKey[custKey] = cZone;
        }

        // SKUs hidden from the pricelist by default (F:\PMDM\PRLISTX2.DBF), shown only
        // to customers whose CZone is one of the allowed zones for that CProdNo.
        var restrictedZonesByProdNo = await pricingRepo.GetPrlistX2RestrictedZonesAsync();

        // SKUs excluded from the pricelist entirely, for every account, no
        // exception (F:\PMDM\PRLISTX.DBF) — unlike PRLISTX2 above, this is a
        // full row removal, not a per-customer blank.
        var excludedProdNos = await pricingRepo.GetPrlistXRestrictedProdNosAsync();

        var products = await productRepo.GetPriceListWithCategoryAsync();

        var groups = new List<PricelistCategoryGroup>();
        string? currentHeader = null;
        List<PricelistRow>? currentRows = null;
        var hasGroup = false;

        foreach (var pr in products)
        {
            var product = pr.Product;
            if (excludedProdNos.Contains(product.CProdNo)) continue;

            var basePrice = await priceCalc.GetBasePriceAsync(product, effectivityDate);

            var byCust = new Dictionary<string, PricelistCustomerValue>();
            var anyPrice = false;

            foreach (var custKey in keys)
            {
                if (basePrice is null)
                {
                    byCust[custKey] = new PricelistCustomerValue(null, null, null);
                    continue;
                }

                if (restrictedZonesByProdNo.TryGetValue(product.CProdNo, out var allowedZones)
                    && !allowedZones.Contains(cZoneByCustKey[custKey]))
                {
                    byCust[custKey] = new PricelistCustomerValue(null, null, null);
                    continue;
                }

                var (zoneAddOns, zone2AddOns) = addOnsByCustKey[custKey];
                var zoneAddOn = zoneAddOns.GetValueOrDefault(product.CProdNo);
                var zone2AddOn = zone2AddOns.GetValueOrDefault(product.CProdNo);

                var casePriceExVat = basePrice.Value + zoneAddOn + zone2AddOn;
                var casePriceWithVat = casePriceExVat * (1 + product.TaxRate);
                var unitPriceWithVat = product.Pieces > 0 ? casePriceWithVat / product.Pieces : casePriceWithVat;
                var srp = unitPriceWithVat * (1 + srpMarkupPercent / 100m);

                byCust[custKey] = new PricelistCustomerValue(
                    Math.Round(casePriceWithVat, 2), Math.Round(unitPriceWithVat, 2), Math.Round(srp, 2));
                anyPrice = true;
            }

            if (!anyPrice) continue;

            if (!hasGroup || pr.CategoryHeader != currentHeader)
            {
                hasGroup = true;
                currentHeader = pr.CategoryHeader;
                currentRows = [];
                groups.Add(new PricelistCategoryGroup(currentHeader, currentRows));
            }

            currentRows!.Add(new PricelistRow(
                product.CProdNo, product.ProdDesc, product.PackSize, product.Pieces,
                product.CaseBarcode, product.Barcode, byCust));
        }

        return new PricelistExportResult(effectivityDate, srpMarkupPercent, columns, groups);
    }
}
