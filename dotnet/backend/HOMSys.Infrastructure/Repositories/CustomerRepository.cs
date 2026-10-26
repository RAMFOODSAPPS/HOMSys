using System.Linq.Expressions;
using HOMSys.Application.DTOs.SalesOrders;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class CustomerRepository(AppDbContext db) : ICustomerRepository
{
    public async Task<List<CustomerMatchInfo>> MatchAsync(IReadOnlyList<string> fragments, CustomerBranchScope? scope, int take)
    {
        var rows = await Matching(fragments, scope)
            .OrderBy(x => x.CusName)
            .Select(x => new { x.CustKey, x.CusName, x.Branch, x.AddrLn1, x.AddrLn2, x.Id })
            .Take(take * 2) // a CustKey can sit in more than one branch file — dedupe below
            .ToListAsync();

        return rows
            .GroupBy(x => x.CustKey)
            .Select(g => g.OrderByDescending(x => x.Id).First())
            .Take(take)
            .Select(x => new CustomerMatchInfo(x.CustKey.Trim(), x.CusName.Trim(), x.Branch,
                $"{x.AddrLn1.Trim()} {x.AddrLn2.Trim()}".Trim()))
            .ToList();
    }

    public Task<int> CountMatchAsync(IReadOnlyList<string> fragments, CustomerBranchScope? scope) =>
        Matching(fragments, scope).Select(x => x.CustKey).Distinct().CountAsync();

    /// <summary>Active customers narrowed by name fragments and branch scope (see MatchAsync).</summary>
    private IQueryable<Customer> Matching(IReadOnlyList<string> fragments, CustomerBranchScope? scope)
    {
        var q = db.Customers.AsNoTracking().Where(x => !x.Inactive);

        if (fragments.Count > 0)
        {
            // CusName LIKE '%f1%' OR CusName LIKE '%f2%' ... built as an expression so EF
            // translates it to one parameterised WHERE (fragments are values, never SQL text).
            var c = Expression.Parameter(typeof(Customer), "c");
            var name = Expression.Property(c, nameof(Customer.CusName));
            var like = typeof(DbFunctionsExtensions).GetMethod(nameof(DbFunctionsExtensions.Like),
                [typeof(DbFunctions), typeof(string), typeof(string)])!;
            Expression? any = null;
            foreach (var f in fragments)
            {
                var call = Expression.Call(like, Expression.Constant(EF.Functions), name, Expression.Constant($"%{f}%"));
                any = any is null ? call : Expression.OrElse(any, call);
            }
            q = q.Where(Expression.Lambda<Func<Customer, bool>>(any!, c));
        }

        if (scope is not null)
        {
            var whse = scope.WhseNos.ToList();
            var folders = scope.Folders.ToList();
            var claimed = scope.ClaimedWhseNos.ToList();
            q = q.Where(x => whse.Contains(x.WhseNo) || (folders.Contains(x.Branch) && !claimed.Contains(x.WhseNo)));
        }
        return q;
    }

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

    public async Task<int> SetWithholdingAsync(string custKey, bool withholding, string updatedBy)
    {
        var now = DateTime.UtcNow;
        return await db.Customers
            .Where(c => c.CustKey == custKey)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Withholding, withholding)
                .SetProperty(c => c.WithholdingUpdatedAt, now)
                .SetProperty(c => c.WithholdingUpdatedBy, updatedBy));
    }
}
