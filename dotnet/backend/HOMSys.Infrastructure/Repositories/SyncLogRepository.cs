using HOMSys.Application.Interfaces;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class SyncLogRepository(AppDbContext db) : ISyncLogRepository
{
    public Task RecordAsync(string section) => db.RecordSyncAsync(section);

    public async Task<DateTime?> GetLastSyncedAsync(string section) =>
        (await db.SyncLogs.AsNoTracking().FirstOrDefaultAsync(s => s.Section == section))?.LastSyncedUtc;

    public async Task<IReadOnlyList<(string Section, DateTime LastSyncedUtc)>> GetByPrefixAsync(string prefix) =>
        (await db.SyncLogs.AsNoTracking()
            .Where(s => s.Section.StartsWith(prefix))
            .OrderBy(s => s.Section)
            .Select(s => new { s.Section, s.LastSyncedUtc })
            .ToListAsync())
        .Select(s => (s.Section, s.LastSyncedUtc))
        .ToList();
}
