namespace HOMSys.Domain.Entities;

/// <summary>
/// A POSTED BMS RFC (Return From Customer, imtr_hdr DOCTYPE="RFC", STATUS="2")
/// referencing this order's invoice (REFTYPE1="INVOICE", REFNO1=InvNo). In
/// practice created by c1110k2.scx's toRFC button, which moves an approved RSR
/// (Request for Stock Returns) into an auto-posted RFC. Nothing on the header
/// says whether the whole invoice came back, so full vs partial is decided by
/// quantities — see
/// SalesOrderBridgeService.SyncRfcsAsync. Synced by the bridge as a full
/// snapshot per invoice (replace-if-different); never written from the UI.
/// </summary>
public class SalesOrderRfc
{
    public int Id { get; set; }

    public int SoId { get; set; }
    public SalesOrder SalesOrder { get; set; } = null!;

    /// <summary>imtr_hdr.DOCNO — the RFC number.</summary>
    public int RfcNo { get; set; }

    /// <summary>imtr_hdr.REFNO1 — the invoice this RFC returns against.</summary>
    public int InvNo { get; set; }

    /// <summary>imtr_hdr.REFNO3 when REFTYPE3="CRSR" — the approved RSR (Request for
    /// Stock Returns, c1110k2) this RFC was moved from by the toRFC button.</summary>
    public int? RsrNo { get; set; }

    public DateOnly? RfcDate { get; set; }
    public DateOnly? PostedDate { get; set; }
    public string? UserName { get; set; }

    /// <summary>imtr_hdr.REMARKS C(30) and REMARKS2 C(200).</summary>
    public string? Remarks { get; set; }
    public string? Remarks2 { get; set; }

    public DateTime SyncedAt { get; set; }

    public ICollection<SalesOrderRfcLine> Lines { get; set; } = new List<SalesOrderRfcLine>();
}

/// <summary>
/// One imtr_det row of a posted RFC. Raw BMS amount fields are kept as-is for
/// future reports: SPAMT = selling amount EX-VAT, TAX = its 12% VAT, AMT = COST
/// (verified on RFC 87139572: 1599.10 + 191.89). "RFC Amount" = SpAmt + Tax,
/// VAT-inclusive like SalesOrder.InvAmt.
/// </summary>
public class SalesOrderRfcLine
{
    public int Id { get; set; }

    public int RfcId { get; set; }
    public SalesOrderRfc Rfc { get; set; } = null!;

    public string CProdNo { get; set; } = string.Empty;
    public int QtyCs { get; set; }
    public int QtyPc { get; set; }
    public int Pieces { get; set; }

    public decimal SpAmt { get; set; }
    public decimal Amt { get; set; }
    public decimal Tax { get; set; }
    public decimal DiscAmt1 { get; set; }
    public decimal DiscAmt2 { get; set; }

    /// <summary>imtr_det.RETCODE — return reason code (retcode.dbf).</summary>
    public string? RetCode { get; set; }

    /// <summary>imtr_det.RSNO C(8) — reason number entered on the RFC line.</summary>
    public string? RsNo { get; set; }

    /// <summary>imtr_det.REMARKS C(50).</summary>
    public string? Remarks { get; set; }
}
