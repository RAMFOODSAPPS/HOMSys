using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

public interface IRoleRepository
{
    Task<IEnumerable<Role>> GetAllAsync();
    Task<Role?> GetByIdAsync(int id);
    Task<Role?> GetByNameAsync(string name);
    Task<Role> CreateAsync(Role role);
    Task<Role?> UpdateAsync(Role role);
    Task<bool> DeleteAsync(int id);
}
