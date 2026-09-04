using System.Security.Claims;
using HOMSys.Application.DTOs.Departments;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace HOMSys.Application.Services;

public class DepartmentService(IDepartmentRepository deptRepo, IHttpContextAccessor http)
{
    private string CurrentUser =>
        http.HttpContext?.User?.FindFirstValue(ClaimTypes.Name) ?? "system";

    public async Task<IEnumerable<DepartmentDto>> GetAllAsync()
    {
        var departments = await deptRepo.GetAllAsync();
        return departments.Select(MapToDto);
    }

    public async Task<DepartmentDto?> GetByIdAsync(int id)
    {
        var dept = await deptRepo.GetByIdAsync(id);
        return dept is null ? null : MapToDto(dept);
    }

    public async Task<(DepartmentDto? department, string? error)> CreateAsync(CreateDepartmentDto dto)
    {
        if (await deptRepo.ExistsAsync(dto.Name, dto.Code, dto.CompanyId))
            return (null, "A department with that name or code already exists in this company.");

        var dept = new Department
        {
            Name        = dto.Name,
            Code        = dto.Code,
            Description = dto.Description,
            CompanyId   = dto.CompanyId,
            IsActive    = true,
            CreatedBy   = CurrentUser
        };

        var created = await deptRepo.CreateAsync(dept);
        return (MapToDto(created), null);
    }

    public async Task<(DepartmentDto? department, string? error)> UpdateAsync(int id, UpdateDepartmentDto dto)
    {
        var dept = await deptRepo.GetByIdAsync(id);
        if (dept is null) return (null, "Department not found.");

        if (await deptRepo.ExistsAsync(dto.Name, dto.Code, dto.CompanyId, id))
            return (null, "A department with that name or code already exists in this company.");

        dept.Name        = dto.Name;
        dept.Code        = dto.Code;
        dept.Description = dto.Description;
        dept.CompanyId   = dto.CompanyId;
        dept.IsActive    = dto.IsActive;
        dept.UpdatedAt   = DateTime.UtcNow;
        dept.UpdatedBy   = CurrentUser;

        await deptRepo.UpdateAsync(dept);
        return (MapToDto(dept), null);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var dept = await deptRepo.GetByIdAsync(id);
        if (dept is null) return false;
        await deptRepo.DeleteAsync(id);
        return true;
    }

    private static DepartmentDto MapToDto(Department d) => new()
    {
        Id          = d.Id,
        Name        = d.Name,
        Code        = d.Code,
        Description = d.Description,
        IsActive    = d.IsActive,
        CompanyId   = d.CompanyId,
        CompanyName = d.Company?.Name ?? string.Empty,
        CreatedAt   = d.CreatedAt,
        CreatedBy   = d.CreatedBy,
        UpdatedAt   = d.UpdatedAt,
        UpdatedBy   = d.UpdatedBy
    };
}
