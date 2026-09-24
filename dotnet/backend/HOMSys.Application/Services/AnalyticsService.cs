using System.Security.Claims;
using System.Text.Json;
using HOMSys.Application.DTOs.Analytics;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace HOMSys.Application.Services;

/// <summary>
/// Data Analytics: resolves the viewer's branch scope and dataset access,
/// enforces share rules on saved reports/dashboards, and orchestrates export.
/// Shared items always run with the VIEWER's scope and permissions, never the
/// owner's — sharing can't escalate privilege.
/// </summary>
public class AnalyticsService(
    IAnalyticsRepository repo,
    ISiteRepository siteRepo,
    IRoleRepository roleRepo,
    IHttpContextAccessor http,
    AnalyticsExcelBuilder excel)
{
    public const int QueryCap = 5_000, ExportCap = 100_000, ValuesCap = 200;
    private const int MaxDefinitionLength = 256 * 1024;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private ClaimsPrincipal User => http.HttpContext?.User ?? new ClaimsPrincipal();
    private string CurrentUser => User.FindFirstValue(ClaimTypes.Name) ?? "system";
    private int CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private bool Has(string permission) => User.HasClaim("permission", permission);
    private bool IsAnalyticsAdmin => Has("data-analytics-admin");

    /// <summary>Asia/Manila "today" (UTC+8, no DST) — never DateTime.Today on a UTC host.</summary>
    public static DateOnly TodayPh() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));

    // ── scope & access ───────────────────────────────────────────────────────

    /// <summary>
    /// JWT "branch" claim -> Site -> pricing folder (+ WhseNos for hon-priced
    /// sites). Same resolution rules as PricelistController.GetMyBranch and
    /// ZonePricelistExportService.GetHonBranchesAsync. Null = unscoped (HO).
    /// </summary>
    public async Task<BranchScope?> ResolveScopeAsync()
    {
        var claim = User.FindFirstValue("branch");
        if (string.IsNullOrWhiteSpace(claim)) return null;

        var stripped = claim.EndsWith("-B", StringComparison.OrdinalIgnoreCase) ? claim[..^2] : claim;
        var sites = (await siteRepo.GetAllAsync()).ToList();
        var site =
            sites.FirstOrDefault(s => string.Equals(s.Code, claim, StringComparison.OrdinalIgnoreCase)) ??
            sites.FirstOrDefault(s => string.Equals(s.Code, stripped + "-B", StringComparison.OrdinalIgnoreCase));

        string? folder = site switch
        {
            null => null,
            { PricesOffHon: true } => "hon",
            _ when site.Code.EndsWith("-B", StringComparison.OrdinalIgnoreCase) => site.Code[..^2],
            _ => null,
        };
        IReadOnlyList<int> whseNos = site is { PricesOffHon: true } ? ParseWhseNos(site.Cuwhsenos) : [];
        // SiteCode = the raw claim, exactly like SalesOrderService's branch filter.
        return new BranchScope(claim, folder, whseNos);
    }

    private static List<int> ParseWhseNos(string cuwhsenos) =>
        cuwhsenos.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var n) ? n : (int?)null)
            .OfType<int>()
            .ToList();

    private string? DatasetAccessError(string? datasetKey)
    {
        var ds = repo.Datasets.FirstOrDefault(d => string.Equals(d.Key, datasetKey, StringComparison.OrdinalIgnoreCase));
        if (ds is null) return $"Unknown dataset '{datasetKey}'.";
        if (ds.Permission is not null && !Has(ds.Permission)) return $"You don't have access to the '{ds.Label}' dataset.";
        return null;
    }

    public async Task<AnalyticsMetaDto> GetMetaAsync()
    {
        var scope = await ResolveScopeAsync();
        var datasets = repo.Datasets.Where(d => d.Permission is null || Has(d.Permission)).ToList();
        var roles = (await roleRepo.GetAllAsync()).OrderBy(r => r.Name).Select(r => new RoleOptionDto(r.Id, r.Name)).ToList();
        return new AnalyticsMetaDto(datasets, roles, scope?.SiteCode, TodayPh(), IsAnalyticsAdmin);
    }

    public async Task<(AnalyticsResultDto? Result, string? Error)> QueryAsync(ReportSpecDto spec)
    {
        if (DatasetAccessError(spec.Dataset) is { } err) return (null, err);
        return await repo.QueryAsync(spec, await ResolveScopeAsync(), TodayPh(), QueryCap);
    }

    public async Task<(List<ValueOptionDto>? Values, string? Error)> ValuesAsync(string dataset, string field, string? q, int take)
    {
        if (DatasetAccessError(dataset) is { } err) return (null, err);
        return await repo.ValuesAsync(dataset, field, q, Math.Clamp(take, 1, ValuesCap), await ResolveScopeAsync(), TodayPh());
    }

    // ── export ───────────────────────────────────────────────────────────────

    /// <summary>One workbook, one sheet per item. Top-N is lifted so the export holds every row (up to ExportCap).</summary>
    public async Task<(byte[]? Bytes, string? Error)> ExportAsync(ExportRequest request)
    {
        if (request.Items is not { Count: > 0 }) return (null, "Nothing to export.");
        if (request.Items.Count > 30) return (null, "At most 30 items can be exported at once.");

        var today = TodayPh();
        var scope = await ResolveScopeAsync();
        var sheets = new List<ExportSheet>();
        foreach (var item in request.Items)
        {
            if (DatasetAccessError(item.Spec.Dataset) is { } err) return (null, err);
            var spec = item.Spec;
            var full = new ReportSpecDto
            {
                V = spec.V, Dataset = spec.Dataset, Type = spec.Type, Dimensions = spec.Dimensions,
                Measures = spec.Measures, Filters = spec.Filters, Sort = spec.Sort, Fill = spec.Fill,
                Limit = null, Others = false, Compare = null,
            };
            var (result, error) = await repo.QueryAsync(full, scope, today, ExportCap);
            if (error is not null) return (null, $"{item.Title}: {error}");
            sheets.Add(new ExportSheet(item.Title, repo.Describe(full, today), result!, spec.Type == "pivot"));
        }
        var bytes = excel.Build(request.Title, CurrentUser, DateTime.UtcNow.AddHours(8), sheets);
        return (bytes, null);
    }

    // ── saved reports / dashboards ───────────────────────────────────────────

    private async Task<HashSet<int>> MyRoleIdsAsync() =>
        (await roleRepo.GetAllAsync()).Where(r => User.IsInRole(r.Name)).Select(r => r.Id).ToHashSet();

    private static List<int> ParseRoleIds(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var n) ? n : (int?)null).OfType<int>().ToList();

    private bool CanView(SavedReport r, HashSet<int> myRoles) =>
        r.OwnerUserId == CurrentUserId || r.IsSystem || IsAnalyticsAdmin || ParseRoleIds(r.SharedRoleIds).Any(myRoles.Contains);

    private bool CanEdit(SavedReport r) => (r.OwnerUserId == CurrentUserId && !r.IsSystem) || IsAnalyticsAdmin;

    private SavedItemDto Map(SavedReport r, IReadOnlyDictionary<int, string> owners, bool withDefinition) => new()
    {
        Id = r.Id,
        Kind = r.Kind,
        Name = r.Name,
        Description = r.Description,
        DatasetKey = r.DatasetKey,
        Visual = r.Visual,
        OwnerName = r.IsSystem ? "System" : owners.GetValueOrDefault(r.OwnerUserId, "(unknown)"),
        IsOwner = r.OwnerUserId == CurrentUserId,
        CanEdit = CanEdit(r),
        IsSystem = r.IsSystem,
        SharedRoleIds = ParseRoleIds(r.SharedRoleIds),
        UpdatedAt = r.UpdatedAt ?? r.CreatedAt,
        Definition = withDefinition ? r.DefinitionJson : null,
    };

    public async Task<List<SavedItemDto>> GetItemsAsync()
    {
        var myRoles = await MyRoleIdsAsync();
        var items = (await repo.GetItemsAsync()).Where(r => CanView(r, myRoles)).ToList();
        var owners = await repo.GetUserNamesAsync(items.Select(i => i.OwnerUserId));
        return items.Select(i => Map(i, owners, false)).ToList();
    }

    /// <summary>Null when missing OR not viewable, so ids can't be probed.</summary>
    public async Task<SavedItemDto?> GetItemAsync(int id)
    {
        var item = await repo.GetItemAsync(id);
        if (item is null || !CanView(item, await MyRoleIdsAsync())) return null;
        var owners = await repo.GetUserNamesAsync([item.OwnerUserId]);
        return Map(item, owners, true);
    }

    public async Task<(SavedItemDto? Item, string? Error)> CreateAsync(SaveItemRequest req)
    {
        var (entity, error) = await BuildEntityAsync(req, null);
        if (error is not null) return (null, error);
        entity!.OwnerUserId = CurrentUserId;
        entity.CreatedAt = DateTime.UtcNow;
        entity.CreatedBy = CurrentUser;
        var created = await repo.CreateItemAsync(entity);
        return (await GetItemAsync(created.Id), null);
    }

    public async Task<(SavedItemDto? Item, string? Error)> UpdateAsync(int id, SaveItemRequest req)
    {
        var existing = await repo.GetItemAsync(id);
        if (existing is null || !CanView(existing, await MyRoleIdsAsync())) return (null, NotFound);
        if (!CanEdit(existing)) return (null, "You can't edit this item. Use Save As to make your own copy.");
        if (!string.Equals(existing.Kind, req.Kind, StringComparison.OrdinalIgnoreCase)) return (null, "An item's kind can't be changed.");

        var (entity, error) = await BuildEntityAsync(req, existing);
        if (error is not null) return (null, error);
        entity!.Id = id;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = CurrentUser;
        await repo.UpdateItemAsync(entity);
        return (await GetItemAsync(id), null);
    }

    public async Task<string?> DeleteAsync(int id)
    {
        var existing = await repo.GetItemAsync(id);
        if (existing is null || !CanView(existing, await MyRoleIdsAsync())) return NotFound;
        if (!CanEdit(existing)) return "You can't delete this item.";
        await repo.DeleteItemAsync(id);
        return null;
    }

    public const string NotFound = "Item not found.";

    private async Task<(SavedReport? Entity, string? Error)> BuildEntityAsync(SaveItemRequest req, SavedReport? existing)
    {
        var kind = req.Kind?.Trim().ToLowerInvariant();
        if (kind is not ("report" or "dashboard")) return (null, "Kind must be report or dashboard.");
        var name = req.Name?.Trim() ?? "";
        if (name.Length is 0 or > 100) return (null, "Name is required (max 100 characters).");
        if ((req.Description?.Length ?? 0) > 500) return (null, "Description is too long (max 500 characters).");
        if (string.IsNullOrWhiteSpace(req.Definition) || req.Definition.Length > MaxDefinitionLength)
            return (null, "The definition is missing or too large.");

        // Publishing a system template is an admin action; editing keeps the current flag otherwise.
        var isSystem = existing?.IsSystem ?? false;
        if (req.IsSystem != isSystem)
        {
            if (!IsAnalyticsAdmin) return (null, "Only analytics administrators can publish system templates.");
            isSystem = req.IsSystem;
        }

        var validRoles = (await roleRepo.GetAllAsync()).Select(r => r.Id).ToHashSet();
        var roleIds = (req.SharedRoleIds ?? []).Where(validRoles.Contains).Distinct().OrderBy(i => i).ToList();

        var today = TodayPh();
        string? datasetKey = null, visual = null;
        try
        {
            if (kind == "report")
            {
                var spec = JsonSerializer.Deserialize<ReportSpecDto>(req.Definition, Json);
                if (spec is null) return (null, "The report definition is empty.");
                if (repo.Validate(spec, today) is { } err) return (null, err);
                datasetKey = spec.Dataset;
                visual = spec.Type;
            }
            else
            {
                var dash = JsonSerializer.Deserialize<DashboardDefDto>(req.Definition, Json);
                if (dash is null) return (null, "The dashboard definition is empty.");
                if (dash.Widgets.Count > 30) return (null, "A dashboard can hold at most 30 widgets.");
                foreach (var w in dash.Widgets)
                    if (repo.Validate(w.Spec, today) is { } err)
                        return (null, $"Widget '{w.Title}': {err}");
            }
        }
        catch (JsonException)
        {
            return (null, "The definition is not valid JSON.");
        }

        return (new SavedReport
        {
            Kind = kind,
            Name = name,
            Description = req.Description?.Trim() ?? "",
            DatasetKey = datasetKey,
            Visual = visual,
            DefinitionJson = req.Definition,
            IsSystem = isSystem,
            SharedRoleIds = string.Join(",", roleIds),
        }, null);
    }
}
