namespace HOMSys.Domain.Entities;

/// <summary>One bank deposit slip covering one or more collections. Bank-statement matching is done at batch level.</summary>
public class DepositBatch
{
    public int Id { get; set; }
    public int SiteId { get; set; }
    public Site Site { get; set; } = null!;

    /// <summary>The company account deposited into; Bank is its label ("BDO 0012...") kept for display.</summary>
    public int? BankAccountId { get; set; }
    public BankAccount? BankAccount { get; set; }
    public string Bank { get; set; } = string.Empty;
    public DateOnly DepositDate { get; set; }
    /// <summary>Optional (user 2026-10-15): empty = no reference; such a deposit is never shared by another collection.</summary>
    public string SlipRefNo { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }

    /// <summary>The bank-statement credit this slip was matched to (BankStatementLine.Id).</summary>
    public int? MatchedBankLineId { get; set; }
    public DateTime? MatchedAt { get; set; }
    /// <summary>Set when a Finance Verifier approves the match (a manual match by finance is approved at once).
    /// Until then an automatic match is only a suggestion and the collections stay Collected.</summary>
    public DateTime? MatchConfirmedAt { get; set; }
    public string? MatchConfirmedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;

    public ICollection<Collection> Collections { get; set; } = [];
}
