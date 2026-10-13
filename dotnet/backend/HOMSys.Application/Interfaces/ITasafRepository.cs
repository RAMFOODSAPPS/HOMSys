using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

public interface ITasafRepository
{
    /// <summary>
    /// Header + lookup names + approval steps, no lines. visibleToUserId null = everyone's;
    /// otherwise the TASAFs that user proposed or is an approver on.
    /// </summary>
    Task<List<Tasaf>> GetListAsync(int? visibleToUserId);
    /// <summary>Tracked, with lines, history and lookups loaded.</summary>
    Task<Tasaf?> GetByIdAsync(int id);
    Task CreateAsync(Tasaf tasaf);
    /// <summary>Saves header changes; lines were replaced on the tracked entity. Re-read with GetByIdAsync after.</summary>
    Task UpdateAsync(Tasaf tasaf);
    /// <summary>
    /// Saves a submit. When RefNo is null, assigns refNoPrefix + the next 2-digit
    /// sequence for that prefix (retrying if another submit takes the same number).
    /// </summary>
    Task SubmitAsync(Tasaf tasaf, string refNoPrefix);
    Task DeleteAsync(Tasaf tasaf);
}
