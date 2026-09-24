namespace HOMSys.Domain.Entities;

/// <summary>
/// A Data Analytics report or dashboard. Dashboards embed copies of their
/// widget specs in DefinitionJson (no widget table), so a shared dashboard
/// can never point at a private report the viewer can't open.
/// </summary>
public class SavedReport
{
    public int Id { get; set; }

    /// <summary>"report" | "dashboard".</summary>
    public string Kind { get; set; } = "report";
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Denormalised from the spec for the gallery (null for dashboards).</summary>
    public string? DatasetKey { get; set; }

    /// <summary>Denormalised visual type for the gallery icon (null for dashboards).</summary>
    public string? Visual { get; set; }

    /// <summary>ReportSpec or DashboardDef JSON (plain nvarchar(max), not the SQL 2025 json type).</summary>
    public string DefinitionJson { get; set; } = "{}";

    /// <summary>No FK — orphaned private items are harmless; owner name shown via Users lookup.</summary>
    public int OwnerUserId { get; set; }

    /// <summary>Admin-published template, visible to every data-analytics user.</summary>
    public bool IsSystem { get; set; }

    /// <summary>CSV of Roles.Id granted view-only access, e.g. "3,5".</summary>
    public string SharedRoleIds { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
