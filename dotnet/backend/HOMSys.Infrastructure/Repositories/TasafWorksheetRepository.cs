using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class TasafWorksheetRepository(AppDbContext db) : ITasafWorksheetRepository
{
    private IQueryable<TasafWorksheet> Full(IQueryable<TasafWorksheet> q) => q
        .Include(w => w.ListingFeeLines.OrderBy(l => l.LineNo))
        .Include(w => w.NearExLines.OrderBy(l => l.LineNo))
        .Include(w => w.Tasaf).ThenInclude(t => t!.Steps)
        .AsSplitQuery();

    public async Task<List<TasafWorksheet>> GetListAsync(int userId, bool allAttached) =>
        await Full(db.TasafWorksheets.AsNoTracking())
            .Where(w => w.ProponentUserId == userId
                     || (w.Tasaf != null && (allAttached || w.Tasaf.ProponentUserId == userId
                                             || w.Tasaf.Steps.Any(s => s.UserId == userId))))
            .OrderByDescending(w => w.Id)
            .ToListAsync();

    public async Task<TasafWorksheet?> GetByIdAsync(int id) =>
        await Full(db.TasafWorksheets).FirstOrDefaultAsync(w => w.Id == id);

    public async Task CreateAsync(TasafWorksheet worksheet)
    {
        db.TasafWorksheets.Add(worksheet);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    public async Task UpdateAsync(TasafWorksheet worksheet)
    {
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    public async Task DeleteAsync(TasafWorksheet worksheet)
    {
        db.TasafWorksheets.Remove(worksheet);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }
}
