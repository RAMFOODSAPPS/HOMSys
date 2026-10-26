using HOMSys.Application.DTOs.SalesOrders;
using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

/// <summary>Just enough per-customer info to group and pick a representative — see GroupedPricelistService.</summary>
public record CustomerGroupingInfo(string CustKey, string CusName, DateOnly? FirstOrder);

/// <summary>A store picked by name keywords (TASAF Cust Key dropdown for a chain).</summary>
public record CustomerMatchInfo(string CustKey, string CusName, string Branch, string Address);

/// <summary>
/// Customers of a set of branches (Sites): WhseNo in <see cref="WhseNos"/> (the sites'
/// Cuwhsenos), or — for a site with no Cuwhsenos, e.g. HON-B — in one of <see cref="Folders"/>
/// with a WhseNo no site claims (<see cref="ClaimedWhseNos"/>).
/// </summary>
public record CustomerBranchScope(IReadOnlyList<int> WhseNos, IReadOnlyList<string> Folders, IReadOnlyList<int> ClaimedWhseNos);

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
    /// Active customers, one row per CustKey, by name — the TASAF Cust Key dropdown. Narrowed to
    /// names containing ANY of the fragments (SQL LIKE pieces, so %/_ work) when given, and to the
    /// branches in <paramref name="scope"/> when given (null = every branch).
    /// </summary>
    Task<List<CustomerMatchInfo>> MatchAsync(IReadOnlyList<string> fragments, CustomerBranchScope? scope, int take);
    /// <summary>How many distinct CustKeys <see cref="MatchAsync"/> would find (no cap).</summary>
    Task<int> CountMatchAsync(IReadOnlyList<string> fragments, CustomerBranchScope? scope);

    /// <summary>
    /// If <paramref name="pricingFolder"/> is given, results are restricted to
    /// customers whose own Branch matches that folder (optionally narrowed
    /// further to <paramref name="whseNos"/> — cuwhsenos — when a shared folder
    /// like "hon" covers more than one real branch), and Inactive customers
    /// are excluded.
    /// </summary>
    Task<IEnumerable<CustomerSuggestionDto>> SearchAsync(
        string term, int take = 50, string? pricingFolder = null, IReadOnlyCollection<int>? whseNos = null);

    /// <summary>Sets the withholding flag on every row with this CustKey; returns the rows updated (0 = unknown customer).</summary>
    Task<int> SetWithholdingAsync(string custKey, bool withholding, string updatedBy);
}
