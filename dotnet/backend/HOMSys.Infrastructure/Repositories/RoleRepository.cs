using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class RoleRepository(AppDbContext db) : IRoleRepository
{
    public async Task<IEnumerable<Role>> GetAllAsync() =>
        await db.Roles.OrderBy(r => r.Name).ToListAsync();

    public async Task<Role?> GetByIdAsync(int id) =>
        await db.Roles.FindAsync(id);

    public async Task<Role?> GetByNameAsync(string name) =>
        await db.Roles.FirstOrDefaultAsync(r => r.Name == name);

    public async Task<Role> CreateAsync(Role role)
    {
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role;
    }

    public async Task<Role?> UpdateAsync(Role role)
    {
        db.Roles.Update(role);
        await db.SaveChangesAsync();
        return role;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var role = await db.Roles.FindAsync(id);
        if (role is null) return false;
        db.Roles.Remove(role);
        await db.SaveChangesAsync();
        return true;
    }
}
