using System.Security.Claims;
using HOMSys.Application.DTOs.SiteTypes;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace HOMSys.Application.Services;

public class SiteTypeService(ISiteTypeRepository siteTypeRepo, IHttpContextAccessor http)
{
    private string CurrentUser =>
        http.HttpContext?.User?.FindFirstValue(ClaimTypes.Name) ?? "system";

    public async Task<IEnumerable<SiteTypeDto>> GetAllAsync()
    {
        var siteTypes = await siteTypeRepo.GetAllAsync();
        return siteTypes.Select(MapToDto);
    }

    public async Task<SiteTypeDto?> GetByIdAsync(int id)
    {
        var siteType = await siteTypeRepo.GetByIdAsync(id);
        return siteType is null ? null : MapToDto(siteType);
    }

    public async Task<(SiteTypeDto? siteType, string? error)> CreateAsync(CreateSiteTypeDto dto)
    {
        if (await siteTypeRepo.ExistsAsync(dto.Name, dto.Code))
            return (null, "A site type with that name or code already exists.");

        var siteType = new SiteType
        {
            Name        = dto.Name,
            Code        = dto.Code,
            Description = dto.Description,
            IsActive    = true,
            CreatedBy   = CurrentUser
        };

        var created = await siteTypeRepo.CreateAsync(siteType);
        return (MapToDto(created), null);
    }

    public async Task<(SiteTypeDto? siteType, string? error)> UpdateAsync(int id, UpdateSiteTypeDto dto)
    {
        var siteType = await siteTypeRepo.GetByIdAsync(id);
        if (siteType is null) return (null, "Site type not found.");

        if (await siteTypeRepo.ExistsAsync(dto.Name, dto.Code, id))
            return (null, "A site type with that name or code already exists.");

        siteType.Name        = dto.Name;
        siteType.Code        = dto.Code;
        siteType.Description = dto.Description;
        siteType.IsActive    = dto.IsActive;
        siteType.UpdatedAt   = DateTime.UtcNow;
        siteType.UpdatedBy   = CurrentUser;

        await siteTypeRepo.UpdateAsync(siteType);
        return (MapToDto(siteType), null);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var siteType = await siteTypeRepo.GetByIdAsync(id);
        if (siteType is null) return false;
        await siteTypeRepo.DeleteAsync(id);
        return true;
    }

    private static SiteTypeDto MapToDto(SiteType st) => new()
    {
        Id          = st.Id,
        Name        = st.Name,
        Code        = st.Code,
        Description = st.Description,
        IsActive    = st.IsActive,
        CreatedAt   = st.CreatedAt,
        CreatedBy   = st.CreatedBy,
        UpdatedAt   = st.UpdatedAt,
        UpdatedBy   = st.UpdatedBy
    };
}
