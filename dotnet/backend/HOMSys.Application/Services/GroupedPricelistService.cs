using System.Text;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;

namespace HOMSys.Application.Services;

public record GroupedPricelistResult(PricelistExportResult Result, Customer? Baseline);

/// <summary>
/// Groups a branch's active customers by IDENTICAL computed price across every
/// SKU (not per-SKU independently — a customer's full price vector must match
/// another's exactly to share a column), instead of one column per customer.
/// Reuses PricelistExportService for the actual pricing, so it always sees the
/// same product/pricing rules as the Per Account report. Validated 2026-09-08
/// against Isabela before becoming this Pricelist Export report mode.
/// </summary>
public class GroupedPricelistService(
    ISiteRepository siteRepo,
    ZonePricelistExportService zoneExport,
    ICustomerRepository customerRepo,
    PricelistExportService pricelistExport)
{
    public async Task<GroupedPricelistResult> BuildAsync(
        string branchSiteName, DateOnly effectivityDate, decimal srpMarkupPercent, string? baselineCustKey)
    {
        var sites = await siteRepo.GetAllAsync();
        var site = sites.FirstOrDefault(s => s.Name.Equals(branchSiteName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Branch '{branchSiteName}' not found.", nameof(branchSiteName));

        var honBranches = await zoneExport.GetHonBranchesAsync();
        var pricingFolder = ZonePricelistExportService.ResolvePricingFolder(site.Name, honBranches.Keys);
        honBranches.TryGetValue(site.Name, out var whseNos);

        var customers = await customerRepo.GetActiveBranchCustomersAsync(pricingFolder, whseNos);
        if (customers.Count == 0)
            throw new ArgumentException($"No active customers found for branch '{branchSiteName}'.", nameof(branchSiteName));

        // The baseline may not be an active customer of this branch (different
        // branch, or itself inactive) — fetch it separately and fold it into the
        // customer set either way, so its price still gets computed.
        Customer? baseline = null;
        var custInfoList = new List<CustomerGroupingInfo>(customers);
        if (!string.IsNullOrWhiteSpace(baselineCustKey))
        {
            baseline = await customerRepo.GetByCustKeyAsync(baselineCustKey.Trim());
            if (baseline is not null && !custInfoList.Any(c => c.CustKey == baseline.CustKey))
                custInfoList.Add(new CustomerGroupingInfo(baseline.CustKey, baseline.CusName, baseline.FirstOrder));
        }

        var custKeys = custInfoList.Select(c => c.CustKey).Distinct().ToList();
        var result = await pricelistExport.BuildAsync(custKeys, effectivityDate, srpMarkupPercent);

        // Vector = every row's (case, unit, srp) for this customer, in row order —
        // two customers share a group only if this string matches exactly.
        var vectorByCustKey = new Dictionary<string, string>(custKeys.Count);
        foreach (var custKey in custKeys)
        {
            var sb = new StringBuilder();
            foreach (var group in result.Groups)
                foreach (var row in group.Rows)
                {
                    var v = row.ByCustKey.GetValueOrDefault(custKey);
                    sb.Append(v?.CasePriceWithVat).Append('|').Append(v?.UnitPriceWithVat).Append('|').Append(v?.Srp).Append(';');
                }
            vectorByCustKey[custKey] = sb.ToString();
        }

        var custInfo = custInfoList.ToDictionary(c => c.CustKey);
        var priceGroups = custKeys.GroupBy(k => vectorByCustKey[k]).OrderByDescending(g => g.Count()).ToList();

        var representativeColumns = new List<PricelistCustomerColumn>();
        foreach (var g in priceGroups)
        {
            var members = g.Select(k => custInfo[k]).ToList();
            // "Most recently onboarded": prefer the customer with a FirstOrder date
            // (max among those that have one); FirstOrder is often blank, so fall
            // back to the highest CustKey (keys are assigned sequentially) when
            // nobody in the group has one.
            var rep = members
                .OrderByDescending(m => m.FirstOrder.HasValue)
                .ThenByDescending(m => m.FirstOrder)
                .ThenByDescending(m => m.CustKey)
                .First();
            var label = members.Count > 1 ? $"{rep.CusName} & {members.Count - 1} more" : rep.CusName;
            representativeColumns.Add(new PricelistCustomerColumn(rep.CustKey, label));
        }

        var groupedResult = result with { Customers = representativeColumns };
        return new GroupedPricelistResult(groupedResult, baseline);
    }
}
