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
    /// <summary>The TASAF whose approval step carries this email-link token hash, or null.</summary>
    Task<Tasaf?> GetByEmailTokenAsync(string tokenHash);
    Task CreateAsync(Tasaf tasaf);
    /// <summary>Saves header changes; lines were replaced on the tracked entity. Re-read with GetByIdAsync after.</summary>
    Task UpdateAsync(Tasaf tasaf);
    /// <summary>
    /// Saves the TASAF (adds it when isNew). When refNoPrefix is given and RefNo is still null,
    /// also assigns refNoPrefix + the next number of the one continuous series (TASAF Setup ›
    /// Numbering), retrying if a concurrent save takes the same number. Clears the tracker.
    /// </summary>
    Task SaveWithRefNoAsync(Tasaf tasaf, string? refNoPrefix, bool isNew = false);
    Task DeleteAsync(Tasaf tasaf);
}
