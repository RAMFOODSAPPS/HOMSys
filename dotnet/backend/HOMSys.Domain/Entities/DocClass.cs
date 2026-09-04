namespace HOMSys.Domain.Entities;

/// <summary>
/// Document classification lookup, mirrors BMS <c>docclass.DBF</c>
/// (\\Acastillano\setup\ADC\BMSRAM). Only 4 rows in practice: blank =
/// "REGULAR TRANSACTION", "1" = "DUS CLEARING", "2" = "DOCUMENTATION",
/// "3" = "AUTO GENERATED".
/// </summary>
public class DocClass
{
    /// <summary>docclass.DOCCLASS C(1). Blank is a valid code (regular transaction).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>docclass.DOCDESC C(20).</summary>
    public string Description { get; set; } = string.Empty;
}
