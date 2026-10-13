using System.Security.Claims;
using HOMSys.Application.DTOs.Tasaf;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace HOMSys.Application.Services;

/// <summary>A TASAF master list: its URL key, entity type and which optional fields it uses.</summary>
public record TasafMasterDef(
    string Key, string Label, Type EntityType,
    string? ParentKey = null, string? ParentLabel = null,
    string? UserLabel = null, string? AmountLabel = null);

public class TasafMasterService(ITasafLookupRepository repo, IHttpContextAccessor http)
{
    public const string NotFound = "Record not found.";
    public const string UnknownMaster = "Unknown master list.";

    // Order = order shown on the TASAF Setup page.
    public static readonly IReadOnlyList<TasafMasterDef> Masters =
    [
        new("sales-groups",    "Sales Groups",     typeof(TasafSalesGroup)),
        new("chains",          "Customers / Chains", typeof(TasafChain),       ParentKey: "sales-groups", ParentLabel: "Sales Group"),
        new("areas",           "Areas",            typeof(TasafArea)),
        new("program-types",   "Program Types",    typeof(TasafProgramType),
            ParentKey: "approval-types", ParentLabel: "Approval Type",
            AmountLabel: "Max CTS % (blank = no limit)"),
        new("objectives",      "Objectives",       typeof(TasafObjective)),
        new("target-markets",  "Target Markets",   typeof(TasafTargetMarket)),
        new("channels",        "Channels",         typeof(TasafChannel)),
        new("product-groups",  "Product Groups",   typeof(TasafProductGroup)),
        new("categories",      "Product Categories", typeof(TasafCategory),    ParentKey: "product-groups", ParentLabel: "Product Group"),
        new("cost-types",      "Cost Descriptions", typeof(TasafCostType)),
        new("approval-types",  "Approval Types",   typeof(TasafApprovalType)),
        new("approval-roles",  "Approval Roles",   typeof(TasafApprovalRole)),
    ];

    private string CurrentUser =>
        http.HttpContext?.User?.FindFirstValue(ClaimTypes.Name) ?? "system";

