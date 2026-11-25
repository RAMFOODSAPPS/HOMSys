namespace HOMSys.Domain.Entities;

/// <summary>
/// Approved annual trade budget for the TASAF Dashboard (user decision 2026-11-24): per year and sales group (ChainId / AreaId null),
/// per year and account chain, and per year and area for a group shown by area (LKA). A sales group with no budget of its own shows the total of its chains' budgets.
/// </summary>
public class TasafBudget
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int SalesGroupId { get; set; }
    public TasafSalesGroup? SalesGroup { get; set; }
    /// <summary>Null (with AreaId null) = the sales group's own budget.</summary>
    public int? ChainId { get; set; }
    public TasafChain? Chain { get; set; }
    /// <summary>An area of a sales group shown by area on the dashboard (LKA); null otherwise.</summary>
    public int? AreaId { get; set; }
    public TasafArea? Area { get; set; }
    public decimal Amount { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}
