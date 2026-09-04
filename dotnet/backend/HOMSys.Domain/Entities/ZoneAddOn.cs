namespace HOMSys.Domain.Entities;

/// <summary>
/// Mirrors C:\claude\pricing\addon\{branch}\zone.dbf — per-branch zone add-on
/// rates, keyed by product + zone. Only the columns the price lookup needs are
/// imported (OLD_ADD_ON/SRPR8/STATUS/STATDESC are write-path-only concerns in
/// the Pricing Adjustment subsystem, not needed here).
/// </summary>
public class ZoneAddOn
{
    public int Id { get; set; }

    /// <summary>1-based physical record position in ZONE.DBF (VFP RECNO()) — stable
    /// row identity used to diff-sync instead of a natural key, since (Branch,
    /// CProdNo, CZone, EffDate) can legitimately repeat across rows.</summary>
    public int RecNo { get; set; }

    /// <summary>Pricing-subsystem branch folder key, e.g. "hon".</summary>
    public string Branch { get; set; } = string.Empty;

    public string CProdNo { get; set; } = string.Empty;

    public string CZone { get; set; } = string.Empty;

    public DateOnly? EffDate { get; set; }

    public decimal AddOn { get; set; }

    public decimal Rate { get; set; }

    public decimal FixAmt { get; set; }

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}
