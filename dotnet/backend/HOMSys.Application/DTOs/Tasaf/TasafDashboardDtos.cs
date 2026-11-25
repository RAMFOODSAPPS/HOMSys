namespace HOMSys.Application.DTOs.Tasaf;

/// <summary>TASAF Dashboard (budget dashboard) for one year: the "By Sales Group" and "By Account Chain" views.</summary>
public class TasafDashboardDto
{
    public int Year { get; set; }
    /// <summary>Years with TASAFs or budgets (plus this year), newest first.</summary>
    public List<int> Years { get; set; } = [];
    /// <summary>May enter budgets (tasaf-masters).</summary>
    public bool CanEditBudget { get; set; }
    public List<TasafDashboardRowDto> BySalesGroup { get; set; } = [];
    public List<TasafDashboardRowDto> ByChain { get; set; } = [];
}

/// <summary>
/// One dashboard row. ChainId and AreaId null = a whole sales group. Created = every TASAF but Draft / Cancelled; Approved =
/// Approved + Completed; Completed = Completed, valued at their approved TPARs; Closed = after post-evaluation (not built
/// yet, so 0); Balance = Budget − Approved amount. A TASAF with several chains (or areas) splits its amount evenly among them.
/// </summary>
public class TasafDashboardRowDto
{
    public int SalesGroupId { get; set; }
    public string SalesGroup { get; set; } = string.Empty;
    public int? ChainId { get; set; }
    public string? Chain { get; set; }
    /// <summary>Set on the area rows of a group shown by area (LKA) in the sales-group view.</summary>
    public int? AreaId { get; set; }
    public string? Area { get; set; }
    /// <summary>The budget entered for this row (null = none).</summary>
    public decimal? BudgetEntered { get; set; }
    /// <summary>Budget used: entered, else (sales group row) its chains' budgets added up.</summary>
    public decimal Budget { get; set; }
    public int CreatedCount { get; set; }
    public decimal CreatedAmount { get; set; }
    public int ApprovedCount { get; set; }
    public decimal ApprovedAmount { get; set; }
    public int CompletedCount { get; set; }
    public decimal CompletedAmount { get; set; }
    public int ClosedCount { get; set; }
    public decimal ClosedAmount { get; set; }
    public decimal Balance { get; set; }
}

public class SaveTasafBudgetsDto
{
    public int Year { get; set; }
    public List<TasafBudgetItemDto> Items { get; set; } = [];
}

/// <summary>Amount null = remove that budget.</summary>
public class TasafBudgetItemDto
{
    public int SalesGroupId { get; set; }
    public int? ChainId { get; set; }
    public int? AreaId { get; set; }
    public decimal? Amount { get; set; }
}
