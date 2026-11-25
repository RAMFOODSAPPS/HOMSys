namespace HOMSys.Domain.Entities;

/// <summary>
/// Post evaluation of a Completed TASAF (user request 2026-11-24, "TASA Form_MT version template 2026.xlsx" ›
/// PROMO Post Eval Template). One per TASAF. A PROMO- program type gets the full Promo Post Evaluation (actual dates,
/// doors, sales per promoted SKU, cost); every other program type the simple Post Activity form (actual execution
/// dates + cost evaluation). Actual cost ≤ planned = green, over = red flag. Signed by the same approval chain as the
/// TASAF (rebuilt at submit); its final approval closes the TASAF (Completed → Closed).
/// </summary>
public class TasafPostEval
{
    public int Id { get; set; }
    public int TasafId { get; set; }
    public Tasaf? Tasaf { get; set; }
    /// <summary>"Promo" (PROMO- program types) or "Activity" (Post Activity form).</summary>
    public string Kind { get; set; } = TasafPostEvalKind.Activity;
    public string Status { get; set; } = TasafPostEvalStatus.Draft;

    // ── Execution ─────────────────────────────────────────────
    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }
    /// <summary>Cust Keys on the TASAF when the evaluation was started.</summary>
    public int? PlannedDoors { get; set; }
    public int? ActualDoors { get; set; }
    /// <summary>What worked / what didn't (Post Activity: remarks).</summary>
    public string Explanation { get; set; } = string.Empty;
    public string Conclusion { get; set; } = string.Empty;

    // ── Cost evaluation ───────────────────────────────────────
    /// <summary>The TASAF's approved TotalCost (snapshot, refreshed on save).</summary>
    public decimal PlannedCost { get; set; }
    public decimal? ActualCost { get; set; }

    // ── Workflow (same matrix as the TASAF) ───────────────────
    public string ApprovalTypeName { get; set; } = string.Empty;
    public int? CurrentStepSeq { get; set; }
    public int? CurrentApproverUserId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? DecidedAt { get; set; }

    public int? CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public List<TasafPostEvalLine> Lines { get; set; } = [];
    public List<TasafPostEvalStep> Steps { get; set; } = [];
    public List<TasafPostEvalAttachment> Attachments { get; set; } = [];
}

public static class TasafPostEvalKind
{
    public const string Promo    = "Promo";
    public const string Activity = "Activity";
}

public static class TasafPostEvalStatus
{
    public const string Draft       = "Draft";
    public const string ForApproval = "For Approval";
    public const string Returned    = "Returned";
    /// <summary>Final sign-off: the TASAF is Closed.</summary>
    public const string Approved    = "Approved";
}

/// <summary>Sales evaluation row (Promo only): a promoted SKU of the TASAF. Volumes in cases.</summary>
public class TasafPostEvalLine
{
    public int Id { get; set; }
    public int PostEvalId { get; set; }
    public int LineNo { get; set; }
    public string CProdNo { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal LspPerCase { get; set; }
    /// <summary>Pre-promo monthly average (the TASAF's P3 Mo Avg, else SPLY).</summary>
    public decimal? PrePromoCs { get; set; }
    /// <summary>Planned promo-period volume (the TASAF's Promoted Vol).</summary>
    public decimal PlannedCs { get; set; }
    public decimal? ActualCs { get; set; }
    /// <summary>First month after the promo.</summary>
    public decimal? PostPromoCs { get; set; }
}

/// <summary>One approver of a post evaluation (same shape as TasafApprovalStep).</summary>
public class TasafPostEvalStep
{
    public int Id { get; set; }
    public int PostEvalId { get; set; }
    public int Seq { get; set; }
    public int? RoleId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Status { get; set; } = TasafStepStatus.Pending;
    public DateTime? ActedAt { get; set; }
    public string Comment { get; set; } = string.Empty;
}

/// <summary>A supporting file of a post evaluation (photos, proof of execution, receipts); only the store path is kept here.</summary>
public class TasafPostEvalAttachment
{
    public int Id { get; set; }
    public int PostEvalId { get; set; }
    public string BlobPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public int? UploadedByUserId { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
}
