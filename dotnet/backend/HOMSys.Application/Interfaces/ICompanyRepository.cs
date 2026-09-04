using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

public interface ICompanyRepository
{
    Task<IEnumerable<Company>> GetAllAsync();
    Task<Company?> GetByIdAsync(int id);
    Task<Company> CreateAsync(Company company);
    Task UpdateAsync(Company company);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(string name, string code, int? excludeId = null);
}
