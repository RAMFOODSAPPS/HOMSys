using System.Security.Claims;
using HOMSys.Application.DTOs.Companies;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace HOMSys.Application.Services;

public class CompanyService(ICompanyRepository companyRepo, IUserRepository users, IFileStorage storage, IHttpContextAccessor http)
{
    private string CurrentUser =>
        http.HttpContext?.User?.FindFirstValue(ClaimTypes.Name) ?? "system";

    public async Task<IEnumerable<CompanyDto>> GetAllAsync()
    {
        var companies = await companyRepo.GetAllAsync();
        return companies.Select(MapToDto);
    }

    public async Task<CompanyDto?> GetByIdAsync(int id)
    {
        var company = await companyRepo.GetByIdAsync(id);
        return company is null ? null : MapToDto(company);
    }

    public async Task<(CompanyDto? company, string? error)> CreateAsync(CreateCompanyDto dto)
    {
        if (await companyRepo.ExistsAsync(dto.Name, dto.Code))
            return (null, "A company with that name or code already exists.");

        var company = new Company
        {
            Name          = dto.Name,
            Code          = dto.Code,
            Address       = dto.Address,
            Phone         = dto.Phone,
            Email         = dto.Email,
            ContactPerson = dto.ContactPerson,
            IsActive      = true,
            CreatedBy     = CurrentUser
        };

        var created = await companyRepo.CreateAsync(company);
        return (MapToDto(created), null);
    }

    public async Task<(CompanyDto? company, string? error)> UpdateAsync(int id, UpdateCompanyDto dto)
    {
        var company = await companyRepo.GetByIdAsync(id);
        if (company is null) return (null, "Company not found.");

        if (await companyRepo.ExistsAsync(dto.Name, dto.Code, id))
            return (null, "A company with that name or code already exists.");

        company.Name          = dto.Name;
        company.Code          = dto.Code;
        company.Address       = dto.Address;
        company.Phone         = dto.Phone;
        company.Email         = dto.Email ?? string.Empty;
        company.ContactPerson = dto.ContactPerson;
        company.IsActive      = dto.IsActive;
        company.UpdatedAt     = DateTime.UtcNow;
        company.UpdatedBy     = CurrentUser;

        await companyRepo.UpdateAsync(company);
        return (MapToDto(company), null);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var company = await companyRepo.GetByIdAsync(id);
        if (company is null) return false;
        await companyRepo.DeleteAsync(id);
        return true;
    }

    private static CompanyDto MapToDto(Company c) => new()
    {
        Id            = c.Id,
        Name          = c.Name,
        Code          = c.Code,
        Address       = c.Address,
        Phone         = c.Phone,
        Email         = c.Email,
        ContactPerson = c.ContactPerson,
        IsActive      = c.IsActive,
        HasLogo       = c.LogoBlob is not null,
        LogoSizeKb    = c.LogoSize is int b ? Math.Round(b / 1024m, 1) : null,
        LogoVersion   = c.LogoUpdatedAt?.Ticks,
        CreatedAt     = c.CreatedAt,
        CreatedBy     = c.CreatedBy,
        UpdatedAt     = c.UpdatedAt,
        UpdatedBy     = c.UpdatedBy
    };

    // ── Logo (Azure Blob Storage, container "company-logos") ──
    private const string LogoContainer = "company-logos";
    /// <summary>The browser resizes / compresses before upload; this is the server's hard cap.</summary>
    public const int MaxLogoBytes = 300 * 1024;

