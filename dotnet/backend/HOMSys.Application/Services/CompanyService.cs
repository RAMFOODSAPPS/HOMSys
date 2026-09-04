using System.Security.Claims;
using HOMSys.Application.DTOs.Companies;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace HOMSys.Application.Services;

public class CompanyService(ICompanyRepository companyRepo, IHttpContextAccessor http)
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
        CreatedAt     = c.CreatedAt,
        CreatedBy     = c.CreatedBy,
        UpdatedAt     = c.UpdatedAt,
        UpdatedBy     = c.UpdatedBy
    };
}
