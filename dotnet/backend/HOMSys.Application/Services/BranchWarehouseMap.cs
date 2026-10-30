namespace HOMSys.Application.Services;

/// <summary>
/// Branch warehouse number (the "WH" in SL{WH}{MMYY}.DBF and the BMS_auto_Distribution
/// configs) -> Site.Code. Static like the pricing-folder map; add a row when a branch is added.
/// </summary>
public static class BranchWarehouseMap
{
    private static readonly Dictionary<int, string> SiteCodeByWh = new()
    {
        [87] = "CDC-B", [79] = "LUC-B", [10] = "MEX-B", [86] = "DAG-B", [94] = "NAG-B",
        [96] = "ISA-B", [36] = "LEG-B", [88] = "HON-B", [81] = "CEB-B", [82] = "ILO-B",
        [83] = "BAC-B", [84] = "TAC-B", [91] = "CDO-B", [92] = "DAV-B", [93] = "ZAM-B",
        [99] = "DUM-B", [97] = "GEN-B", [73] = "BUT-B", [89] = "KID-B", [37] = "ROX-B",
        [33] = "TGM-B", [38] = "OZA-B", [32] = "PAN-B", [18] = "VAL-B", [46] = "SVS-B",
        [90] = "OPL-B",
    };

    public static string? SiteCode(int wh) => SiteCodeByWh.GetValueOrDefault(wh);

    /// <summary>Reverse lookup; null when the site has no warehouse mapping.</summary>
    public static int? Wh(string siteCode) =>
        SiteCodeByWh.Where(kv => string.Equals(kv.Value, siteCode, StringComparison.OrdinalIgnoreCase))
            .Select(kv => (int?)kv.Key).FirstOrDefault();
}
