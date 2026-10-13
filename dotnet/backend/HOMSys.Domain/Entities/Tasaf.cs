namespace HOMSys.Domain.Entities;

/// <summary>
/// Trade Activity Spend Approval Form — the "TASAF Template" sheet of
/// "TASA Form_MT version template 2026.xlsx". Filed by a KAM before the
/// activity runs; GKAM reviews, then one approval level signs off by amount.
/// </summary>
public class Tasaf
{
    public int Id { get; set; }

    /// <summary>YEAR-SalesGroup-ProponentInitials-NN (e.g. 2026-RKA-ER-03). Null until submitted.</summary>
    public string? RefNo { get; set; }
    /// <summary>The proposal number (last part of RefNo): one continuous series, unique. See TasafNumbering.</summary>
    public int? RefSeq { get; set; }
    public string Status { get; set; } = TasafStatus.Draft;

    // ── Program ───────────────────────────────────────────────
    public string Title { get; set; } = string.Empty;
    /// <summary>Program duration (inclusive).</summary>
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public int? SalesGroupId { get; set; }
    public TasafSalesGroup? SalesGroup { get; set; }
    public int? ChainId { get; set; }
    public TasafChain? Chain { get; set; }

    public int? ProgramTypeId { get; set; }
    public TasafProgramType? ProgramType { get; set; }
    public string ProgramTypeOther { get; set; } = string.Empty;
    public int? ObjectiveId { get; set; }
    public TasafObjective? Objective { get; set; }
    public string ObjectiveOther { get; set; } = string.Empty;
    public int? TargetMarketId { get; set; }
    public TasafTargetMarket? TargetMarket { get; set; }
    public string TargetMarketOther { get; set; } = string.Empty;
    public int? ChannelId { get; set; }
    public TasafChannel? Channel { get; set; }

    public int? ProductGroupId { get; set; }
    public TasafProductGroup? ProductGroup { get; set; }
    public int? ProductGroup2Id { get; set; }
    public TasafProductGroup? ProductGroup2 { get; set; }
    public int? CategoryId { get; set; }
    public TasafCategory? Category { get; set; }
    public int? Category2Id { get; set; }
    public TasafCategory? Category2 { get; set; }

    public string Background { get; set; } = string.Empty;
    public string Concessions { get; set; } = string.Empty;
    public string Mechanics { get; set; } = string.Empty;

    // ── Computation (pesos) ───────────────────────────────────
    /// <summary>Σ cost lines.</summary>
    public decimal TotalCost { get; set; }
    /// <summary>Σ product Promoted Vol × LSP when products are listed, else entered.</summary>
    public decimal? PromotedValue { get; set; }
    /// <summary>Pre-promo monthly average sales — P3M average, or SPLY for seasonal SKUs. Entered.</summary>
    public decimal? PrePromoSales { get; set; }
    /// <summary>Σ product Incremental Vol × LSP when products are listed, else entered.</summary>
    public decimal? IncrementalSales { get; set; }

    // ── Mode of payment: payment type × who liquidates ────────
    public decimal PayInvDeductionAp { get; set; }
    public decimal PayInvDeductionSalesman { get; set; }
    public decimal PayCheckAp { get; set; }
    public decimal PayCheckSalesman { get; set; }
    public decimal PayFreeGoodsAp { get; set; }
    public decimal PayFreeGoodsSalesman { get; set; }
    public decimal PayOffInvoiceAp { get; set; }
    public decimal PayOffInvoiceSalesman { get; set; }

    // ── Proponent / audit ─────────────────────────────────────
    public int ProponentUserId { get; set; }
    public User? ProponentUser { get; set; }
    public string ProponentName { get; set; } = string.Empty;

    // ── Workflow — the approval chain is built at (re)submit ──
    public DateTime? SubmittedAt { get; set; }
    /// <summary>Approval matrix used at submit (from the program type), e.g. "Promo / Near Ex Moveout".</summary>
    public string ApprovalTypeName { get; set; } = string.Empty;
    /// <summary>Seq of the step waiting for a decision (Status For Approval), else null.</summary>
    public int? CurrentStepSeq { get; set; }
    /// <summary>That step's approver — denormalised for "awaiting me" queries.</summary>
    public int? CurrentApproverUserId { get; set; }
    public DateTime? DecidedAt { get; set; }

