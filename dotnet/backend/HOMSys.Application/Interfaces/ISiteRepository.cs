using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

public interface ISiteRepository
{
    Task<IEnumerable<Site>> GetAllAsync();
    Task<Site?> GetByIdAsync(int id);
    Task<Site> CreateAsync(Site site);
    Task UpdateAsync(Site site);
    Task DeleteAsync(int id);
    /// <summary>Site named by a User.BranchCode / bridge branch value: matches Site.Code as-is, else "{code minus -B}-B".</summary>
    Task<Site?> GetByBranchCodeAsync(string branchCode);
    Task SetBmsDateAsync(int siteId, DateOnly bmsDate);
    Task<bool> ExistsAsync(string name, string code, int companyId, int? excludeId = null);
}
