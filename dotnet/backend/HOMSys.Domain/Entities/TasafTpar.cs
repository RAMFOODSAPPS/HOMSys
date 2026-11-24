namespace HOMSys.Domain.Entities;

/// <summary>
/// One TPAR (Trade Promo Analysis Report) against an Approved TASAF, entered on the TPAR page by an encoder
/// (permission `tpar`; user decision 2026-11-24, replacing the single TASAF TPAR No.). A TASAF can have several,
/// made by different BAs. Draft → Submitted → Approved (for now the encoder who entered it, or an Admin, does each
/// step), or Cancelled. Every TPAR but a Cancelled one holds its amount against the TASAF's approved TotalCost (their
/// total can't exceed it); when the Approved ones reach it, the TASAF is Completed.
/// </summary>
public class TasafTpar
{
    public int Id { get; set; }
    public int TasafId { get; set; }
    public Tasaf? Tasaf { get; set; }
    /// <summary>The TPAR document no. — free text, unique across all TASAFs (a Cancelled one frees its number).</summary>
    public string TparNo { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Remarks { get; set; } = string.Empty;
    public string Status { get; set; } = TasafTparStatus.Draft;
    /// <summary>Branch the TPAR is for: one of the TASAF's branches (any active Site when the TASAF covers an ALL … scope).</summary>
    public int? SiteId { get; set; }
    public Site? Site { get; set; }
    /// <summary>Branch name when saved (kept if the Site is renamed).</summary>
    public string BranchName { get; set; } = string.Empty;

    /// <summary>Who entered it (plain id: kept even if the user is removed).</summary>
    public int? CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelledBy { get; set; }
}

public static class TasafTparStatus
{
    public const string Draft     = "Draft";
    public const string Submitted = "Submitted";
    public const string Approved  = "Approved";
    /// <summary>Withdrawn: frees its amount and its TPAR No.; kept for the record.</summary>
    public const string Cancelled = "Cancelled";
}
