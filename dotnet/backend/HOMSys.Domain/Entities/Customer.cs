namespace HOMSys.Domain.Entities;

/// <summary>
/// Customer reference data, seeded from the BMS <c>cust4win.DBF</c>.
/// Read-only in HOMSys — maintained in BMS.
///
/// This is the real customer master. <c>CUSTDIR.DBF</c> is only the lookup
/// picker in the legacy form and is deliberately not imported.
/// </summary>
public class Customer
{
    public int Id { get; set; }

    /// <summary>cust4win.CUSTKEY C(7) — the key the operator types.</summary>
    public string CustKey { get; set; } = string.Empty;

    /// <summary>cust4win.CKEY C(5) — substr(CustKey,3,5) in the legacy form.</summary>
    public string CKey { get; set; } = string.Empty;

    public string CusName { get; set; } = string.Empty;

    public string AddrLn1 { get; set; } = string.Empty;
    public string AddrLn2 { get; set; } = string.Empty;

    /// <summary>Delivery address. When non-empty these override AddrLn1/2 as ship-to.</summary>
    public string DelAddrLn1 { get; set; } = string.Empty;
    public string DelAddrLn2 { get; set; } = string.Empty;
    public string DelArea { get; set; } = string.Empty;

    public int WhseNo { get; set; }
    public int CustWhse { get; set; }

    /// <summary>cust4win.SERVEWH — the warehouse that services the order. Falls back to WhseNo when 0.</summary>
    public int ServeWh { get; set; }

    /// <summary>cust4win.DELWHSE — the delivery warehouse.</summary>
    public int DelWhse { get; set; }

    public int Salesman { get; set; }
    public string CsMan { get; set; } = string.Empty;

    /// <summary>Payment term. 0 = cash, which enables the O.R. fields on the form.</summary>
    public int Term { get; set; }
    public int TermDays { get; set; }

    public string CZone { get; set; } = string.Empty;
    public string VatId { get; set; } = string.Empty;
    public string Subd { get; set; } = string.Empty;

    public bool Tpc { get; set; }
    public bool Offshore { get; set; }
    public bool ExBranch { get; set; }

    public int CCode { get; set; }
    public int OldCCode { get; set; }

    /// <summary>Cutover date: on/after this, CCode applies; before it, OldCCode.</summary>
    public DateOnly? IEffDate { get; set; }

    // --- Not used by the encode flow today, imported so the legacy gates can be
    //     switched on later without a migration. See ANALYSIS.md "validcust". ---

    /// <summary>Blocked from invoicing. Legacy form refuses unless an H.O. code is supplied.</summary>
    public bool BlockInv { get; set; }

    /// <summary>Empty TIN is a hard stop in the legacy form.</summary>
    public string Tin { get; set; } = string.Empty;

    /// <summary>Alternate key used by the order-limit lookup (out of scope).</summary>
    public string AliasKey { get; set; } = string.Empty;

    /// <summary>Chain consolidation key used by order limits (out of scope).</summary>
    public string ConsoMax2 { get; set; } = string.Empty;

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}
