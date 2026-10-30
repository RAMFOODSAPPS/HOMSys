namespace HOMSys.Domain.Entities;

/// <summary>
/// Signed SUM(AMT + STAX) of one posted RFC's rows (SL DOCID=6, REFID=1) that point at one invoice,
/// per source file. Kept per file because an RFC can be posted in a later month's SL file
/// than its invoice; PostedInvoice.RfcTotal is the sum across files and RFCs.
/// </summary>
public class PostedRfc
{
    public int Id { get; set; }

    /// <summary>Branch warehouse from the SL file name (same as PostedInvoice.WhseNo).</summary>
    public int WhseNo { get; set; }

    public string SourceFile { get; set; } = string.Empty;

    /// <summary>SL REFNO — the invoice's DOCNO.</summary>
    public int InvoiceNo { get; set; }

    /// <summary>The RFC's own document no. (SL DOCNO of the DOCID=6 rows); 0 = one sum per invoice from an older watcher.</summary>
    public int RfcNo { get; set; }

    public DateOnly? RfcDate { get; set; }

    /// <summary>Signed (RFC amounts are negative in BMS).</summary>
    public decimal Amt { get; set; }

    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
}
