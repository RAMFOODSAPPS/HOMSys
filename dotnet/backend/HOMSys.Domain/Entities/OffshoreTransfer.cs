namespace HOMSys.Domain.Entities;

/// <summary>
/// An offshore order's origin BMS rows, as uploaded by the origin bridge and
/// replayed into ForBranch's BMS — the automated form of the UTIL18 TCODE 31
/// zip (DL..0011/0012/0014). Each JSON column holds raw DBF field -> text
/// values (dbf.reader.decode_record), so the target maps them by field name
/// onto its own live table schema.
/// </summary>
public class OffshoreTransfer
{
    public int SoId { get; set; }
    public SalesOrder SalesOrder { get; set; } = null!;

    /// <summary>One oowkhdr row (object).</summary>
    public string HeaderJson { get; set; } = "{}";

    /// <summary>oowkdet rows (array).</summary>
    public string LinesJson { get; set; } = "[]";

    /// <summary>oowkdis rows (array).</summary>
    public string DiscountsJson { get; set; } = "[]";

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
