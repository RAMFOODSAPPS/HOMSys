using HOMSys.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Data;

public static class SyncLogExtensions
{
    public static async Task RecordSyncAsync(this AppDbContext db, string section)
    {
        var row = await db.SyncLogs.FirstOrDefaultAsync(s => s.Section == section);
        if (row is null)
            db.SyncLogs.Add(new SyncLog { Section = section, LastSyncedUtc = DateTime.UtcNow });
        else
            row.LastSyncedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
