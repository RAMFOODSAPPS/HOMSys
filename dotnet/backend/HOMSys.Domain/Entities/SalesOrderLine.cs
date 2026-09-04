namespace HOMSys.Domain.Entities;

/// <summary>
/// Sales order detail line. Mirrors the BMS <c>oowkdet.DBF</c> (64 fields).
///
/// Same split as <see cref="SalesOrder"/>: ENCODE-OWNED is written here,
/// BMS-OWNED is downstream state left null for the bridge.
///
/// Pricing and discounts are entirely BMS-owned — this module captures
/// quantities only.
/// </summary>
public class SalesOrderLine
{
    public int Id { get; set; }

    public int SoId { get; set; }
    public SalesOrder SalesOrder { get; set; } = null!;

    /// <summary>oowkdet.DOCNO — set by the Python bridge alongside SalesOrder.DocNo.</summary>
    public int? DocNo { get; set; }

    /// <summary>Line order as encoded. Not an oowkdet column; HOMSys needs stable ordering.</summary>
    public int LineNo { get; set; }

    // ── ENCODE-OWNED ─────────────────────────────────────────────────────────

    /// <summary>The code the operator types.</summary>
    public string CProdNo { get; set; } = string.Empty;
    public int ProdNo { get; set; }

    /// <summary>Denormalised from Product on save. oowkdet.PRODDESC is C(50) — truncate.</summary>
    public string ProdDesc { get; set; } = string.Empty;
    public string PackSize { get; set; } = string.Empty;

    /// <summary>Cases. Normalised on save so QtyPc is always less than Pieces.</summary>
    public int QtyCs { get; set; }

    /// <summary>Loose pieces. Always less than Pieces after normalisation.</summary>
    public int QtyPc { get; set; }

    /// <summary>Pieces per case, denormalised from Product. Divisor — guard against 0.</summary>
    public int Pieces { get; set; }
    public int QtyPerPc { get; set; }
    public string Um { get; set; } = string.Empty;

    public int Supplier { get; set; }
    public string CSupplier { get; set; } = string.Empty;

    public bool PriceList { get; set; }
    public decimal TaxRate { get; set; }
    public int Class { get; set; }
    public bool FreeGoods { get; set; }

    // ── BMS-OWNED — pricing, discounts, fulfilment. Never written by HOMSys. ──

    public int? InvNo { get; set; }

    public decimal? Price { get; set; }
    public decimal? ZPrice { get; set; }
    public decimal? Amt { get; set; }
    public decimal? NetAmt { get; set; }
    public decimal? Taxable { get; set; }
    public decimal? Tax { get; set; }
    public decimal? Cost { get; set; }
    public decimal? Uc { get; set; }
    public decimal? OldAmt { get; set; }
    public decimal? OldCost { get; set; }
    public decimal? F10430 { get; set; }

    public decimal? Discount1 { get; set; }
    public decimal? Discount2 { get; set; }
    public decimal? Discount3 { get; set; }
    public decimal? Discount4 { get; set; }
    public decimal? Discount1S { get; set; }
    public decimal? Discount1C { get; set; }
    public decimal? Discount2S { get; set; }
    public decimal? Discount2C { get; set; }
    public decimal? Discount3S { get; set; }
    public decimal? Discount3C { get; set; }

    public decimal? Cash2S { get; set; }
    public decimal? Cash2C { get; set; }
    public decimal? Free1S { get; set; }
    public decimal? Free1C { get; set; }
    public decimal? FreeQCs1 { get; set; }
    public int? FreeQPc1 { get; set; }
    public decimal? FreeQCs2 { get; set; }
    public int? FreeQPc2 { get; set; }
    public bool? FreeAdd { get; set; }
    public bool? FreeProd { get; set; }
    public bool? AutoFree { get; set; }
    public DateOnly? FgDiscType { get; set; }
    public int? FgQtyCs { get; set; }
    public int? FgQtyPc { get; set; }
    public int? EpCs { get; set; }

    public int? StkFlag { get; set; }
    public int? OrigQtyCs { get; set; }
    public int? OrigQtyPc { get; set; }
    public int? Weight { get; set; }

    public decimal? JobArea { get; set; }
    public int? DpAgeNo { get; set; }
    public string? Batch { get; set; }

    public int? RefSoReq { get; set; }
    public int? RrNo { get; set; }
    public DateOnly? RrDate { get; set; }
    public string? DrpCust { get; set; }
}
