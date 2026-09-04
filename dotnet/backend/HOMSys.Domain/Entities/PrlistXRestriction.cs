namespace HOMSys.Domain.Entities;

/// <summary>
/// Mirrors F:\PMDM\PRLISTX.DBF — SKUs fully excluded from the pricelist
/// export for every account, no zone exception (unlike PrlistX2Restriction,
/// which allows specific zones through).
/// </summary>
public class PrlistXRestriction
{
    public int Id { get; set; }

    public string CProdNo { get; set; } = string.Empty;

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}
