namespace HOMSys.Application.Services;

/// <summary>
/// How HOMS numbers a branch's OR / RPDC and which department posts it, as found in the ARCHIVES files
/// (OH03/GH03 Aug–Sep 2026): DOCNO = prefix × 1,000,000 + the printed receipt no.; the prefix is the branch's SL
/// warehouse except MEX (85), and HON/CDC receipts carry the printed no. as-is (prefix 0, like HO).
/// DEPTCODE 31 = Visayas/Mindanao desk, 32 = Luzon/HO desk.
/// </summary>
public static class HomsDocMap
{
    private static readonly Dictionary<string, int> PrefixOverride = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MEX-B"] = 85, ["HON-B"] = 0, ["CDC-B"] = 0,
        // Remittance Advice CRs (NKA chains) are Head Office's: printed OR no. as-is, desk from the RA Excel.
        ["HO"] = 0,
    };

    private static readonly HashSet<int> LuzonDesk = [0, 36, 79, 85, 86, 94, 96];

    /// <summary>DOCNO prefix for the site; null when the branch has no mapping.</summary>
    public static int? Prefix(string siteCode) =>
        PrefixOverride.TryGetValue(siteCode, out var p) ? p : BranchWarehouseMap.Wh(siteCode);

    public static int DocNo(int prefix, int printedNo) => prefix == 0 ? printedNo : prefix * 1_000_000 + printedNo;

    /// <summary>The branch a prefixed HOMS DOCNO belongs to (85 → MEX-B; otherwise the SL warehouse).</summary>
    public static string? SiteCode(int prefix) =>
        PrefixOverride.FirstOrDefault(kv => kv.Value == prefix && prefix != 0).Key ?? BranchWarehouseMap.SiteCode(prefix);

    public static int DeptCode(int prefix) => LuzonDesk.Contains(prefix) ? 32 : 31;
}
