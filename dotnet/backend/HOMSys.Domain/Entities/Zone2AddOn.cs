namespace HOMSys.Domain.Entities;

/// <summary>
/// Mirrors C:\claude\pricing\addon\{branch}\zone2.dbf — per-branch special/chain
/// add-on rates, keyed by product + (customer key OR chain zone code). Optional
/// in the price formula: 0 if no matching row.
/// </summary>
public class Zone2AddOn
{
    public int Id { get; set; }

    /// <summary>1-based physical record position in ZONE2.DBF (VFP RECNO()) — stable
    /// row identity used to diff-sync instead of a natural key, since (Branch,
    /// CustKey, CProdNo, EffDate) can legitimately repeat across rows.</summary>
    public int RecNo { get; set; }

    /// <summary>Pricing-subsystem branch folder key, e.g. "hon".</summary>
    public string Branch { get; set; } = string.Empty;

    /// <summary>Customer key, or cust4win.CZONE for a chain-level row.</summary>
    public string CustKey { get; set; } = string.Empty;

    public string CProdNo { get; set; } = string.Empty;

    public DateOnly? EffDate { get; set; }

    public decimal AddOn { get; set; }

    public decimal Rate { get; set; }

    public decimal FixAmt { get; set; }

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}
