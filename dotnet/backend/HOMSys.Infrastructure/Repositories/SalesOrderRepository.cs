using HOMSys.Application.Interfaces;
using HOMSys.Application.Services;
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
            .Include(o => o.Rfcs.OrderBy(r => r.RfcNo)).ThenInclude(r => r.Lines)
            .AsSplitQuery()
            .AsNoTracking()
            .OrderByDescending(o => o.SoId)
            .ToListAsync();

    public async Task<SalesOrder?> GetByIdAsync(int soId) =>
        await db.SalesOrders
            .Include(o => o.Lines.OrderBy(l => l.LineNo))
            .Include(o => o.OosSyncLines)
            .Include(o => o.Rfcs.OrderBy(r => r.RfcNo)).ThenInclude(r => r.Lines)
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

    public async Task<SalesOrder?> GetForUpdateBySoNoAsync(int soNo, string branch) =>
        await db.SalesOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.SoNo == soNo && o.Branch == branch);

    public async Task<SalesOrder?> GetForUpdateByInvNoAsync(int invNo, string branch) =>
        await db.SalesOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => (o.InvNo == invNo || o.CancelledInvNo == invNo) && o.Branch == branch);

    public async Task<IEnumerable<SalesOrder>> GetReconcileCandidatesAsync(string branch, DateOnly since) =>
        await db.SalesOrders.AsNoTracking()
            .Include(o => o.Rfcs)
            .Where(o => o.Branch == branch && o.SoNo != null && o.OrderDate >= since
                        && o.WorkflowStatus != "Cancelled" && o.WorkflowStatus != SalesOrderBridgeService.FullRfc
                        // Handed off to this branch but not in its BMS yet — nothing to reconcile.
                        && !(o.ForBranch != null && o.OffshoreUploadedAt != null && o.OffshoreReceivedAt == null))
            .OrderBy(o => o.SoNo)
            .ToListAsync();

    public async Task<IEnumerable<SalesOrder>> GetOffshoreAwaitingAsync(string branch) =>
        await db.SalesOrders.AsNoTracking()
            .Where(o => o.Branch == branch && o.ForBranch != null && o.SoNo != null
                        && o.OffshoreUploadedAt == null && o.WorkflowStatus != "Cancelled")
            .OrderBy(o => o.SoNo)
            .ToListAsync();

    public async Task<IEnumerable<SalesOrder>> GetOffshoreInboundAsync(string branch) =>
        await db.SalesOrders.AsNoTracking()
            .Include(o => o.OffshoreTransfer)
            .Where(o => o.Branch == branch && o.ForBranch != null
                        && o.OffshoreUploadedAt != null && o.OffshoreReceivedAt == null)
            .OrderBy(o => o.SoNo)
            .ToListAsync();

    public async Task<SalesOrder?> GetOffshoreForUpdateAsync(int soNo, string originBranch) =>
        await db.SalesOrders
            .Where(o => o.SoNo == soNo && o.OriginBranch == originBranch && o.ForBranch != null
                        && (o.Branch == originBranch || o.OffshoreUploadedAt != null))
            .OrderByDescending(o => o.SoId)
            .FirstOrDefaultAsync();

    public Task<bool> SoNoTakenAsync(int soNo, string branch, int excludeSoId) =>
        db.SalesOrders.AnyAsync(o => o.SoNo == soNo && o.Branch == branch && o.SoId != excludeSoId);

    public async Task<SalesOrder?> GetForRfcSyncAsync(int? soNo, int invNo, string branch)
    {
        IQueryable<SalesOrder> q = db.SalesOrders
            .Include(o => o.Lines)
            .Include(o => o.OosSyncLines)
            .Include(o => o.Rfcs).ThenInclude(r => r.Lines)
            .AsSplitQuery()
            .Where(o => o.Branch == branch);
        // An SO# match only counts if it agrees with the RFC's invoice (or HOMSys
        // hasn't got the invoice yet) — guards against a reused/stale SO#.
        if (soNo is > 0)
        {
            var bySoNo = await q.FirstOrDefaultAsync(o => o.SoNo == soNo
                && (o.InvNo == null || o.InvNo == invNo || o.CancelledInvNo == invNo));
            if (bySoNo is not null)
                return bySoNo;
        }
        return await q.FirstOrDefaultAsync(o => o.InvNo == invNo || o.CancelledInvNo == invNo);
    }

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
