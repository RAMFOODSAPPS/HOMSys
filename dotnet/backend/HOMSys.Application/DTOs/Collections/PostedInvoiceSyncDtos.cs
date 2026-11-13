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

/// <summary>
/// New / changed invoices from HOMS's A/R open file (AROF4HO.DBF), sent by LegacyMasterWatcher (arof_sync.py).
/// One group per invoice no.: its invoice row (null = no DOCID 1 row any more) and its whole RFC set, which replaces
/// what HOMSys holds for that invoice. Removed = invoice nos. no longer in the file at all.
/// </summary>
public record ArofSyncRequest(List<ArofInvoiceGroup>? Groups, List<int>? Removed);

public record ArofInvoiceGroup(int InvoiceNo, ArofInvoice? Invoice, List<ArofRfc>? Rfcs);

/// <summary>Total = DRAMT − CRAMT (VAT-inclusive, the same as SL's SUM(AMT + STAX)); HomsBalance = DOCBAL.</summary>
public record ArofInvoice(
    DateOnly DocDate, string CustKey, string CKey, string? CustName, int Term, string? PoNum, decimal Total,
    int Salesman, int EmpNo, int CustWhseNo, decimal HomsBalance);

/// <summary>One RFC (DOCID 6, REFID 1) against the invoice: its DOCNO, date and signed amount (negative).</summary>
public record ArofRfc(int RfcNo, DateOnly? RfcDate, decimal Amt);

public record ArofSyncResult(int Inserted, int Updated, int Unchanged, int Deleted, int KeptInUse, int RfcRows)
{
    public override string ToString() =>
        $"invoices +{Inserted} ~{Updated} ={Unchanged} -{Deleted} (kept, in use: {KeptInUse}); rfc rows {RfcRows}";
}

public record PostedInvoiceSyncResult(int Inserted, int Updated, int Unchanged, int RfcRows, int RfcInvoicesRecomputed, bool SiteResolved)
{
    public override string ToString() =>
        $"invoices +{Inserted} ~{Updated} ={Unchanged}; rfc rows {RfcRows}, invoices re-totalled {RfcInvoicesRecomputed}; site {(SiteResolved ? "resolved" : "NOT resolved")}";
}
