using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class TasafRepository(AppDbContext db) : ITasafRepository
{
    private IQueryable<Tasaf> WithLookups(IQueryable<Tasaf> q) => q
        .Include(t => t.SalesGroup)
        .Include(t => t.Area)
        .Include(t => t.Chain)
        .Include(t => t.ProgramType)
        .Include(t => t.Objective)
        .Include(t => t.TargetMarket)
        .Include(t => t.Channel)
        .Include(t => t.ProductGroup)
        .Include(t => t.ProductGroup2)
        .Include(t => t.Category)
        .Include(t => t.Category2);

    public async Task<List<Tasaf>> GetListAsync(int? visibleToUserId)
    {
        var q = db.Tasafs.AsNoTracking()
            .Include(t => t.SalesGroup)
            .Include(t => t.Chain)
            .Include(t => t.ProgramType)
            .Include(t => t.Steps)
            .AsSplitQuery()
            .AsQueryable();
        if (visibleToUserId is int uid)
            q = q.Where(t => t.ProponentUserId == uid || t.Steps.Any(s => s.UserId == uid));

        return await q.OrderByDescending(t => t.Id).ToListAsync();
    }

    public async Task<Tasaf?> GetByIdAsync(int id) =>
        await WithLookups(db.Tasafs)
            .Include(t => t.Branches.OrderBy(b => b.LineNo))
            .Include(t => t.Products.OrderBy(p => p.LineNo))
            .Include(t => t.Costs.OrderBy(c => c.LineNo)).ThenInclude(c => c.CostType)
            .Include(t => t.Actions.OrderBy(a => a.Id))
            .Include(t => t.Steps.OrderBy(s => s.Seq))
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id);

    // Both clear the tracker so a following GetByIdAsync re-reads the lookup
    // navigations for the (possibly changed) FK ids instead of the stale ones.
    public async Task CreateAsync(Tasaf tasaf)
    {
        db.Tasafs.Add(tasaf);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    public async Task UpdateAsync(Tasaf tasaf)
    {
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    public async Task SubmitAsync(Tasaf tasaf, string refNoPrefix)
    {
        var assign = tasaf.RefNo is null;
        for (var attempt = 1; ; attempt++)
        {
            if (assign)
            {
                // Proposal number is ONE continuous series across every TASAF (all years,
                // sales groups and proponents) — the number after the last "-". Never resets,
                // no 99 cap (01..99, then 100, 101, ...).
                var taken = await db.Tasafs.AsNoTracking()
                    .Where(t => t.RefNo != null)
                    .Select(t => t.RefNo!)
                    .ToListAsync();
                var next = taken
                    .Select(r => int.TryParse(r[(r.LastIndexOf('-') + 1)..], out var n) ? n : 0)
                    .DefaultIfEmpty(0).Max() + 1;
                tasaf.RefNo = $"{refNoPrefix}{next:00}";
            }
            try
            {
                await db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException) when (assign && attempt < 5)
            {
                // Unique RefNo index: another submit took this number first — take the next one.
            }
        }
        db.ChangeTracker.Clear();
    }

    public async Task DeleteAsync(Tasaf tasaf)
    {
        db.Tasafs.Remove(tasaf);
        await db.SaveChangesAsync();
    }
}
