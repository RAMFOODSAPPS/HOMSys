using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class SiteTypeRepository(AppDbContext db) : ISiteTypeRepository
{
    public async Task<IEnumerable<SiteType>> GetAllAsync() =>
        await db.SiteTypes
            .OrderBy(st => st.Name)
            .ToListAsync();

    public async Task<SiteType?> GetByIdAsync(int id) =>
        await db.SiteTypes.FindAsync(id);

    public async Task<SiteType> CreateAsync(SiteType siteType)
    {
        db.SiteTypes.Add(siteType);
        await db.SaveChangesAsync();
        return siteType;
    }

    public async Task UpdateAsync(SiteType siteType)
    {
        db.ChangeTracker.Clear();
        await db.SiteTypes.Where(st => st.Id == siteType.Id).ExecuteUpdateAsync(s => s
            .SetProperty(st => st.Name,        siteType.Name)
            .SetProperty(st => st.Code,        siteType.Code)
            .SetProperty(st => st.Description, siteType.Description)
            .SetProperty(st => st.IsActive,    siteType.IsActive)
            .SetProperty(st => st.UpdatedAt,   siteType.UpdatedAt));
    }

    public async Task DeleteAsync(int id)
    {
        var siteType = await db.SiteTypes.FindAsync(id);
        if (siteType is not null)
        {
            db.SiteTypes.Remove(siteType);
            await db.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(string name, string code, int? excludeId = null)
    {
        var query = db.SiteTypes.AsQueryable();
        if (excludeId.HasValue)
            query = query.Where(st => st.Id != excludeId.Value);

        return await query.AnyAsync(st =>
            st.Name == name ||
            (!string.IsNullOrEmpty(code) && st.Code == code));
    }
}
