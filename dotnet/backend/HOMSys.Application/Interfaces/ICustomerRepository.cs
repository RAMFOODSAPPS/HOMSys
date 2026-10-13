using HOMSys.Application.DTOs.SalesOrders;
using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

/// <summary>Just enough per-customer info to group and pick a representative — see GroupedPricelistService.</summary>
public record CustomerGroupingInfo(string CustKey, string CusName, DateOnly? FirstOrder);

/// <summary>A store picked by name keywords (TASAF Cust Key dropdown for a chain).</summary>
public record CustomerMatchInfo(string CustKey, string CusName, string Branch, string Address);

public interface ICustomerRepository
{
    Task<Customer?> GetByCustKeyAsync(string custKey);
    Task<Dictionary<string, Customer>> GetByCustKeysAsync(IEnumerable<string> custKeys);
    /// <summary>Every active (non-Inactive) customer of a branch, optionally narrowed to
    /// specific cuwhsenos — the base customer set for the Grouped-by-Price report.</summary>
    Task<List<CustomerGroupingInfo>> GetActiveBranchCustomersAsync(string branch, IReadOnlyCollection<int>? whseNos);

    /// <summary>CZone -&gt; (representative customer name, active customer count) for a
    /// branch, using the same "most recently onboarded, else highest CustKey"
    /// representative rule as GroupedPricelistService. Labeling only.</summary>
    Task<Dictionary<string, (string RepresentativeName, int Count)>> GetZoneCustomerSummariesAsync(
        string branch, IReadOnlyCollection<int>? whseNos);
    /// <summary>
    /// If <paramref name="pricingFolder"/> is given, results are restricted to
    /// customers whose own Branch matches that folder (optionally narrowed
    /// further to <paramref name="whseNos"/> — cuwhsenos — when a shared folder
    /// like "hon" covers more than one real branch), and Inactive customers
    /// are excluded.
    /// </summary>
    /// <summary>
    /// Active customers whose name contains ANY of the fragments (SQL LIKE pieces, so %/_ work),
    /// one row per CustKey, by name. Used to list a TASAF chain's stores.
    /// </summary>
    Task<List<CustomerMatchInfo>> MatchByNameAsync(IReadOnlyList<string> fragments, int take);

    Task<IEnumerable<CustomerSuggestionDto>> SearchAsync(
        string term, int take = 50, string? pricingFolder = null, IReadOnlyCollection<int>? whseNos = null);
}
