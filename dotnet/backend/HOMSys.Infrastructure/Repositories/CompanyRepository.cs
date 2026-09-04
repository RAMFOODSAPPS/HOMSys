using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class CompanyRepository(AppDbContext db) : ICompanyRepository
{
    public async Task<IEnumerable<Company>> GetAllAsync() =>
        await db.Companies.OrderBy(c => c.Name).ToListAsync();

    public async Task<Company?> GetByIdAsync(int id) =>
        await db.Companies.FirstOrDefaultAsync(c => c.Id == id);

    public async Task<Company> CreateAsync(Company company)
    {
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company;
    }

    public async Task UpdateAsync(Company company)
    {
        db.ChangeTracker.Clear();
        await db.Companies.Where(c => c.Id == company.Id).ExecuteUpdateAsync(s => s
            .SetProperty(c => c.Name,          company.Name)
            .SetProperty(c => c.Code,          company.Code)
            .SetProperty(c => c.Address,       company.Address)
            .SetProperty(c => c.Phone,         company.Phone)
            .SetProperty(c => c.Email,         company.Email)
            .SetProperty(c => c.ContactPerson, company.ContactPerson)
            .SetProperty(c => c.IsActive,      company.IsActive)
            .SetProperty(c => c.UpdatedAt,     company.UpdatedAt));
    }

    public async Task DeleteAsync(int id)
    {
        var company = await db.Companies.FindAsync(id);
        if (company is not null)
        {
            db.Companies.Remove(company);
            await db.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(string name, string code, int? excludeId = null)
    {
        var query = db.Companies.AsQueryable();
        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);

        return await query.AnyAsync(c =>
            c.Name == name ||
            (!string.IsNullOrEmpty(code) && c.Code == code));
    }
}
