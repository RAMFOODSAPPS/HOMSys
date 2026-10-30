namespace HOMSys.Domain.Entities;

/// <summary>
/// A posted BMS invoice, mirrored from the branch EOD SL{WH}{MMYY}.DBF (DOCID=1).
/// Total = SUM(AMT + STAX) per (WhseNo, DocNo); RfcTotal = SUM(AMT + STAX) of DOCID=6 rows with
/// REFID=1 and REFNO = DocNo (already negative in BMS). Never derived from arof4win.
/// </summary>
public class PostedInvoice
{
    public int Id { get; set; }

    /// <summary>Branch warehouse from the SL file name (SL{WH}{MMYY}.DBF) — the branch identity.
    /// The rows' own WHSENO is a different (customer-side) warehouse and is ignored.</summary>
    public int WhseNo { get; set; }
    public int DocNo { get; set; }
    public DateOnly DocDate { get; set; }

    /// <summary>Resolved from WhseNo at sync time; null until a mapping exists.</summary>
    public int? SiteId { get; set; }
    public Site? Site { get; set; }

    public string CustKey { get; set; } = string.Empty;
    public string CKey { get; set; } = string.Empty;
    public string CustName { get; set; } = string.Empty;
    public int Term { get; set; }
    public string? PoNum { get; set; }

    /// <summary>The SL rows' SALESMAN, EMPNO and WHSENO (customer-side warehouse), copied onto the HOMS OR/RPDC
    /// lines HOMSys writes. 0 for invoices fetched before these were synced (CustKey's first two digits stand in).</summary>
    public int Salesman { get; set; }
    public int EmpNo { get; set; }
    public int CustWhseNo { get; set; }

    public decimal Total { get; set; }

    /// <summary>Signed sum of posted RFCs against this invoice (zero or negative).</summary>
    public decimal RfcTotal { get; set; }

    public string SourceFile { get; set; } = string.Empty;
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CollectionInvoice> Allocations { get; set; } = [];
}
