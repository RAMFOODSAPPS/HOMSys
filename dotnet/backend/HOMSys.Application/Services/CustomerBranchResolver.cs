using HOMSys.Application.Interfaces;

namespace HOMSys.Application.Services;

/// <summary>
/// Resolves a customer's pricing branch + CZone. Shared by
/// PriceCalculationService (single-SKU quote) and PricelistExportService
/// (bulk pricelist export) so the branch-mapping rule lives in one place.
/// </summary>
public class CustomerBranchResolver(ICustomerRepository customerRepo, IPricingRepository pricingRepo)
{
    /// <summary>
    /// F:\AUTOPROG\CUSTOMER\{branch} folder name -> F:\AUTOPROG\ADDON\{branch}
    /// pricing-folder name, for the branches that keep their own CUST4WIN.DBF
    /// but have no ADDON folder of their own and instead price off hon's
    /// ZONE/ZONE2 (Dagupan, Isabela, Legazpi, Lucena, Mexico, Naga â€” confirmed
    /// on disk). Every other CUSTOMER folder name matches its own ADDON folder
    /// name 1:1, so GetValueOrDefault falls back to the CustomerBranchZone row's own
    /// Branch unchanged. NOTE: Customer.WhseNo (BMSRAM) does NOT correspond to
    /// the "wh" field in config-*.json â€” nearly every live customer's WhseNo
    /// falls outside that numbering, so branch resolution must come from the
    /// per-customer CustomerBranchZone row (F:\ ground truth), not WhseNo.
    /// </summary>
    private static readonly Dictionary<string, string> CustomerBranchToPricingFolder = new()
    {
        ["DAG"] = "hon", ["ISA"] = "hon", ["LEG"] = "hon", ["LUC"] = "hon",
        ["mxs"] = "hon", ["NAG"] = "hon"
    };

    public const string DefaultBranch = "hon";

    /// <summary>
    /// CustomerBranchZone (F:\AUTOPROG\CUSTOMER\{branch}\cust4win.dbf) carries both
    /// the real branch tag and CZone for this customer â€” the trustworthy
    /// source. Falls back to the BMSRAM-sourced Customer.CZone/DefaultBranch
    /// only if this customer hasn't been backfilled into CustomerBranchZone yet.
    /// </summary>
    public async Task<(string Branch, string CZone)> ResolveAsync(string? custKey)
    {
        custKey = custKey?.Trim();
        if (string.IsNullOrEmpty(custKey))
            return (DefaultBranch, string.Empty);

        var zone = await pricingRepo.GetCustomerBranchZoneAsync(custKey);
        if (zone is not null)
        {
            var branch = CustomerBranchToPricingFolder.GetValueOrDefault(zone.Value.Branch, zone.Value.Branch);
            return (branch, zone.Value.CZone);
        }

        var customer = await customerRepo.GetByCustKeyAsync(custKey);
        return (DefaultBranch, customer?.CZone ?? string.Empty);
    }
}
