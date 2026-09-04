using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class OosSyncRepository(AppDbContext db) : IOosSyncRepository
{
    public async Task ReplaceForOrderAsync(int soId, IEnumerable<OosSyncLine> lines)
    {
        var existing = await db.OosSyncLines.Where(l => l.SoId == soId).ToListAsync();
        db.OosSyncLines.RemoveRange(existing);
        await db.OosSyncLines.AddRangeAsync(lines);
        await db.SaveChangesAsync();
    }
}
