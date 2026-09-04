using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class SiteRepository(AppDbContext db) : ISiteRepository
{
    public async Task<IEnumerable<Site>> GetAllAsync() =>
        await db.Sites
            .Include(s => s.Company)
            .Include(s => s.SiteType)
            .OrderBy(s => s.Company.Name)
            .ThenBy(s => s.Name)
            .ToListAsync();

    public async Task<Site?> GetByIdAsync(int id) =>
        await db.Sites
            .Include(s => s.Company)
            .Include(s => s.SiteType)
            .FirstOrDefaultAsync(s => s.Id == id);

    public async Task<Site> CreateAsync(Site site)
    {
        db.Sites.Add(site);
        await db.SaveChangesAsync();
        await db.Entry(site).Reference(s => s.Company).LoadAsync();
        if (site.SiteTypeId.HasValue)
            await db.Entry(site).Reference(s => s.SiteType).LoadAsync();
        return site;
    }

    public async Task UpdateAsync(Site site)
    {
        db.ChangeTracker.Clear();
        await db.Sites.Where(s => s.Id == site.Id).ExecuteUpdateAsync(x => x
            .SetProperty(s => s.Name,          site.Name)
            .SetProperty(s => s.Code,          site.Code)
            .SetProperty(s => s.CompanyId,     site.CompanyId)
            .SetProperty(s => s.SiteTypeId,    site.SiteTypeId)
            .SetProperty(s => s.Address,       site.Address)
            .SetProperty(s => s.Phone,         site.Phone)
            .SetProperty(s => s.ContactPerson, site.ContactPerson)
            .SetProperty(s => s.Description,   site.Description)
            .SetProperty(s => s.IsActive,      site.IsActive)
            .SetProperty(s => s.UpdatedAt,     site.UpdatedAt)
            .SetProperty(s => s.UpdatedBy,     site.UpdatedBy));
    }

    public async Task DeleteAsync(int id)
    {
        var site = await db.Sites.FindAsync(id);
        if (site is not null)
        {
            db.Sites.Remove(site);
            await db.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(string name, string code, int companyId, int? excludeId = null)
    {
        var query = db.Sites.Where(s => s.CompanyId == companyId).AsQueryable();
        if (excludeId.HasValue)
            query = query.Where(s => s.Id != excludeId.Value);

        return await query.AnyAsync(s =>
            s.Name == name ||
            (!string.IsNullOrEmpty(code) && s.Code == code));
    }
}