    private static TasafMasterDef? Find(string key) =>
        Masters.FirstOrDefault(m => m.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<TasafMasterMetaDto> GetMeta() => Masters.Select(m => new TasafMasterMetaDto
    {
        Key         = m.Key,
        Label       = m.Label,
        ParentKey   = m.ParentKey,
        ParentLabel = m.ParentLabel,
        UserLabel   = m.UserLabel,
        AmountLabel = m.AmountLabel
    });

    // ── Approval rules (approval type × role → always / over amount / not required) ──
    public async Task<TasafApprovalRulesDto> GetRulesAsync()
    {
        var types = await MapAsync(Find("approval-types")!, await repo.GetAllAsync(typeof(TasafApprovalType)));
        var roles = await MapAsync(Find("approval-roles")!, await repo.GetAllAsync(typeof(TasafApprovalRole)));
        return new TasafApprovalRulesDto
        {
            Types = types.Where(t => t.IsActive).ToList(),
            Roles = roles.Where(r => r.IsActive).ToList(),
            Rules = (await repo.GetRulesAsync()).Select(r => new TasafApprovalRuleCellDto
            {
                ApprovalTypeId = r.ApprovalTypeId,
                RoleId         = r.RoleId,
                Required       = true,
                OverAmount     = r.OverAmount
            }).ToList()
        };
    }

    public async Task<(TasafApprovalRulesDto? rules, string? error)> SaveRulesAsync(List<TasafApprovalRuleCellDto> cells)
    {
        var typeIds = (await repo.GetAllAsync(typeof(TasafApprovalType))).Select(t => t.Id).ToHashSet();
        var roleIds = (await repo.GetAllAsync(typeof(TasafApprovalRole))).Select(r => r.Id).ToHashSet();
        if (cells.Any(c => !typeIds.Contains(c.ApprovalTypeId) || !roleIds.Contains(c.RoleId)))
            return (null, "Unknown approval type or role.");
        if (cells.Any(c => c.OverAmount is < 0))
            return (null, "Amounts cannot be negative.");

        await repo.SaveRulesAsync(cells, CurrentUser);
        return (await GetRulesAsync(), null);
    }

    // ── Numbering (RefNo proposal number series) ──────────────
    public async Task<TasafNumberingDto> GetNumberingAsync()
    {
        var (setting, lastSeq, lastRefNo) = await repo.GetNumberingAsync();
        return new TasafNumberingDto
        {
            NextNumber = Math.Max(setting.NextNumber, (lastSeq ?? 0) + 1),
            MinDigits  = setting.MinDigits,
            LastNumber = lastSeq,
            LastRefNo  = lastRefNo,
            UpdatedAt  = setting.UpdatedAt,
            UpdatedBy  = setting.UpdatedBy
        };
    }

    public async Task<(TasafNumberingDto? numbering, string? error)> SaveNumberingAsync(SaveTasafNumberingDto dto)
    {
        if (dto.MinDigits is < 1 or > 6) return (null, "Minimum digits must be between 1 and 6.");
        if (dto.NextNumber is < 1 or > 9_999_999) return (null, "Next number must be between 1 and 9,999,999.");
        var (_, lastSeq, lastRefNo) = await repo.GetNumberingAsync();
        if (lastSeq is int last && dto.NextNumber <= last)
            return (null, $"Number {dto.NextNumber} is already used (last: {lastRefNo}). The next number must be {last + 1} or higher.");

        await repo.SaveNumberingAsync(dto.NextNumber, dto.MinDigits, CurrentUser);
        return (await GetNumberingAsync(), null);
    }

    // ── Approval matrix (sales group × role → user) ───────────
    public async Task<TasafApprovalMatrixDto> GetMatrixAsync()
    {
        var groups = await MapAsync(Find("sales-groups")!, await repo.GetAllAsync(typeof(TasafSalesGroup)));
        var roles  = await MapAsync(Find("approval-roles")!, await repo.GetAllAsync(typeof(TasafApprovalRole)));
        var cells  = await repo.GetApproversAsync();
        var names  = await repo.GetUserNamesAsync(cells.Where(c => c.UserId is not null).Select(c => c.UserId!.Value));
        return new TasafApprovalMatrixDto
        {
            SalesGroups = groups.Where(g => g.IsActive).ToList(),
            Roles       = roles.Where(r => r.IsActive).ToList(),
            Cells = cells.Select(c => new TasafApproverCellDto
            {
                SalesGroupId = c.SalesGroupId,
                RoleId       = c.RoleId,
                UserId       = c.UserId,
                UserName     = c.UserId is int uid ? names.GetValueOrDefault(uid) : null
            }).ToList()
        };
    }

    public async Task<(TasafApprovalMatrixDto? matrix, string? error)> SaveMatrixAsync(List<TasafApproverCellDto> cells)
    {
        var groupIds = (await repo.GetAllAsync(typeof(TasafSalesGroup))).Select(g => g.Id).ToHashSet();
        var roleIds  = (await repo.GetAllAsync(typeof(TasafApprovalRole))).Select(r => r.Id).ToHashSet();
        var userIds  = (await repo.GetActiveUsersAsync()).Select(u => u.Id).ToHashSet();
        if (cells.Any(c => !groupIds.Contains(c.SalesGroupId) || !roleIds.Contains(c.RoleId)))
            return (null, "Unknown sales group or approval role.");
        if (cells.Any(c => c.UserId is int u && !userIds.Contains(u)))
            return (null, "One of the selected users was not found or is inactive.");

        await repo.SaveApproversAsync(cells, CurrentUser);
        return (await GetMatrixAsync(), null);
    }

    public Task<List<TasafUserOptionDto>> GetUsersAsync() => repo.GetActiveUsersAsync();

    public async Task<(IEnumerable<TasafLookupDto>? rows, string? error)> GetAllAsync(string key)
    {
        var def = Find(key);
        if (def is null) return (null, UnknownMaster);

        var rows = await repo.GetAllAsync(def.EntityType);
        return (await MapAsync(def, rows), null);
    }

    public async Task<(TasafLookupDto? row, string? error)> GetByIdAsync(string key, int id)
    {
        var def = Find(key);
        if (def is null) return (null, UnknownMaster);

        var row = await repo.GetByIdAsync(def.EntityType, id);
        return row is null ? (null, NotFound) : ((await MapAsync(def, [row])).First(), null);
    }

    public async Task<(TasafLookupDto? row, string? error)> CreateAsync(string key, SaveTasafLookupDto dto)
    {
        var def = Find(key);
        if (def is null) return (null, UnknownMaster);

        var error = await ValidateAsync(def, dto, null);
        if (error is not null) return (null, error);

        var entity = (TasafLookup)Activator.CreateInstance(def.EntityType)!;
        Apply(entity, dto);
        entity.CreatedBy = CurrentUser;

        await repo.CreateAsync(entity);
        return ((await MapAsync(def, [entity])).First(), null);
    }

    public async Task<(TasafLookupDto? row, string? error)> UpdateAsync(string key, int id, SaveTasafLookupDto dto)
    {
        var def = Find(key);
        if (def is null) return (null, UnknownMaster);

        var entity = await repo.GetByIdAsync(def.EntityType, id);
        if (entity is null) return (null, NotFound);

        var error = await ValidateAsync(def, dto, id);
        if (error is not null) return (null, error);

        Apply(entity, dto);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = CurrentUser;

        await repo.UpdateAsync(entity);
        return ((await MapAsync(def, [entity])).First(), null);
    }

    public async Task<string?> DeleteAsync(string key, int id)
    {
        var def = Find(key);
        if (def is null) return UnknownMaster;

        var entity = await repo.GetByIdAsync(def.EntityType, id);
        if (entity is null) return NotFound;

        return await repo.DeleteAsync(entity)
            ? null
            : $"\"{entity.Name}\" is already in use and cannot be deleted. Set it to Inactive instead.";
    }

    private async Task<string?> ValidateAsync(TasafMasterDef def, SaveTasafLookupDto dto, int? excludeId)
    {
        dto.Code = dto.Code.Trim();
        dto.Name = dto.Name.Trim();
        dto.Description = dto.Description.Trim();

        if (dto.Name.Length == 0) return "Name is required.";
        if (dto.Name.Length > 150) return "Name must be 150 characters or fewer.";
        if (dto.Code.Length > 20) return "Code must be 20 characters or fewer.";
        if (dto.Amount is < 0) return "Amount cannot be negative.";
        if (def.EntityType == typeof(TasafProgramType) && dto.Amount is > 100) return "Max CTS % cannot be over 100.";

        if (await repo.ExistsAsync(def.EntityType, dto.Name, dto.Code, excludeId))
            return $"An entry with that name or code already exists in {def.Label}.";

        if (def.ParentKey is not null && dto.ParentId is not null)
        {
            var parent = Find(def.ParentKey)!;
            if (await repo.GetByIdAsync(parent.EntityType, dto.ParentId.Value) is null)
                return $"{def.ParentLabel} not found.";
        }

        if (def.UserLabel is not null && dto.UserId is not null
            && !(await repo.GetActiveUsersAsync()).Any(u => u.Id == dto.UserId))
            return $"{def.UserLabel}: user not found or inactive.";

        return null;
    }

    private static void Apply(TasafLookup e, SaveTasafLookupDto dto)
    {
        e.Code        = dto.Code;
        e.Name        = dto.Name;
        e.Description = dto.Description;
        e.SortOrder   = dto.SortOrder;
        e.IsActive    = dto.IsActive;
        if (e is ITasafParentLookup p) p.ParentId = dto.ParentId;
        if (e is ITasafUserLookup u)   u.UserId   = dto.UserId;
        if (e is ITasafAmountLookup a) a.Amount   = dto.Amount;
    }

    private async Task<List<TasafLookupDto>> MapAsync(TasafMasterDef def, IEnumerable<TasafLookup> rows)
    {
        var list = rows.ToList();

        var parentNames = new Dictionary<int, string>();
        if (def.ParentKey is not null)
            parentNames = (await repo.GetAllAsync(Find(def.ParentKey)!.EntityType)).ToDictionary(p => p.Id, p => p.Name);

        var userIds = list.OfType<ITasafUserLookup>().Where(u => u.UserId is not null).Select(u => u.UserId!.Value).Distinct();
        var userNames = await repo.GetUserNamesAsync(userIds);

        return list.Select(e =>
        {
            var parentId = (e as ITasafParentLookup)?.ParentId;
            var userId   = (e as ITasafUserLookup)?.UserId;
            return new TasafLookupDto
            {
                Id          = e.Id,
                Code        = e.Code,
                Name        = e.Name,
                Description = e.Description,
                SortOrder   = e.SortOrder,
                IsActive    = e.IsActive,
                ParentId    = parentId,
                ParentName  = parentId is int pid ? parentNames.GetValueOrDefault(pid) : null,
                UserId      = userId,
                UserName    = userId is int uid ? userNames.GetValueOrDefault(uid) : null,
                Amount      = (e as ITasafAmountLookup)?.Amount,
                CreatedAt   = e.CreatedAt,
                CreatedBy   = e.CreatedBy,
                UpdatedAt   = e.UpdatedAt,
                UpdatedBy   = e.UpdatedBy
            };
        }).ToList();
    }
}
