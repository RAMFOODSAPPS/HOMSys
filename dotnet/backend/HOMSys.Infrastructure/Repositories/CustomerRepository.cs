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

    // StartsWith (not Contains) so both branches can use the CustKey/CusName
    // indexes — a leading-wildcard LIKE '%term%' over 149k customers can't.
    public async Task<IEnumerable<CustomerSuggestionDto>> SearchAsync(string term, int take = 50) =>
        await db.Customers.AsNoTracking()
            .Where(c => c.CustKey.StartsWith(term) || c.CusName.StartsWith(term))
            .OrderByDescending(c => c.CustKey.StartsWith(term))
            .ThenBy(c => c.CusName)
            .Select(c => new CustomerSuggestionDto { CustKey = c.CustKey, CusName = c.CusName })
            .Take(take)
            .ToListAsync();
}
