namespace HOMSys.Domain.Entities;

/// <summary>
/// Mirrors C:\claude\pricing\pmdm\prchst.dbf — used only as the base-price
/// fallback when a product's prod4win.FROM date is still in the future
/// (i.e. NEWPRICE hasn't taken effect yet). See PriceCalculationService
/// (future pass) for the lookup rule: latest row with Effective &lt;= today.
/// </summary>
public class PriceHistory
{
    public int Id { get; set; }

    /// <summary>1-based physical record position in PRCHST.DBF (VFP RECNO()) —
    /// stable row identity used to diff-sync, since (ProdNo, Effective) can
    /// legitimately repeat across rows (confirmed in production data).</summary>
    public int RecNo { get; set; }

    public int ProdNo { get; set; }

    public DateOnly? Effective { get; set; }

    /// <summary>NPAFVAT — price with VAT as of Effective.</summary>
    public decimal NpAfVat { get; set; }

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}
