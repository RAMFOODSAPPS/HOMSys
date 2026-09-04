using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

public interface ISiteTypeRepository
{
    Task<IEnumerable<SiteType>> GetAllAsync();
    Task<SiteType?> GetByIdAsync(int id);
    Task<SiteType> CreateAsync(SiteType siteType);
    Task UpdateAsync(SiteType siteType);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(string name, string code, int? excludeId = null);
}
