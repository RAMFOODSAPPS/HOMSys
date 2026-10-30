using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

public interface ITasafWorksheetRepository
{
    /// <summary>
    /// Worksheets with lines and their TASAF (+ steps) visible to <paramref name="userId"/>: the ones they made, and
    /// attached ones whose TASAF they proposed or are an approver on — or every attached one when
    /// <paramref name="allAttached"/> (TASAF View All). An unattached worksheet is only ever its creator's.
    /// </summary>
    Task<List<TasafWorksheet>> GetListAsync(int userId, bool allAttached);
    /// <summary>Tracked, with lines and the TASAF (+ steps).</summary>
    Task<TasafWorksheet?> GetByIdAsync(int id);
    Task CreateAsync(TasafWorksheet worksheet);
    /// <summary>Saves the tracked worksheet (lines were replaced on it).</summary>
    Task UpdateAsync(TasafWorksheet worksheet);
    Task DeleteAsync(TasafWorksheet worksheet);
}
