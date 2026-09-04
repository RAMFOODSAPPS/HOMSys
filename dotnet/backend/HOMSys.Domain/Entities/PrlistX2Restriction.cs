namespace HOMSys.Domain.Entities;

/// <summary>
/// Mirrors F:\PMDM\PRLISTX2.DBF — SKUs that are hidden from the pricelist
/// export by default, shown only to customers whose CZONE matches one of
/// the allowed zone rows for that CProdNo (one row per allowed zone).
/// </summary>
public class PrlistX2Restriction
{
    public int Id { get; set; }

    public string CProdNo { get; set; } = string.Empty;

    public string Zone { get; set; } = string.Empty;

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}
