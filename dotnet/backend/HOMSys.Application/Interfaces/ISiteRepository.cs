using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

public interface ISiteRepository
{
    Task<IEnumerable<Site>> GetAllAsync();
    Task<Site?> GetByIdAsync(int id);
    Task<Site> CreateAsync(Site site);
    Task UpdateAsync(Site site);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(string name, string code, int companyId, int? excludeId = null);
}