    public List<TasafApprovalStep> Steps { get; set; } = [];
    public List<TasafAction> Actions { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>Areas covered (multi-select).</summary>
    public List<TasafAreaLink> Areas { get; set; } = [];
    public List<TasafBranch> Branches { get; set; } = [];
    /// <summary>Participating stores (Cust Keys) from the customer master — any number.</summary>
    public List<TasafCustomer> Customers { get; set; } = [];
    public List<TasafProduct> Products { get; set; } = [];
    public List<TasafCost> Costs { get; set; } = [];
}

/// <summary>
/// Draft → For Approval (each step of the chain in turn) → Approved.
/// Any approver can Return (back to the KAM to edit + resubmit, chain restarts) or Reject (final).
/// </summary>
public static class TasafStatus
{
    public const string Draft       = "Draft";
    public const string ForApproval = "For Approval";
    public const string Returned    = "Returned";
    public const string Approved    = "Approved";
    public const string Rejected    = "Rejected";
    /// <summary>Withdrawn by the proponent (or an Admin). Kept for the record, never deleted.</summary>
    public const string Cancelled   = "Cancelled";
}

public static class TasafStepStatus
{
    public const string Pending  = "Pending";
    public const string Approved = "Approved";
    public const string Returned = "Returned";
    public const string Rejected = "Rejected";
    /// <summary>No one holds the role for this sales group (vacant / NA).</summary>
    public const string Skipped  = "Skipped";
}

/// <summary>One approver in a TASAF's chain — a snapshot of the matrix at submit.</summary>
public class TasafApprovalStep
{
    public int Id { get; set; }
    public int TasafId { get; set; }
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

/// <summary>Approval history row — one per submit / review / decision.</summary>
public class TasafAction
{
    public int Id { get; set; }
    public int TasafId { get; set; }
    /// <summary>Submitted, Resubmitted, Approved, Returned, Rejected, Reverted (Admin), Cancelled.</summary>
    public string Action { get; set; } = string.Empty;
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public int ByUserId { get; set; }
    public string ByName { get; set; } = string.Empty;
    /// <summary>"KAM" or the approval role code (GKAM, TMM, NSM, NSD, COO).</summary>
    public string Role { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public DateTime At { get; set; } = DateTime.UtcNow;
}

/// <summary>An area the TASAF covers (TasafArea master row).</summary>
public class TasafAreaLink
{
    public int Id { get; set; }
    public int TasafId { get; set; }
    public int LineNo { get; set; }
    public int AreaId { get; set; }
    public TasafArea? Area { get; set; }
}

/// <summary>A participating branch: a Site, or a scope such as "ALL LUZON" (SiteId null).</summary>
public class TasafBranch
{
    public int Id { get; set; }
    public int TasafId { get; set; }
    public int LineNo { get; set; }
    public int? SiteId { get; set; }
    public Site? Site { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>A participating store (cust4win CustKey + name at the time it was picked).</summary>
public class TasafCustomer
{
    public int Id { get; set; }
    public int TasafId { get; set; }
    public int LineNo { get; set; }
    public string CustKey { get; set; } = string.Empty;
    public string CusName { get; set; } = string.Empty;
}

/// <summary>Participating Products row. Volumes in cases.</summary>
public class TasafProduct
{
    public int Id { get; set; }
    public int TasafId { get; set; }
    public int LineNo { get; set; }
    public string CProdNo { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal? P3MoAvgCs { get; set; }
    public decimal? SplyCs { get; set; }
    public decimal PromotedVolCs { get; set; }
    public decimal IncrVolCs { get; set; }
    public decimal LspPerCase { get; set; }
    /// <summary>PromotedVolCs × LspPerCase.</summary>
    public decimal PromotedValue { get; set; }
}

/// <summary>Cost Details row.</summary>
public class TasafCost
{
    public int Id { get; set; }
    public int TasafId { get; set; }
    public int LineNo { get; set; }
    public int? CostTypeId { get; set; }
    public TasafCostType? CostType { get; set; }
    public decimal Amount { get; set; }
    public string Remarks { get; set; } = string.Empty;
}

/// <summary>
/// TASAF Setup › Numbering (single row, Id 1). The proposal number is one continuous series
/// across all TASAFs; the next RefNo takes max(NextNumber, highest used + 1).
/// </summary>
public class TasafNumbering
{
    public int Id { get; set; }
    public int NextNumber { get; set; } = 1;
    /// <summary>Zero-padded to at least this many digits (2 → 01 … 99, 100, ...).</summary>
    public int MinDigits { get; set; } = 2;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
