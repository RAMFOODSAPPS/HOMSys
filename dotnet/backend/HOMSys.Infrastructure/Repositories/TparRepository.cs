using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class TparRepository(AppDbContext db) : ITparRepository
{
    public async Task<List<Tasaf>> GetTasafsAsync(bool includeCompleted) =>
        await db.Tasafs.AsNoTracking()
            .Where(t => t.Status == TasafStatus.Approved || (includeCompleted && t.Status == TasafStatus.Completed))
            .Include(t => t.SalesGroup)
            .Include(t => t.ProgramType)
            .Include(t => t.Tpars)
            .Include(t => t.Branches)
            .AsSplitQuery()
            .OrderByDescending(t => t.DecidedAt).ThenByDescending(t => t.Id)
            .ToListAsync();

    public async Task<List<TasafTpar>> GetListAsync(int? tasafId)
    {
        var q = db.TasafTpars.AsNoTracking().Include(p => p.Tasaf).AsQueryable();
        if (tasafId is int id) q = q.Where(p => p.TasafId == id);
        return await q.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).ToListAsync();
    }

    public async Task<int?> GetTasafIdAsync(int tparId) =>
        await db.TasafTpars.AsNoTracking().Where(p => p.Id == tparId).Select(p => (int?)p.TasafId).FirstOrDefaultAsync();

    public async Task<Tasaf?> GetTasafAsync(int tasafId) =>
        await db.Tasafs.Include(t => t.Tpars).Include(t => t.Branches).AsSplitQuery().FirstOrDefaultAsync(t => t.Id == tasafId);

    public async Task<List<int>> GetServicingSiteIdsAsync(int userId) =>
        await db.UserServicingBranches.AsNoTracking().Where(b => b.UserId == userId).Select(b => b.SiteId).ToListAsync();

    public async Task<List<Site>> GetActiveSitesAsync() =>
        await db.Sites.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();

    public async Task<string?> TparNoUsedOnAsync(string tparNo, int? exceptTparId) =>
        await db.TasafTpars.AsNoTracking()
            .Where(p => p.TparNo == tparNo && p.Id != (exceptTparId ?? 0) && p.Status != TasafTparStatus.Cancelled)
            .Select(p => p.Tasaf!.RefNo ?? "#" + p.TasafId)
            .FirstOrDefaultAsync();

    public async Task<T> InTasafLockAsync<T>(int tasafId, Func<Task<T>> work)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM Tasafs WITH (UPDLOCK, HOLDLOCK) WHERE Id = {tasafId}");
        var result = await work();
        await tx.CommitAsync();
        return result;
    }

    public async Task<bool> SaveAsync()
    {
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }
}
