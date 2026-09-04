using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class PermissionRepository(AppDbContext db) : IPermissionRepository
{
    public async Task<IEnumerable<Permission>> GetAllAsync() =>
        await db.Permissions.OrderBy(p => p.Name).ToListAsync();

    public async Task<IEnumerable<Permission>> GetByRoleIdAsync(int roleId) =>
        await db.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission)
            .ToListAsync();

    public async Task SetRolePermissionsAsync(int roleId, IEnumerable<int> permissionIds)
    {
        var existing = await db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync();
        db.RolePermissions.RemoveRange(existing);

        var newEntries = permissionIds.Select(pid => new RolePermission { RoleId = roleId, PermissionId = pid });
        db.RolePermissions.AddRange(newEntries);

        await db.SaveChangesAsync();
    }
}
