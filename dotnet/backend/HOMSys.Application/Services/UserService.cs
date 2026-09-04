using System.Security.Claims;
using HOMSys.Application.DTOs.Users;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace HOMSys.Application.Services;

public class UserService(IUserRepository userRepo, IRoleRepository roleRepo, IHttpContextAccessor http)
{
    private string CurrentUser =>
        http.HttpContext?.User?.FindFirstValue(ClaimTypes.Name) ?? "system";

    public async Task<IEnumerable<UserDto>> GetAllAsync()
    {
        var users = await userRepo.GetAllAsync();
        return users.Select(MapToDto);
    }

    public async Task<UserDto?> GetByIdAsync(int id)
    {
        var user = await userRepo.GetByIdAsync(id);
        return user is null ? null : MapToDto(user);
    }

    public async Task<(UserDto? user, string? error)> CreateAsync(CreateUserDto dto)
    {
        if (await userRepo.ExistsAsync(dto.Username, dto.Email))
            return (null, "Username or email already exists.");

        var roles = new List<Role>();
        foreach (var rid in dto.RoleIds)
        {
            var role = await roleRepo.GetByIdAsync(rid);
            if (role is null) return (null, $"Role {rid} not found.");
            roles.Add(role);
        }

        var user = new User
        {
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            CompanyId = dto.CompanyId,
            DepartmentId = dto.DepartmentId,
            SiteId = dto.SiteId,
            BranchCode = dto.BranchCode,
            IsActive = true,
            MustChangePassword = true,
            CreatedBy = CurrentUser,
            UserRoles = roles.Select(r => new UserRole { RoleId = r.Id, Role = r }).ToList()
        };

        var created = await userRepo.CreateAsync(user);
        return (MapToDto(created), null);
    }

    public async Task<(UserDto? user, string? error)> UpdateAsync(int id, UpdateUserDto dto)
    {
        var user = await userRepo.GetByIdAsync(id);
        if (user is null) return (null, "User not found.");

        if (await userRepo.ExistsAsync(string.Empty, dto.Email, id))
            return (null, "Email already in use by another user.");

        user.Email = dto.Email;
        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.CompanyId = dto.CompanyId;
        user.DepartmentId = dto.DepartmentId;
        user.SiteId = dto.SiteId;
        user.BranchCode = dto.BranchCode;
        user.IsActive = dto.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = CurrentUser;

        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.MustChangePassword = false;
        }

        var roles = new List<Role>();
        foreach (var rid in dto.RoleIds)
        {
            var role = await roleRepo.GetByIdAsync(rid);
            if (role is null) return (null, $"Role {rid} not found.");
            roles.Add(role);
        }

        user.UserRoles = roles.Select(r => new UserRole { UserId = id, RoleId = r.Id, Role = r }).ToList();

        await userRepo.UpdateAsync(user);
        return (MapToDto(user), null);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var user = await userRepo.GetByIdAsync(id);
        if (user is null) return false;
        await userRepo.DeleteAsync(id);
        return true;
    }

    private static UserDto MapToDto(User u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        Email = u.Email,
        FirstName = u.FirstName,
        LastName = u.LastName,
        CompanyId = u.CompanyId,
        CompanyName = u.Company?.Name,
        DepartmentId = u.DepartmentId,
        DepartmentName = u.Department?.Name,
        SiteId = u.SiteId,
        SiteName = u.Site?.Name,
        BranchCode = u.BranchCode,
        IsActive = u.IsActive,
        MustChangePassword = u.MustChangePassword,
        CreatedAt = u.CreatedAt,
        CreatedBy = u.CreatedBy,
        UpdatedAt = u.UpdatedAt,
        UpdatedBy = u.UpdatedBy,
        Roles = u.UserRoles.Select(ur => ur.Role?.Name ?? string.Empty).Where(n => n != string.Empty),
        RoleIds = u.UserRoles.Select(ur => ur.RoleId)
    };
}
