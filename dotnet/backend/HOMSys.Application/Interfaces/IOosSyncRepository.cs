using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

public interface IOosSyncRepository
{
    /// <summary>
    /// Full-overwrite dump: replaces every OosSyncLine row for this SO with
    /// the given set (the bridge always posts every oowkdet line still
    /// present at sync time, so a prior line missing from the new set is
    /// simply dropped — its absence is what marks it as gone from oowkdet).
    /// </summary>
    Task ReplaceForOrderAsync(int soId, IEnumerable<OosSyncLine> lines);
}
