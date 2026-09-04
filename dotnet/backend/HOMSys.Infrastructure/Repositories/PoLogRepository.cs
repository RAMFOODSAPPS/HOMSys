using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class PoLogRepository(AppDbContext db) : IPoLogRepository
{
    public async Task<bool> ExistsAsync(string poNum) =>
        await db.PoLogs.AsNoTracking().AnyAsync(p => p.PoNum == poNum);

    public async Task<PoLog?> GetLatestAsync(string poNum) =>
        await db.PoLogs.AsNoTracking()
            .Where(p => p.PoNum == poNum)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

    public async Task AddAsync(PoLog entry)
    {
        db.PoLogs.Add(entry);
        await db.SaveChangesAsync();
    }

    public async Task SetSoNoBySoIdAsync(int soId, int soNo)
    {
        await db.PoLogs
            .Where(p => p.SoId == soId && p.SoNo == null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.SoNo, soNo));
    }
}
