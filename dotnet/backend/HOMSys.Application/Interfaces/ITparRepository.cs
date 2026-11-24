using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

/// <summary>TPARs entered against Approved TASAFs (TPAR page).</summary>
public interface ITparRepository
{
    /// <summary>Approved (and, when asked, Completed) TASAFs with their TPARs — the TPAR page's picker. No tracking.</summary>
    Task<List<Tasaf>> GetTasafsAsync(bool includeCompleted);
    /// <summary>TPARs with their TASAF, newest first; tasafId null = all. No tracking.</summary>
    Task<List<TasafTpar>> GetListAsync(int? tasafId);
    /// <summary>The TASAF a TPAR belongs to, or null when there is no such TPAR.</summary>
    Task<int?> GetTasafIdAsync(int tparId);
    /// <summary>Tracked TASAF with its TPARs (for adding / changing one).</summary>
    Task<Tasaf?> GetTasafAsync(int tasafId);
    /// <summary>Active Sites (branch options when a TASAF covers an ALL … scope). No tracking.</summary>
    Task<List<Site>> GetActiveSitesAsync();
    /// <summary>The user's servicing branches (Site ids; Users page). Empty = none set.</summary>
    Task<List<int>> GetServicingSiteIdsAsync(int userId);
    /// <summary>RefNo (or "#id") of the TASAF already using this TPAR No., else null.</summary>
    Task<string?> TparNoUsedOnAsync(string tparNo, int? exceptTparId);
    /// <summary>
    /// Runs the work in a transaction holding an update lock on the TASAF row, so two encoders adding TPARs
    /// to the same TASAF at once can't both pass the "total ≤ approved amount" check.
    /// </summary>
    Task<T> InTasafLockAsync<T>(int tasafId, Func<Task<T>> work);
    /// <summary>Saves; false when a TPAR No. is already used (unique index).</summary>
    Task<bool> SaveAsync();
}
