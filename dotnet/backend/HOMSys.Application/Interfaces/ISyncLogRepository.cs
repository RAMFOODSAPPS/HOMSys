namespace HOMSys.Application.Interfaces;

/// <summary>Per-section "last synced" timestamps (SyncLog table) — see SyncLogSections.</summary>
public interface ISyncLogRepository
{
    Task RecordAsync(string section);
    Task<DateTime?> GetLastSyncedAsync(string section);
    Task<IReadOnlyList<(string Section, DateTime LastSyncedUtc)>> GetByPrefixAsync(string prefix);
}
