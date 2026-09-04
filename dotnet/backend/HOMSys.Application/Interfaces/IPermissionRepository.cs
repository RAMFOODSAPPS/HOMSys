using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

public interface IPermissionRepository
{
    Task<IEnumerable<Permission>> GetAllAsync();
    Task<IEnumerable<Permission>> GetByRoleIdAsync(int roleId);
    Task SetRolePermissionsAsync(int roleId, IEnumerable<int> permissionIds);
}
