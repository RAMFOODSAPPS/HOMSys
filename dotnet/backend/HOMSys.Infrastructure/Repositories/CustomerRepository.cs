using HOMSys.Application.DTOs.SalesOrders;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class CustomerRepository(AppDbContext db) : ICustomerRepository
{
    public async Task<Customer?> GetByCustKeyAsync(string custKey) =>
        await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustKey == custKey);

    public async Task<Dictionary<string, Customer>> GetByCustKeysAsync(IEnumerable<string> custKeys)
    {
        var keys = custKeys.Distinct().ToList();
        return await db.Customers.AsNoTracking()
            .Where(c => keys.Contains(c.CustKey))
            .ToDictionaryAsync(c => c.CustKey);
    }

    // Each whitespace-separated keyword must match somewhere in CustKey/CusName,
    // in any order (e.g. "Puregold Isabela" matches "Puregold Price Club - Isabela
    // St. Manila"). This needs a leading-wildcard LIKE per token, which can't use
    // the CustKey/CusName indexes — acceptable because the frontend now debounces
    // 1s before calling this, instead of on every keystroke.
    public async Task<IEnumerable<CustomerSuggestionDto>> SearchAsync(string term, int take = 50)
    {
        var tokens = term.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var query = db.Customers.AsNoTracking();
        foreach (var token in tokens)
            query = query.Where(c => c.CustKey.Contains(token) || c.CusName.Contains(token));

        return await query
            .OrderByDescending(c => c.CustKey.StartsWith(term))
            .ThenBy(c => c.CusName)
            .Select(c => new CustomerSuggestionDto { CustKey = c.CustKey, CusName = c.CusName })
            .Take(take)
            .ToListAsync();
    }
}
