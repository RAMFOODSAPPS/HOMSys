using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class FormLayoutRepository(AppDbContext db) : IFormLayoutRepository
{
    public async Task<FormLayout?> GetAsync(string formKey) =>
        await db.FormLayouts.AsNoTracking().FirstOrDefaultAsync(x => x.FormKey == formKey);

    public async Task UpsertAsync(string formKey, string layoutJson, string updatedBy)
    {
        var row = await db.FormLayouts.FirstOrDefaultAsync(x => x.FormKey == formKey);
        if (row is null)
        {
            row = new FormLayout { FormKey = formKey };
            db.FormLayouts.Add(row);
        }
        row.LayoutJson = layoutJson;
        row.UpdatedAt  = DateTime.UtcNow;
        row.UpdatedBy  = updatedBy;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(string formKey) =>
        await db.FormLayouts.Where(x => x.FormKey == formKey).ExecuteDeleteAsync();
}
