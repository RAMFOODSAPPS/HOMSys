namespace HOMSys.Application.DTOs.Analytics;

// ── Report spec (persisted as JSON in SavedReports.DefinitionJson) ──────────

public record SpecDim(string Field, string? Bucket = null);
public record SpecMeasure(string Field, string? Agg = null, string? Label = null);
public record SpecFilter(string Field, string Op, List<string?>? Values = null);
public record SpecSort(string By, string Dir = "desc");

public class ReportSpecDto
{
    public int V { get; set; } = 1;
    public string Dataset { get; set; } = string.Empty;
    public string Type { get; set; } = "table";   // table | pivot | kpi | bar | line | pie | doughnut
    public bool Stacked { get; set; }
    public bool Horizontal { get; set; }
    public List<SpecDim> Dimensions { get; set; } = [];
    public List<SpecMeasure> Measures { get; set; } = [];
    public List<SpecFilter> Filters { get; set; } = [];
    public SpecSort? Sort { get; set; }
    public int? Limit { get; set; }
    public bool Others { get; set; }
    public bool Fill { get; set; }
    public string? Compare { get; set; }           // previousPeriod
}

// ── Query result ─────────────────────────────────────────────────────────────

/// <summary>Role: dim | caption | measure. Rows are arrays aligned to Columns.</summary>
public record ColumnDto(string Key, string? Field, string Label, string Type, string Role,
    string? Format = null, bool Additive = true, string? Of = null, string? Bucket = null);

public class AnalyticsResultDto
{
    public List<ColumnDto> Columns { get; set; } = [];
    public List<object?[]> Rows { get; set; } = [];
    /// <summary>"(Others)" rows for top-N (1 row for 1 dim, one per series for 2 dims).</summary>
    public List<object?[]> OthersRows { get; set; } = [];
    public object?[]? Total { get; set; }
    /// <summary>Per first-dimension subtotals (2-dim shapes only).</summary>
    public List<object?[]>? RowTotals { get; set; }
    /// <summary>Per second-dimension subtotals (2-dim shapes only).</summary>
    public List<object?[]>? ColTotals { get; set; }
    public object?[]? Compare { get; set; }
    public string? CompareLabel { get; set; }
    public bool Truncated { get; set; }
    public DateTime? AsOf { get; set; }
    public string? Scope { get; set; }
}

// ── Catalog metadata (no SQL ever leaves the server) ────────────────────────

public record FieldDto(string Key, string Label, string Kind, string Type, IReadOnlyList<string> Aggs,
    string? DefaultAgg, string? Format, bool HasCaption, string? DrillTo, string? Description, bool Additive);

public record DatasetDto(string Key, string Label, string Grain, string? DefaultDate, bool Heavy, bool National,
    IReadOnlyList<FieldDto> Fields, IReadOnlyList<string> Detail, IReadOnlyList<SpecFilter> Defaults, string? Permission);

public record RoleOptionDto(int Id, string Name);

public record AnalyticsMetaDto(IReadOnlyList<DatasetDto> Datasets, IReadOnlyList<RoleOptionDto> Roles,
    string? Scope, DateOnly Today, bool CanPublish);

public record ValueOptionDto(string? Value, string? Caption);

/// <summary>Viewer's branch row-level scope, resolved from the JWT "branch" claim. Null = unscoped (HO).</summary>
public record BranchScope(string SiteCode, string? Folder, IReadOnlyList<int> WhseNos);

// ── Saved reports / dashboards ───────────────────────────────────────────────

public class SavedItemDto
{
    public int Id { get; set; }
    public string Kind { get; set; } = "report";
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? DatasetKey { get; set; }
    public string? Visual { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public bool IsOwner { get; set; }
    public bool CanEdit { get; set; }
    public bool IsSystem { get; set; }
    public List<int> SharedRoleIds { get; set; } = [];
    public DateTime? UpdatedAt { get; set; }
    /// <summary>Only populated by GET items/{id}.</summary>
    public string? Definition { get; set; }
}

public class SaveItemRequest
{
    public string Kind { get; set; } = "report";
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Definition { get; set; } = "{}";
    public List<int> SharedRoleIds { get; set; } = [];
    public bool IsSystem { get; set; }
}

/// <summary>Dashboard definition (SavedReports.DefinitionJson for Kind = dashboard). Widgets embed spec copies.</summary>
public class DashboardDefDto
{
    public int V { get; set; } = 1;
    public List<SpecFilter> Filters { get; set; } = [];
    public List<DashboardWidgetDto> Widgets { get; set; } = [];
}

public class DashboardWidgetDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int W { get; set; } = 6;
    public int H { get; set; } = 3;
    public ReportSpecDto Spec { get; set; } = new();
    public int? SourceItemId { get; set; }
}

public record ExportItem(string Title, ReportSpecDto Spec);
public record ExportRequest(string Title, List<ExportItem> Items);

/// <summary>One sheet worth of export data, handed from the service to AnalyticsExcelBuilder.</summary>
public record ExportSheet(string Title, string FiltersText, AnalyticsResultDto Result, bool Pivot);