    /// <summary>Upload (replace) the company logo. PNG or JPEG only — checked by content, not just the name.</summary>
    public async Task<(CompanyDto? company, string? error)> SaveLogoAsync(int id, byte[] data)
    {
        if (!storage.IsConfigured) return (null, "File storage is not configured on the server.");
        var company = await companyRepo.GetByIdAsync(id);
        if (company is null) return (null, "Company not found.");
        if (data.Length == 0) return (null, "The file is empty.");
        if (data.Length > MaxLogoBytes) return (null, $"The logo is {data.Length / 1024m:N0} KB — the limit is {MaxLogoBytes / 1024} KB.");

        var (contentType, ext) = ImageType(data);
        if (contentType is null) return (null, "Only PNG or JPG images can be used as a logo.");

        var name = $"company-{id}{ext}";
        await storage.SaveAsync(LogoContainer, name, data, contentType);
        if (company.LogoBlob is not null && company.LogoBlob != name)
            await storage.DeleteAsync(LogoContainer, company.LogoBlob);

        company.LogoBlob        = name;
        company.LogoContentType = contentType;
        company.LogoSize        = data.Length;
        company.LogoUpdatedAt   = DateTime.UtcNow;
        company.UpdatedAt       = DateTime.UtcNow;
        company.UpdatedBy       = CurrentUser;
        await companyRepo.UpdateAsync(company);
        return (MapToDto(company), null);
    }

    public async Task<(CompanyDto? company, string? error)> RemoveLogoAsync(int id)
    {
        var company = await companyRepo.GetByIdAsync(id);
        if (company is null) return (null, "Company not found.");
        if (company.LogoBlob is not null && storage.IsConfigured)
            await storage.DeleteAsync(LogoContainer, company.LogoBlob);
        company.LogoBlob = company.LogoContentType = null;
        company.LogoSize = null;
        company.LogoUpdatedAt = null;
        company.UpdatedAt = DateTime.UtcNow;
        company.UpdatedBy = CurrentUser;
        await companyRepo.UpdateAsync(company);
        return (MapToDto(company), null);
    }

    /// <summary>The logo file, or null when the company has none.</summary>
    public async Task<(byte[] Data, string ContentType)?> GetLogoAsync(int id)
    {
        var company = await companyRepo.GetByIdAsync(id);
        if (company?.LogoBlob is null || !storage.IsConfigured) return null;
        return await storage.ReadAsync(LogoContainer, company.LogoBlob);
    }

    /// <summary>
    /// Logo for a document (TASAF print / PDF) as a data: URI — the user's company, else the first
    /// active company. Null when there is none (the document just has no logo).
    /// </summary>
    public async Task<string?> LogoDataUriForUserAsync(int userId)
    {
        var companyId = (await users.GetByIdAsync(userId))?.CompanyId
            ?? (await companyRepo.GetAllAsync()).Where(c => c.IsActive).OrderBy(c => c.Id).FirstOrDefault()?.Id;
        if (companyId is not int cid) return null;
        var company = await companyRepo.GetByIdAsync(cid);
        if (company?.LogoBlob is null || !storage.IsConfigured) return null;

        // Kept in memory per (company, upload time): a new upload changes the key, so it shows at once.
        var key = (cid, company.LogoUpdatedAt?.Ticks ?? 0);
        if (LogoCache.TryGetValue(key, out var cached)) return cached;
        try
        {
            var uri = await storage.ReadAsync(LogoContainer, company.LogoBlob) is var (data, type)
                ? $"data:{type};base64,{Convert.ToBase64String(data)}" : null;
            foreach (var old in LogoCache.Keys.Where(k => k.Item1 == cid)) LogoCache.TryRemove(old, out _);
            if (uri is not null) LogoCache[key] = uri;
            return uri;
        }
        catch { return null; } // a storage hiccup must never break printing
    }

    /// <summary>Logo data: URIs by (company, LogoUpdatedAt ticks) — a handful of small images.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(int, long), string> LogoCache = new();

    /// <summary>PNG / JPEG by magic bytes.</summary>
    private static (string? type, string ext) ImageType(byte[] d) =>
        d.Length > 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47 ? ("image/png", ".png")
        : d.Length > 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF ? ("image/jpeg", ".jpg")
        : (null, "");
}
