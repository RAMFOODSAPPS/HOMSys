namespace HOMSys.Domain.Entities;

// TASAF (Trade Activity Spend Approval Form) master lists — the DATA sheet of
// "TASA Form_MT version template 2026.xlsx". Each list is its own table; they
// share these columns so one service/controller/page can manage all of them.
public abstract class TasafLookup
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

// Optional extras a list can carry. Implemented explicitly so EF maps only the
// list's own named column (SalesGroupId, MaxCtsPercent, ...).
public interface ITasafParentLookup { int? ParentId { get; set; } }
public interface ITasafUserLookup   { int? UserId   { get; set; } }
public interface ITasafAmountLookup { decimal? Amount { get; set; } }
public interface ITasafTextLookup   { string Text { get; set; } }

/// <summary>NKA / RKA / LKA / GT. Its approvers per role are in TasafApprover (the approval matrix).</summary>
public class TasafSalesGroup : TasafLookup { }

/// <summary>Customer chain/account (SVI, Puregold, RCS Chain ...).</summary>
public class TasafChain : TasafLookup, ITasafParentLookup, ITasafTextLookup
{
    public int? SalesGroupId { get; set; }
    public TasafSalesGroup? SalesGroup { get; set; }
    int? ITasafParentLookup.ParentId { get => SalesGroupId; set => SalesGroupId = value; }
    /// <summary>
    /// Customer-name keywords (comma-separated, SQL LIKE fragments) that pick this chain's stores
    /// from the customer master for the TASAF Cust Key dropdown. Empty = no filter (search all).
    /// </summary>
    public string CustomerMatch { get; set; } = string.Empty;
    string ITasafTextLookup.Text { get => CustomerMatch; set => CustomerMatch = value; }
}

/// <summary>Area covered (GMA / NCR, West Visayas, ...).</summary>
public class TasafArea : TasafLookup, ITasafTextLookup
{
    /// <summary>
    /// Branch (Site) codes in this area, comma-separated (e.g. "DAG-B, ISA-B, MEX-B"). Picks the
    /// area's stores for the TASAF Cust Key dropdown. Empty = no filter (all customers).
    /// </summary>
    public string BranchCodes { get; set; } = string.Empty;
    string ITasafTextLookup.Text { get => BranchCodes; set => BranchCodes = value; }
}

public class TasafProgramType : TasafLookup, ITasafParentLookup, ITasafAmountLookup
{
    /// <summary>Which approval matrix routes this program type (Promo / Near Ex, or Account Support).</summary>
    public int? ApprovalTypeId { get; set; }
    public TasafApprovalType? ApprovalType { get; set; }
    /// <summary>Highest CTS (% of promoted value) allowed to submit, e.g. 8 for promo packs. Null = no limit.</summary>
    public decimal? MaxCtsPercent { get; set; }
    int? ITasafParentLookup.ParentId { get => ApprovalTypeId; set => ApprovalTypeId = value; }
    decimal? ITasafAmountLookup.Amount { get => MaxCtsPercent; set => MaxCtsPercent = value; }
}

/// <summary>
/// One of the approval matrices ("Data Fields for IT.xlsx" › TASAF): 1 Promo / Near Ex
/// Moveout (by amount), 2 Listing Fees, Conventions, Other Account Support (always NSD + COO).
/// Its per-role rules are in TasafApprovalRule.
/// </summary>
public class TasafApprovalType : TasafLookup { }

public class TasafObjective : TasafLookup { }
public class TasafTargetMarket : TasafLookup { }
public class TasafChannel : TasafLookup { }
public class TasafProductGroup : TasafLookup { }

/// <summary>Cost Details description, e.g. "Promotions - Free Goods".</summary>
public class TasafCostType : TasafLookup { }

/// <summary>
/// One approver role in the "Simplified Approval Matrix" (GKAM, TMM, NSM, NSD, COO);
/// signing order = SortOrder. When a role applies is per approval type (TasafApprovalRule);
/// who holds it is per sales group (TasafApprover).
/// </summary>
public class TasafApprovalRole : TasafLookup { }

/// <summary>
/// Approval rule: role RoleId signs TASAFs of approval type ApprovalTypeId — always
/// (OverAmount null) or only when the total cost is MORE THAN OverAmount.
/// No row = the role is not required for that type.
/// </summary>
public class TasafApprovalRule
{
    public int Id { get; set; }
    public int ApprovalTypeId { get; set; }
    public TasafApprovalType? ApprovalType { get; set; }
    public int RoleId { get; set; }
    public TasafApprovalRole? Role { get; set; }
    public decimal? OverAmount { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>Approval matrix cell: who holds a role for a sales group. UserId null = vacant (step is skipped).</summary>
public class TasafApprover
{
    public int Id { get; set; }
    public int SalesGroupId { get; set; }
    public TasafSalesGroup? SalesGroup { get; set; }
    public int RoleId { get; set; }
    public TasafApprovalRole? Role { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
