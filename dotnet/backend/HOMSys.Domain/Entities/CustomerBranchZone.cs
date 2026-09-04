namespace HOMSys.Domain.Entities;

/// <summary>
/// Mirrors F:\AUTOPROG\CUSTOMER\{branch}\cust4win.dbf's CUSTKEY/CZONE columns —
/// HO's own per-branch customer-zone record, used as the pricing-lookup source
/// of truth for CZone (Customer.CZone, sourced from the BMSRAM share, is a
/// single install's copy and may lag branch-side pricing updates).
/// </summary>
public class CustomerBranchZone
{
    public int Id { get; set; }

    /// <summary>1-based physical record position in CUST4WIN.DBF (VFP RECNO()) —
    /// stable row identity used to diff-sync.</summary>
    public int RecNo { get; set; }

    /// <summary>F:\AUTOPROG\CUSTOMER folder name, e.g. "hon", "DAG", "ISA".</summary>
    public string Branch { get; set; } = string.Empty;

    public string CustKey { get; set; } = string.Empty;

    public string CZone { get; set; } = string.Empty;

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}
