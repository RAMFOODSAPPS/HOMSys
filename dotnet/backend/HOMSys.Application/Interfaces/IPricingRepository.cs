namespace HOMSys.Application.Interfaces;

public interface IPricingRepository
{
    /// <summary>Latest PriceHistory.NpAfVat with Effective &lt;= asOf, or null if none.</summary>
    Task<decimal?> GetLatestPriceHistoryNpAfVatAsync(int prodNo, DateOnly asOf);

    /// <summary>Latest ZoneAddOn.AddOn for the branch/product/zone with EffDate &lt;= asOf, or 0 if none.</summary>
    Task<decimal> GetZoneAddOnAsync(string branch, string cProdNo, string cZone, DateOnly asOf);

    /// <summary>Latest Zone2AddOn.AddOn matching CustKey == custKey OR CustKey == cZone (chain-level), or 0 if none.</summary>
    Task<decimal> GetZone2AddOnAsync(string branch, string cProdNo, string custKey, string cZone, DateOnly asOf);

    /// <summary>CustomerBranchZone (Branch, CZone) for this CustKey, or null if none.</summary>
    Task<(string Branch, string CZone)?> GetCustomerBranchZoneAsync(string custKey);

    /// <summary>
    /// Bulk form of GetZoneAddOnAsync for every CProdNo in one branch/zone at
    /// once (pricelist export) â€” same "latest EffDate &lt;= asOf" tie-break,
    /// just grouped into one query instead of one per SKU. Only keys with a
    /// nonzero result are present.
    /// </summary>
    Task<Dictionary<string, decimal>> GetZoneAddOnsAsync(string branch, string cZone, DateOnly asOf);

    /// <summary>
    /// Bulk form of GetZone2AddOnAsync for one customer (and its chain-level
    /// CZone fallback) across every CProdNo at once. Only keys with a nonzero
    /// result are present.
    /// </summary>
    Task<Dictionary<string, decimal>> GetZone2AddOnsAsync(string branch, string custKey, string cZone, DateOnly asOf);

    /// <summary>
    /// CProdNo -&gt; allowed zones, from PrlistX2Restriction. A CProdNo present
    /// here is hidden from the pricelist export unless the customer's CZone
    /// is in the set; a CProdNo absent here has no restriction.
    /// </summary>
    Task<Dictionary<string, HashSet<string>>> GetPrlistX2RestrictedZonesAsync();
    Task<HashSet<string>> GetPrlistXRestrictedProdNosAsync();
}
