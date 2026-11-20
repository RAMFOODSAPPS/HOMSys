namespace HOMSys.Domain.Entities;

/// <summary>
/// A posted BMS invoice. Since 2026-11-12 every invoice comes from HOMS's A/R open file F:\HOMS\RAM\AROF4HO.DBF
/// (SourceFile "AROF4HO", ArofInvoiceSyncImporter): DOCID 1, Total = DRAMT − CRAMT, RfcTotal = its DOCID 6 / REFID 1
/// rows. Before that (and still for a Remittance Advice's "Fetching" invoices not in AROF4HO yet) it came from the
/// branch EOD SL{WH}{MMYY}.DBF: Total = SUM(AMT + STAX), RfcTotal = SUM(AMT + STAX) of DOCID=6 rows with REFID=1 and
/// REFNO = DocNo. Once AROF4HO holds an invoice, its SL copy is never applied again (AROF4HO wins).
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

    /// <summary>HOMS's own open balance (AROF4HO DOCBAL): after every OR, RFC and memo posted in HOMS, including ones
    /// never encoded in HOMSys. Null = not from AROF4HO. Used to leave invoices HOMS has closed out of the
    /// customer's open-invoice list; HOMSys's own balance (Total + RfcTotal − its collections) is unchanged.</summary>
    public decimal? HomsBalance { get; set; }

    public string SourceFile { get; set; } = string.Empty;
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CollectionInvoice> Allocations { get; set; } = [];
}
