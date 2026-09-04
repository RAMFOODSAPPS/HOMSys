namespace HOMSys.Domain.Entities;

/// <summary>
/// Customer PO log, seeded from the BMS <c>pofiles.DBF</c> and appended to on save.
///
/// This is the "PONUM" box in the flow chart. It exists to answer one question:
/// has this customer PO number been encoded before?
///
/// Duplicates are legal. The legacy form warns and lets the operator continue —
/// see <c>txtPonum.Valid</c> in ANALYSIS.md — so PoNum is indexed but NOT unique.
/// </summary>
public class PoLog
{
    public int Id { get; set; }

    /// <summary>pofiles.PONUM C(15).</summary>
    public string PoNum { get; set; } = string.Empty;

    public DateOnly? PoDate { get; set; }

    /// <summary>
    /// The BMS sales order number. Null for rows created by HOMSys — the Python
    /// bridge fills it when the order is pushed, same as SalesOrder.SoNo.
    /// </summary>
    public int? SoNo { get; set; }

    /// <summary>HOMSys order this row was created for. Null for seeded rows.</summary>
    public int? SoId { get; set; }
    public SalesOrder? SalesOrder { get; set; }

    public DateOnly? OrderDate { get; set; }

    public string CustKey { get; set; } = string.Empty;
    public string CusName { get; set; } = string.Empty;

    /// <summary>True for rows imported from pofiles.DBF, false for rows HOMSys created.</summary>
    public bool IsSeeded { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
