using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public async Task<User?> GetByIdAsync(int id) =>
        await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .Include(u => u.Company)
            .Include(u => u.Department)
            .Include(u => u.Site)
            .FirstOrDefaultAsync(u => u.Id == id);

    public async Task<User?> GetByUsernameAsync(string username) =>
        await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .Include(u => u.Company)
            .Include(u => u.Department)
            .Include(u => u.Site)
            .FirstOrDefaultAsync(u => u.Username == username);

    public async Task<User?> GetByEmailAsync(string email) =>
        await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .Include(u => u.Company)
            .Include(u => u.Department)
            .Include(u => u.Site)
            .FirstOrDefaultAsync(u => u.Email == email);

    public async Task<IEnumerable<User>> GetAllAsync() =>
        await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.Company)
            .Include(u => u.Department)
            .Include(u => u.Site)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync();

    public async Task<User> CreateAsync(User user)
    {
        user.UserRoles = user.UserRoles.Select(ur => new UserRole { RoleId = ur.RoleId }).ToList();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (await GetByIdAsync(user.Id))!;
    }

    public async Task UpdateAsync(User user)
    {
        var newRoleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();

        // Clear tracked entities to avoid EF identity-tracking conflicts —
        // GetByIdAsync already loaded this user into the context, so a second
        // FirstOrDefaultAsync would return the same (already-mutated) instance.
        db.ChangeTracker.Clear();

        await db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s
            .SetProperty(u => u.Email, user.Email)
            .SetProperty(u => u.FirstName, user.FirstName)
            .SetProperty(u => u.LastName, user.LastName)
            .SetProperty(u => u.CompanyId, user.CompanyId)
            .SetProperty(u => u.DepartmentId, user.DepartmentId)
            .SetProperty(u => u.SiteId, user.SiteId)
            .SetProperty(u => u.IsActive, user.IsActive)
            .SetProperty(u => u.PasswordHash, user.PasswordHash)
            .SetProperty(u => u.MustChangePassword, user.MustChangePassword)
            .SetProperty(u => u.UpdatedAt, user.UpdatedAt));

        await db.UserRoles.Where(ur => ur.UserId == user.Id).ExecuteDeleteAsync();
        await db.UserRoles.AddRangeAsync(newRoleIds.Select(rid => new UserRole { UserId = user.Id, RoleId = rid }));
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is not null)
        {
            db.Users.Remove(user);
            await db.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(string username, string email, int? excludeId = null)
    {
        var query = db.Users.AsQueryable();
        if (excludeId.HasValue)
            query = query.Where(u => u.Id != excludeId.Value);

        return await query.AnyAsync(u =>
            (!string.IsNullOrEmpty(username) && u.Username == username) ||
            (!string.IsNullOrEmpty(email) && u.Email == email));
    }
}
