using HOMSys.Application.DTOs.Tasaf;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class TasafLookupRepository(AppDbContext db) : ITasafLookupRepository
{
    private static readonly Dictionary<Type, Func<AppDbContext, IQueryable<TasafLookup>>> Sets = new()
    {
        [typeof(TasafSalesGroup)]    = c => c.TasafSalesGroups.Include(g => g.Members),
        [typeof(TasafChain)]         = c => c.TasafChains,
        [typeof(TasafArea)]          = c => c.TasafAreas,
        [typeof(TasafProgramType)]   = c => c.TasafProgramTypes,
        [typeof(TasafObjective)]     = c => c.TasafObjectives,
        [typeof(TasafTargetMarket)]  = c => c.TasafTargetMarkets,
        [typeof(TasafChannel)]       = c => c.TasafChannels,
        [typeof(TasafProductGroup)]  = c => c.TasafProductGroups,
        [typeof(TasafCostType)]      = c => c.TasafCostTypes,
        [typeof(TasafApprovalRole)]  = c => c.TasafApprovalRoles,
        [typeof(TasafApprovalType)]  = c => c.TasafApprovalTypes,
    };

    private IQueryable<TasafLookup> Query(Type entityType) => Sets[entityType](db);

    public async Task<List<TasafLookup>> GetAllAsync(Type entityType) =>
        await Query(entityType)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .ToListAsync();

    public async Task<TasafLookup?> GetByIdAsync(Type entityType, int id) =>
        await Query(entityType).FirstOrDefaultAsync(x => x.Id == id);

    public async Task<bool> ExistsAsync(Type entityType, string name, string code, int? excludeId = null)
    {
        var query = Query(entityType);
        if (excludeId.HasValue)
            query = query.Where(x => x.Id != excludeId.Value);

        return await query.AnyAsync(x =>
            x.Name == name ||
            (code != "" && x.Code == code));
    }

    public async Task CreateAsync(TasafLookup entity)
    {
        db.Add(entity);
        await db.SaveChangesAsync();
    }

    // Entity comes tracked from GetByIdAsync, so its changes are already staged.
    public async Task UpdateAsync(TasafLookup entity) => await db.SaveChangesAsync();

    public async Task<bool> DeleteAsync(TasafLookup entity)
    {
        db.Remove(entity);
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            // FK restrict: still referenced by a chain/category/TASAF.
            db.Entry(entity).State = EntityState.Unchanged;
            return false;
        }
    }

    public async Task<List<TasafUserOptionDto>> GetActiveUsersAsync() =>
        await db.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new TasafUserOptionDto
            {
                Id       = u.Id,
                Username = u.Username,
                FullName = (u.FirstName + " " + u.LastName).Trim()
            })
            .ToListAsync();

    public async Task<List<int>> GetSalesGroupIdsOfUserAsync(int userId) =>
        await db.TasafSalesGroupUsers.Where(m => m.UserId == userId).Select(m => m.SalesGroupId).ToListAsync();

    public async Task<List<TasafApprover>> GetApproversAsync() =>
        await db.TasafApprovers.AsNoTracking().ToListAsync();

    public async Task SaveApproversAsync(IEnumerable<TasafApproverCellDto> cells, string updatedBy)
    {
        var existing = await db.TasafApprovers.ToDictionaryAsync(a => (a.SalesGroupId, a.RoleId));
        foreach (var c in cells)
        {
            if (!existing.TryGetValue((c.SalesGroupId, c.RoleId), out var row))
            {
                if (c.UserId is null) continue;
                row = new TasafApprover { SalesGroupId = c.SalesGroupId, RoleId = c.RoleId };
                db.TasafApprovers.Add(row);
                existing[(c.SalesGroupId, c.RoleId)] = row;
            }
            if (row.UserId == c.UserId) continue;
            row.UserId    = c.UserId;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = updatedBy;
        }
        await db.SaveChangesAsync();
    }

    public async Task<List<TasafApprovalRule>> GetRulesAsync() =>
        await db.TasafApprovalRules.AsNoTracking().ToListAsync();

    public async Task SaveRulesAsync(IEnumerable<TasafApprovalRuleCellDto> cells, string updatedBy)
    {
        var existing = await db.TasafApprovalRules.ToDictionaryAsync(r => (r.ApprovalTypeId, r.RoleId));
        foreach (var c in cells)
        {
            existing.TryGetValue((c.ApprovalTypeId, c.RoleId), out var row);
            if (!c.Required)
            {
                if (row is not null) db.TasafApprovalRules.Remove(row);
                continue;
            }
            if (row is null)
            {
                row = new TasafApprovalRule { ApprovalTypeId = c.ApprovalTypeId, RoleId = c.RoleId };
                db.TasafApprovalRules.Add(row);
            }
            else if (row.OverAmount == c.OverAmount) continue;
            row.OverAmount = c.OverAmount;
            row.UpdatedAt  = DateTime.UtcNow;
            row.UpdatedBy  = updatedBy;
        }
        await db.SaveChangesAsync();
    }

    public async Task<Dictionary<int, string>> GetUserNamesAsync(IEnumerable<int> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return [];
        return await db.Users
            .Where(u => idList.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.FirstName + " " + u.LastName).Trim());
    }

    public async Task<(TasafNumbering setting, int? lastSeq, string? lastRefNo)> GetNumberingAsync()
    {
        var setting = await db.TasafNumberings.AsNoTracking().FirstAsync(x => x.Id == 1);
        var last = await db.Tasafs.AsNoTracking()
            .Where(t => t.RefSeq != null)
            .OrderByDescending(t => t.RefSeq)
            .Select(t => new { t.RefSeq, t.RefNo })
            .FirstOrDefaultAsync();
        return (setting, last?.RefSeq, last?.RefNo);
    }

    public async Task SaveNumberingAsync(int nextNumber, int minDigits, string updatedBy) =>
        await db.TasafNumberings.Where(x => x.Id == 1).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.NextNumber, nextNumber)
            .SetProperty(x => x.MinDigits, minDigits)
            .SetProperty(x => x.UpdatedAt, DateTime.UtcNow)
            .SetProperty(x => x.UpdatedBy, updatedBy));
}
