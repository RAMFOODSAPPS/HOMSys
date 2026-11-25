namespace HOMSys.Application.DTOs.Tasaf;

/// <summary>Post Evaluation list row: a Completed / Closed TASAF and its post evaluation (if started).</summary>
public class TasafPostEvalListItemDto
{
    public int TasafId { get; set; }
    public string? RefNo { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TasafStatus { get; set; } = string.Empty;
    public string? SalesGroupName { get; set; }
    public string? ProgramTypeName { get; set; }
    public string ProponentName { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    /// <summary>"Promo" or "Activity".</summary>
    public string Kind { get; set; } = string.Empty;
    public int? PostEvalId { get; set; }
    /// <summary>Null = not started.</summary>
    public string? Status { get; set; }
    public decimal PlannedCost { get; set; }
    public decimal? ActualCost { get; set; }
    /// <summary>Actual cost over the planned (approved) cost.</summary>
    public bool? OverBudget { get; set; }
    public string? CurrentRoleCode { get; set; }
    public string? CurrentApproverName { get; set; }
    public bool AwaitingMe { get; set; }
    public bool IsMine { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
}

/// <summary>The post evaluation of one TASAF — a saved one, or (Id 0) a new one pre-filled from the TASAF.</summary>
public class TasafPostEvalDto
{
    public int Id { get; set; }
    public int TasafId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    // ── From the TASAF (read-only) ──
    public string? RefNo { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TasafStatus { get; set; } = string.Empty;
    public string? SalesGroupName { get; set; }
    public string? AreaNames { get; set; }
    public string? ChainNames { get; set; }
    public string? CustKeys { get; set; }
    public int CustKeyCount { get; set; }
    public string? ProgramTypeName { get; set; }
    public string? ObjectiveName { get; set; }
    public string? TargetMarketName { get; set; }
    public string? ChannelNames { get; set; }
    public string? ProductCategoryNames { get; set; }
    public string? SegmentNames { get; set; }
    public string Mechanics { get; set; } = string.Empty;
    public DateOnly? PlannedStartDate { get; set; }
    public DateOnly? PlannedEndDate { get; set; }
    public string ProponentName { get; set; } = string.Empty;
    /// <summary>Approved TPARs of the TASAF (the default actual cost).</summary>
    public decimal TparApprovedTotal { get; set; }

    // ── Entered ──
    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }
    public int? PlannedDoors { get; set; }
    public int? ActualDoors { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public string Conclusion { get; set; } = string.Empty;
    public decimal PlannedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public bool? OverBudget { get; set; }
    public List<TasafPostEvalLineDto> Lines { get; set; } = [];

    // ── Workflow ──
    public string ApprovalTypeName { get; set; } = string.Empty;
    public List<TasafStepDto> Steps { get; set; } = [];
    public List<TasafPostEvalAttachmentDto> Attachments { get; set; } = [];
    /// <summary>May add / remove files (the proponent or an Admin while Draft / Returned).</summary>
    public bool CanAttach { get; set; }
    public string? CurrentRoleCode { get; set; }
    public string? CurrentApproverName { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public bool CanEdit { get; set; }
    public bool CanSubmit { get; set; }
    /// <summary>The current step is the caller's.</summary>
    public bool AwaitingMe { get; set; }
    /// <summary>Admin signing the current step for its approver.</summary>
    public bool CanAdminDecide { get; set; }
    public bool CanRevert { get; set; }
}

public class TasafPostEvalLineDto
{
    public int LineNo { get; set; }
    public string CProdNo { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal LspPerCase { get; set; }
    public decimal? PrePromoCs { get; set; }
    public decimal PlannedCs { get; set; }
    public decimal? ActualCs { get; set; }
    public decimal? PostPromoCs { get; set; }
}

public class SaveTasafPostEvalDto
{
    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }
    public int? ActualDoors { get; set; }
    public string? Explanation { get; set; }
    public string? Conclusion { get; set; }
    public decimal? ActualCost { get; set; }
    /// <summary>Only ActualCs / PostPromoCs are taken, matched on LineNo.</summary>
    public List<TasafPostEvalLineDto> Lines { get; set; } = [];
}

public class TasafPostEvalAttachmentDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime UploadedAt { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
}
