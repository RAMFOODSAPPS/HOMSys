namespace HOMSys.Domain.Entities;

/// <summary>
/// Mirrors F:\AUTOPROG\ADDON\{branch}\zonemast.dbf — zone code -> description
/// only (CZONE/CDESC). Small reference table (~121 rows for hon), so it's a
/// plain truncate+reload per branch rather than a RecNo diff. Used only to
/// label the Pricelist by Zone report's zone picker (2026-09-08) — the
/// price-lookup path still doesn't need it (RATE/FIXAMT here are write-path-
/// only concerns in the Pricing Adjustment subsystem).
/// </summary>
public class ZoneMast
{
    public int Id { get; set; }

    public string Branch { get; set; } = string.Empty;

    public string CZone { get; set; } = string.Empty;

    public string CDesc { get; set; } = string.Empty;

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}
