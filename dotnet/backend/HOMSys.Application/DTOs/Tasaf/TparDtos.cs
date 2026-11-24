namespace HOMSys.Application.DTOs.Tasaf;

/// <summary>TPAR page picker row: an Approved (or Completed) TASAF and how much of it the TPARs have used.</summary>
public class TparTasafDto
{
    public int Id { get; set; }
    public string? RefNo { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? SalesGroupName { get; set; }
    public string? ProgramTypeName { get; set; }
    public string ProponentName { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateTime? ApprovedAt { get; set; }
    /// <summary>The TASAF's TotalCost — the most its TPARs may add up to.</summary>
    public decimal ApprovedAmount { get; set; }
    /// <summary>Every TPAR but Cancelled ones (they hold their amount from Draft on).</summary>
    public decimal TparTotal { get; set; }
    /// <summary>Approved TPARs only — reaching ApprovedAmount completes the TASAF.</summary>
    public decimal ApprovedTotal { get; set; }
    public decimal Balance { get; set; }
    public int TparCount { get; set; }
    /// <summary>Branches a TPAR on this TASAF can be for.</summary>
    public List<TparBranchDto> Branches { get; set; } = [];
}

public class TparDto
{
    public int Id { get; set; }
    public int TasafId { get; set; }
    public string? TasafRefNo { get; set; }
    public string TasafTitle { get; set; } = string.Empty;
    public string TasafStatus { get; set; } = string.Empty;
    public string TparNo { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int? SiteId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    /// <summary>Draft, Submitted, Approved, Cancelled.</summary>
    public string Status { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelledBy { get; set; }
    // Whoever entered it, or an Admin, while the TASAF is Approved:
    /// <summary>Save changes — Draft only.</summary>
    public bool CanEdit { get; set; }
    /// <summary>Draft → Submitted.</summary>
    public bool CanSubmit { get; set; }
    /// <summary>Submitted → Approved.</summary>
    public bool CanApprove { get; set; }
    /// <summary>Draft / Submitted / Approved → Cancelled.</summary>
    public bool CanCancel { get; set; }
    /// <summary>Submitted / Approved → back to Draft (reason required) so it can be changed.</summary>
    public bool CanRevert { get; set; }
}

public class SaveTparDto
{
    public int TasafId { get; set; }
    public int? SiteId { get; set; }
    public string TparNo { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Remarks { get; set; }
}

/// <summary>A branch option for a TPAR (Site).</summary>
public class TparBranchDto
{
    public int SiteId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
