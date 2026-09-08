using HOMSys.Application.DTOs.SalesOrders;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class CustomerRepository(AppDbContext db) : ICustomerRepository
{
    // CustKey lives in exactly one branch's file in practice — take the most
    // recently imported row if that ever isn't true, rather than throwing.
    public async Task<Customer?> GetByCustKeyAsync(string custKey) =>
        await db.Customers.AsNoTracking()
            .Where(c => c.CustKey == custKey)
            .OrderByDescending(c => c.ImportedAt)
            .FirstOrDefaultAsync();

    public async Task<Dictionary<string, Customer>> GetByCustKeysAsync(IEnumerable<string> custKeys)
    {
        var keys = custKeys.Distinct().ToList();
        var rows = await db.Customers.AsNoTracking()
            .Where(c => keys.Contains(c.CustKey))
            .ToListAsync();
        return rows
            .GroupBy(c => c.CustKey)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.ImportedAt).First());
    }

    // Each whitespace-separated keyword must match somewhere in CustKey/CusName,
    // in any order (e.g. "Puregold Isabela" matches "Puregold Price Club - Isabela
    // St. Manila"). This needs a leading-wildcard LIKE per token, which can't use
    // the CustKey/CusName indexes — acceptable because the frontend now debounces
    // 1s before calling this, instead of on every keystroke.
    public async Task<IEnumerable<CustomerSuggestionDto>> SearchAsync(
        string term, int take = 50, string? pricingFolder = null, IReadOnlyCollection<int>? whseNos = null)
    {
        var tokens = term.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var query = db.Customers.AsNoTracking();
        foreach (var token in tokens)
            query = query.Where(c => c.CustKey.Contains(token) || c.CusName.Contains(token));

        // Inactive-exclusion only applies to branch-scoped search (Pricelist Export) —
        // the general Sales Order encode typeahead has never gated on this and
        // isn't being changed here.
        if (!string.IsNullOrWhiteSpace(pricingFolder))
        {
            query = query.Where(c => c.Branch == pricingFolder && !c.Inactive);
            if (whseNos is { Count: > 0 })
                query = query.Where(c => whseNos.Contains(c.WhseNo));
        }

        return await query
            .OrderByDescending(c => c.CustKey.StartsWith(term))
            .ThenBy(c => c.CusName)
            .Select(c => new CustomerSuggestionDto { CustKey = c.CustKey, CusName = c.CusName })
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<CustomerGroupingInfo>> GetActiveBranchCustomersAsync(string branch, IReadOnlyCollection<int>? whseNos)
    {
        var query = db.Customers.AsNoTracking().Where(c => c.Branch == branch && !c.Inactive);
        if (whseNos is { Count: > 0 })
            query = query.Where(c => whseNos.Contains(c.WhseNo));

        return await query
            .Select(c => new CustomerGroupingInfo(c.CustKey, c.CusName, c.FirstOrder))
            .ToListAsync();
    }

    public async Task<Dictionary<string, (string RepresentativeName, int Count)>> GetZoneCustomerSummariesAsync(
        string branch, IReadOnlyCollection<int>? whseNos)
    {
        var query = db.Customers.AsNoTracking().Where(c => c.Branch == branch && !c.Inactive);
        if (whseNos is { Count: > 0 })
            query = query.Where(c => whseNos.Contains(c.WhseNo));

        var rows = await query
            .Select(c => new { c.CZone, c.CustKey, c.CusName, c.FirstOrder })
            .ToListAsync();

        return rows
            .GroupBy(c => c.CZone)
            .ToDictionary(g => g.Key, g =>
            {
                var rep = g
                    .OrderByDescending(m => m.FirstOrder.HasValue)
                    .ThenByDescending(m => m.FirstOrder)
                    .ThenByDescending(m => m.CustKey)
                    .First();
                return (rep.CusName, g.Count());
            });
    }
}
