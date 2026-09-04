using System.Security.Claims;
using HOMSys.Application.DTOs.Sites;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace HOMSys.Application.Services;

public class SiteService(ISiteRepository siteRepo, IHttpContextAccessor http)
{
    public async Task<IEnumerable<SiteDto>> GetAllAsync()
    {
        var sites = await siteRepo.GetAllAsync();
        return sites.Select(MapToDto);
    }

    public async Task<SiteDto?> GetByIdAsync(int id)
    {
        var site = await siteRepo.GetByIdAsync(id);
        return site is null ? null : MapToDto(site);
    }

    private string CurrentUser =>
        http.HttpContext?.User?.FindFirstValue(ClaimTypes.Name) ?? "system";

    public async Task<(SiteDto? site, string? error)> CreateAsync(CreateSiteDto dto)
    {
        if (await siteRepo.ExistsAsync(dto.Name, dto.Code, dto.CompanyId))
            return (null, "A site with that name or code already exists in this company.");

        var site = new Site
        {
            Name          = dto.Name,
            Code          = dto.Code,
            CompanyId     = dto.CompanyId,
            SiteTypeId    = dto.SiteTypeId,
            Address       = dto.Address,
            Phone         = dto.Phone,
            ContactPerson = dto.ContactPerson,
            Description   = dto.Description,
            IsActive      = true,
            CreatedBy     = CurrentUser
        };

        var created = await siteRepo.CreateAsync(site);
        return (MapToDto(created), null);
    }

    public async Task<(SiteDto? site, string? error)> UpdateAsync(int id, UpdateSiteDto dto)
    {
        var site = await siteRepo.GetByIdAsync(id);
        if (site is null) return (null, "Site not found.");

        if (await siteRepo.ExistsAsync(dto.Name, dto.Code, dto.CompanyId, id))
            return (null, "A site with that name or code already exists in this company.");

        site.Name          = dto.Name;
        site.Code          = dto.Code;
        site.CompanyId     = dto.CompanyId;
        site.SiteTypeId    = dto.SiteTypeId;
        site.Address       = dto.Address;
        site.Phone         = dto.Phone;
        site.ContactPerson = dto.ContactPerson;
        site.Description   = dto.Description;
        site.IsActive      = dto.IsActive;
        site.UpdatedAt     = DateTime.UtcNow;
        site.UpdatedBy     = CurrentUser;

        await siteRepo.UpdateAsync(site);
        return (MapToDto(site), null);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var site = await siteRepo.GetByIdAsync(id);
        if (site is null) return false;
        await siteRepo.DeleteAsync(id);
        return true;
    }

    private static SiteDto MapToDto(Site s) => new()
    {
        Id            = s.Id,
        Name          = s.Name,
        Code          = s.Code,
        CompanyId     = s.CompanyId,
        CompanyName   = s.Company?.Name ?? string.Empty,
        SiteTypeId    = s.SiteTypeId,
        SiteTypeName  = s.SiteType?.Name,
        Address       = s.Address,
        Phone         = s.Phone,
        ContactPerson = s.ContactPerson,
        Description   = s.Description,
        IsActive      = s.IsActive,
        CreatedAt     = s.CreatedAt,
        CreatedBy     = s.CreatedBy,
        UpdatedAt     = s.UpdatedAt,
        UpdatedBy     = s.UpdatedBy
    };
}
