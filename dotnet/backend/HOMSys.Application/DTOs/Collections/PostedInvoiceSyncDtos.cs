namespace HOMSys.Application.DTOs.Collections;

/// <summary>One SL{WH}{MMYY}.DBF as aggregated by LegacyMasterWatcher: invoices (DOCID=1) and RFC sums (DOCID=6, REFID=1).</summary>
public record PostedInvoiceSyncRequest(
    int Wh,
    string SourceFile,
    List<PostedInvoiceSyncItem>? Invoices,
    List<PostedRfcSyncItem>? Rfcs,
    // Targeted (on-demand) sync: only these invoice numbers were scanned for, so only their
    // RFC rows in this file may be replaced. Null = the whole file was read (replace all).
    List<int>? ScopeInvoiceNos = null);

/// <summary>Total = SUM(AMT + STAX) (VAT-inclusive) of the invoice's DOCID=1 rows.
/// Salesman / EmpNo / CustWhseNo = the rows' SALESMAN, EMPNO, WHSENO (copied onto the HOMS OR/RPDC lines).</summary>
public record PostedInvoiceSyncItem(
    int DocNo,
    DateOnly DocDate,
    string CustKey,
    string CKey,
    string? CustName,
    int Term,
    string? PoNum,
    decimal Total,
    int Salesman = 0,
    int EmpNo = 0,
    int CustWhseNo = 0);

/// <summary>Signed SUM(AMT + STAX) of one RFC's rows (SL DOCID=6, DOCNO = RfcNo) against an invoice.
/// RfcNo 0 = an older watcher that sent one sum per invoice.</summary>
public record PostedRfcSyncItem(int InvoiceNo, decimal Amt, int RfcNo = 0, DateOnly? RfcDate = null);

public record PostedInvoiceSyncResult(int Inserted, int Updated, int Unchanged, int RfcRows, int RfcInvoicesRecomputed, bool SiteResolved)
{
    public override string ToString() =>
        $"invoices +{Inserted} ~{Updated} ={Unchanged}; rfc rows {RfcRows}, invoices re-totalled {RfcInvoicesRecomputed}; site {(SiteResolved ? "resolved" : "NOT resolved")}";
}
