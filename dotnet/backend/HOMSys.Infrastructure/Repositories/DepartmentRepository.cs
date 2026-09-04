using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class DepartmentRepository(AppDbContext db) : IDepartmentRepository
{
    public async Task<IEnumerable<Department>> GetAllAsync() =>
        await db.Departments
            .Include(d => d.Company)
            .OrderBy(d => d.Company.Name)
            .ThenBy(d => d.Name)
            .ToListAsync();

    public async Task<Department?> GetByIdAsync(int id) =>
        await db.Departments
            .Include(d => d.Company)
            .FirstOrDefaultAsync(d => d.Id == id);

    public async Task<Department> CreateAsync(Department department)
    {
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        await db.Entry(department).Reference(d => d.Company).LoadAsync();
        return department;
    }

    public async Task UpdateAsync(Department department)
    {
        db.ChangeTracker.Clear();
        await db.Departments.Where(d => d.Id == department.Id).ExecuteUpdateAsync(s => s
            .SetProperty(d => d.Name,        department.Name)
            .SetProperty(d => d.Code,        department.Code)
            .SetProperty(d => d.Description, department.Description)
            .SetProperty(d => d.CompanyId,   department.CompanyId)
            .SetProperty(d => d.IsActive,    department.IsActive)
            .SetProperty(d => d.UpdatedAt,   department.UpdatedAt));
    }

    public async Task DeleteAsync(int id)
    {
        var dept = await db.Departments.FindAsync(id);
        if (dept is not null)
        {
            db.Departments.Remove(dept);
            await db.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(string name, string code, int companyId, int? excludeId = null)
    {
        var query = db.Departments.Where(d => d.CompanyId == companyId).AsQueryable();
        if (excludeId.HasValue)
            query = query.Where(d => d.Id != excludeId.Value);

        return await query.AnyAsync(d =>
            d.Name == name ||
            (!string.IsNullOrEmpty(code) && d.Code == code));
    }
}
