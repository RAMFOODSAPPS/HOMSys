using HOMSys.Application.Interfaces;

namespace HOMSys.Application.Services;

public record ZonePricelistColumn(string Zone);

public record ZonePricelistValue(decimal? CasePriceWithVat, decimal? UnitPriceWithVat, decimal? Srp);

public record ZonePricelistRow(
    string CProdNo,
    string ProdDesc,
    string PackSize,
    int Pieces,
    string CaseBarcode,
    string Barcode,
    Dictionary<string, ZonePricelistValue> ByZone);

public record ZonePricelistCategoryGroup(string? Header, List<ZonePricelistRow> Rows);

public record ZonePricelistExportResult(
    string Branch,
    DateOnly EffectivityDate,
    decimal SrpMarkupPercent,
    List<ZonePricelistColumn> Zones,
    List<ZonePricelistCategoryGroup> Groups);

/// <summary>
/// Builds the one-sheet, per-zone-column-pair pricelist comparison for a single
/// branch (List Price/Unit w/ VAT + SRP per selected zone), grouped by category
/// in the same GroupNo/SeqNo order as F:\PMDM\prodcat.dbf. Mirrors
/// PricelistExportService but keyed by zone instead of customer — branch/zone
/// are user-selected directly, so no CustomerBranchResolver is involved.
/// </summary>
public class ZonePricelistExportService(
    IProductRepository productRepo,
    IPricingRepository pricingRepo,
    ISiteRepository siteRepo,
    PriceCalculationService priceCalc)
{
    /// <summary>
    /// Every active Site with PricesOffHon=true has no F:\AUTOPROG\ADDON folder of its
    /// own — they price off hon's ZONE/ZONE2 tables directly. Keyed by
    /// Site.Name, valued by the parsed Site.Cuwhsenos (cust4win.WHSENO values
    /// for that branch, used to narrow hon's shared zone universe down to the
    /// zones that branch's own customers actually use). A blank Cuwhsenos
    /// means no whseno filter is applied — that site sees the full unfiltered
    /// zone list for the "hon" folder. Sites with PricesOffHon=false (independent
    /// branches with their own ADDON folder, plus non-pricing sites like Head
    /// Office) are excluded — their branch identity already comes from
    /// IPricingRepository.GetBranchesWithZonesAsync().
    /// </summary>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<int>>> GetHonBranchesAsync()
    {
        var sites = await siteRepo.GetAllAsync();
        return sites
            .Where(s => s.IsActive && s.PricesOffHon)
            .ToDictionary(
                s => s.Name,
                s => (IReadOnlyList<int>)ParseWhseNos(s.Cuwhsenos),
                StringComparer.OrdinalIgnoreCase);
    }

    private static List<int> ParseWhseNos(string cuwhsenos) =>
        string.IsNullOrWhiteSpace(cuwhsenos)
            ? []
            : cuwhsenos.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(int.Parse)
                .ToList();

    public static string ResolvePricingFolder(string branch, IEnumerable<string> honBranchNames) =>
        honBranchNames.Contains(branch, StringComparer.OrdinalIgnoreCase) ? "hon" : branch;

    public async Task<ZonePricelistExportResult> BuildAsync(
        string branch, IEnumerable<string> zones, DateOnly effectivityDate, decimal srpMarkupPercent)
    {
        var branchKey = branch.Trim();
        if (branchKey.Length == 0)
            throw new ArgumentException("A branch must be selected.", nameof(branch));
        var honBranches = await GetHonBranchesAsync();
        var pricingFolder = ResolvePricingFolder(branchKey, honBranches.Keys);

        var zoneKeys = zones.Select(z => z.Trim()).Where(z => z.Length > 0).Distinct().ToList();
        if (zoneKeys.Count == 0)
            throw new ArgumentException("At least one zone must be selected.", nameof(zones));

        var columns = zoneKeys.Select(z => new ZonePricelistColumn(z)).ToList();

        // Per-zone bulk add-on dictionaries, once each. Zone2AddOn's CustKey==CustKey||CustKey==cZone
        // query collapses to CustKey==zone here (no customer in hand), picking up only chain-level
        // rows keyed directly on the zone code — a no-op for zones with no such rows.
        var addOnsByZone = new Dictionary<string, (Dictionary<string, decimal> Zone, Dictionary<string, decimal> Zone2)>();
        foreach (var zone in zoneKeys)
        {
            var zoneAddOns = await pricingRepo.GetZoneAddOnsAsync(pricingFolder, zone, effectivityDate);
            var zone2AddOns = await pricingRepo.GetZone2AddOnsAsync(pricingFolder, zone, zone, effectivityDate);
            addOnsByZone[zone] = (zoneAddOns, zone2AddOns);
        }

        // SKUs hidden from the pricelist by default (F:\PMDM\PRLISTX2.DBF), shown only
        // for zones that are in the allowed set for that CProdNo.
        var restrictedZonesByProdNo = await pricingRepo.GetPrlistX2RestrictedZonesAsync();

        // SKUs excluded from the pricelist entirely, for every zone, no
        // exception (F:\PMDM\PRLISTX.DBF) — unlike PRLISTX2 above, this is a
        // full row removal, not a per-zone blank.
        var excludedProdNos = await pricingRepo.GetPrlistXRestrictedProdNosAsync();

        var products = await productRepo.GetPriceListWithCategoryAsync();

        var groups = new List<ZonePricelistCategoryGroup>();
        string? currentHeader = null;
        List<ZonePricelistRow>? currentRows = null;
        var hasGroup = false;

        foreach (var pr in products)
        {
            var product = pr.Product;
            if (excludedProdNos.Contains(product.CProdNo)) continue;

            var basePrice = await priceCalc.GetBasePriceAsync(product, effectivityDate);

            var byZone = new Dictionary<string, ZonePricelistValue>();
            var anyPrice = false;

            foreach (var zone in zoneKeys)
            {
                if (basePrice is null)
                {
                    byZone[zone] = new ZonePricelistValue(null, null, null);
                    continue;
                }

                if (restrictedZonesByProdNo.TryGetValue(product.CProdNo, out var allowedZones)
                    && !allowedZones.Contains(zone))
                {
                    byZone[zone] = new ZonePricelistValue(null, null, null);
                    continue;
                }

                var (zoneAddOns, zone2AddOns) = addOnsByZone[zone];
                var zoneAddOn = zoneAddOns.GetValueOrDefault(product.CProdNo);
                var zone2AddOn = zone2AddOns.GetValueOrDefault(product.CProdNo);

                var casePriceExVat = basePrice.Value + zoneAddOn + zone2AddOn;
                var casePriceWithVat = casePriceExVat * (1 + product.TaxRate);
                var unitPriceWithVat = product.Pieces > 0 ? casePriceWithVat / product.Pieces : casePriceWithVat;
                var srp = unitPriceWithVat * (1 + srpMarkupPercent / 100m);

                byZone[zone] = new ZonePricelistValue(
                    Math.Round(casePriceWithVat, 2), Math.Round(unitPriceWithVat, 2), Math.Round(srp, 2));
                anyPrice = true;
            }

            if (!anyPrice) continue;

            if (!hasGroup || pr.CategoryHeader != currentHeader)
            {
                hasGroup = true;
                currentHeader = pr.CategoryHeader;
                currentRows = [];
                groups.Add(new ZonePricelistCategoryGroup(currentHeader, currentRows));
            }

            currentRows!.Add(new ZonePricelistRow(
                product.CProdNo, product.ProdDesc, product.PackSize, product.Pieces,
                product.CaseBarcode, product.Barcode, byZone));
        }

        return new ZonePricelistExportResult(branchKey, effectivityDate, srpMarkupPercent, columns, groups);
    }
}
