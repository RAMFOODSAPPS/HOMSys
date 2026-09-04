using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class SalesOrderRepository(AppDbContext db) : ISalesOrderRepository
{
    public async Task<IEnumerable<SalesOrder>> GetAllAsync() =>
        await db.SalesOrders
            .Include(o => o.Lines.OrderBy(l => l.LineNo))
            .Include(o => o.OosSyncLines)
            .AsSplitQuery()
            .AsNoTracking()
            .OrderByDescending(o => o.SoId)
            .ToListAsync();

    public async Task<SalesOrder?> GetByIdAsync(int soId) =>
        await db.SalesOrders
            .Include(o => o.Lines.OrderBy(l => l.LineNo))
            .Include(o => o.OosSyncLines)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.SoId == soId);

    public async Task<SalesOrder?> GetForUpdateAsync(int soId) =>
        await db.SalesOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.SoId == soId);

    public async Task<IEnumerable<SalesOrder>> GetPendingBridgeAsync(string branch) =>
        await db.SalesOrders
            .Include(o => o.Lines.OrderBy(l => l.LineNo))
            .AsNoTracking()
            .Where(o => o.SoNo == null && o.Branch == branch)
            .OrderBy(o => o.SoId)
            .ToListAsync();

    public async Task<IEnumerable<SalesOrder>> GetResyncPendingAsync(string branch) =>
        await db.SalesOrders
            .Include(o => o.Lines.OrderBy(l => l.LineNo))
            .AsNoTracking()
            .Where(o => o.SoNo != null && o.NeedsResync && o.Branch == branch)
            .OrderBy(o => o.SoId)
            .ToListAsync();

    public async Task<SalesOrder?> FindByFileHashAsync(string fileHash) =>
        await db.SalesOrders.AsNoTracking()
            .Where(o => o.SourceFileHash == fileHash)
            .OrderBy(o => o.CreatedAt)
            .FirstOrDefaultAsync();

    public async Task<IEnumerable<SalesOrder>> FindByPoNumsAsync(IEnumerable<string> poNums)
    {
        var set = poNums.ToList();
        return await db.SalesOrders.AsNoTracking()
            .Where(o => set.Contains(o.PoNum))
            .ToListAsync();
    }

    public async Task<SalesOrder> CreateAsync(SalesOrder order)
    {
        db.SalesOrders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    public Task SaveChangesAsync() => db.SaveChangesAsync();

    public async Task DeleteAsync(int soId)
    {
        var order = await db.SalesOrders.FindAsync(soId);
        if (order is not null)
        {
            db.SalesOrders.Remove(order);
            await db.SaveChangesAsync();
        }
    }
}
