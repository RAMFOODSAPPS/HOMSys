using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class CustomerIdentifierMapRepository(AppDbContext db) : ICustomerIdentifierMapRepository
{
    public async Task<List<CustomerIdentifierMap>> GetByIdentifiersAsync(IEnumerable<string> identifiers)
    {
        var keys = identifiers.Distinct().ToList();
        return await db.CustomerIdentifierMaps.AsNoTracking()
            .Where(m => keys.Contains(m.Identifier))
            .ToListAsync();
    }

    public async Task UpsertRangeAsync(IEnumerable<(string Identifier, string CustKey)> mappings, string user)
    {
        var list = mappings.ToList();
        var identifiers = list.Select(m => m.Identifier).Distinct().ToList();
        var existing = await db.CustomerIdentifierMaps
            .Where(m => identifiers.Contains(m.Identifier))
            .ToDictionaryAsync(m => m.Identifier);

        var now = DateTime.UtcNow;
        foreach (var (identifier, custKey) in list)
        {
            if (existing.TryGetValue(identifier, out var map))
            {
                map.CustKey = custKey;
                map.UpdatedAt = now;
                map.UpdatedBy = user;
            }
            else
            {
                db.CustomerIdentifierMaps.Add(new CustomerIdentifierMap
                {
                    Identifier = identifier,
                    CustKey = custKey,
                    CreatedAt = now,
                    CreatedBy = user
                });
            }
        }

        await db.SaveChangesAsync();
    }
}
