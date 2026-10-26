using HOMSys.Application.DTOs.Tasaf;
using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

/// <summary>One repository for every TASAF master table; entityType selects the table.</summary>
public interface ITasafLookupRepository
{
    Task<List<TasafLookup>> GetAllAsync(Type entityType);
    Task<TasafLookup?> GetByIdAsync(Type entityType, int id);
    Task<bool> ExistsAsync(Type entityType, string name, string code, int? excludeId = null);
    Task CreateAsync(TasafLookup entity);
    Task UpdateAsync(TasafLookup entity);
    /// <summary>False when the row is still referenced (e.g. by a TASAF) and cannot be deleted.</summary>
    Task<bool> DeleteAsync(TasafLookup entity);
    Task<List<TasafUserOptionDto>> GetActiveUsersAsync();
    Task<Dictionary<int, string>> GetUserNamesAsync(IEnumerable<int> ids);
    /// <summary>Sales groups the user is a member of (TASAF Setup › Sales Groups › Users).</summary>
    Task<List<int>> GetSalesGroupIdsOfUserAsync(int userId);

    /// <summary>Every assigned approval-matrix cell (rows with a null UserId are vacant).</summary>
    Task<List<TasafApprover>> GetApproversAsync();
    /// <summary>Upserts the given cells (UserId null = vacant).</summary>
    Task SaveApproversAsync(IEnumerable<TasafApproverCellDto> cells, string updatedBy);

    Task<List<TasafApprovalRule>> GetRulesAsync();
    /// <summary>Upserts the given cells; Required = false deletes the rule.</summary>
    Task SaveRulesAsync(IEnumerable<TasafApprovalRuleCellDto> cells, string updatedBy);

    /// <summary>TASAF Setup › Numbering (single row) + the highest proposal number used.</summary>
    Task<(TasafNumbering setting, int? lastSeq, string? lastRefNo)> GetNumberingAsync();
    Task SaveNumberingAsync(int nextNumber, int minDigits, string updatedBy);
}
