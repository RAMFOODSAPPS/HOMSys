namespace HOMSys.Domain.Entities;

/// <summary>
/// Remittance Advice (NKA chains, user 2026-10-15): one customer payment (e.g. SVI check 8700129452) holding 1+ CRs.
/// Each CR is a normal Collection at Head Office whose invoices + deductions come from the chain's RA Excel
/// (one file per OR, see RemittanceLine). The Finance Verifier verifies the RA as a whole; its CRs follow its status.
/// Encoded by the Custodian Verifier (collections-ra-encode, every branch).
/// </summary>
public class Remittance
{
    public int Id { get; set; }

    /// <summary>Head Office: the CRs apply invoices of any branch; their ORs go to the HOMS Luzon / VisMin desk.</summary>
    public int SiteId { get; set; }
    public Site Site { get; set; } = null!;

    /// <summary>CHAIN NAME in the RA Excel (e.g. "SVI").</summary>
    public string ChainName { get; set; } = string.Empty;

    /// <summary>Check or BankTransfer — same rules as a CR.</summary>
    public PaymentMode PaymentMode { get; set; } = PaymentMode.Check;
    /// <summary>Drawee bank, check no. and date of the customer's check (none for a transfer).</summary>
    public string? Bank { get; set; }
    public string? CheckNo { get; set; }
    public DateOnly? CheckDate { get; set; }
    /// <summary>The whole payment; its CRs must add up to it before the RA can be submitted.</summary>
    public decimal CheckAmount { get; set; }

    /// <summary>One deposit slip for the payment; every member CR is put on it.</summary>
    public int? DepositBatchId { get; set; }
    public DepositBatch? DepositBatch { get; set; }

    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Draft;
    public string? ReturnReason { get; set; }
    public string? VoidReason { get; set; }
    public string? Remarks { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerifiedBy { get; set; }

    public ICollection<Collection> Collections { get; set; } = [];
    public ICollection<RemittanceHistory> History { get; set; } = [];
}

/// <summary>
/// One invoice block of a CR's RA Excel, as read, with what HOMSys found for it. The CR's applied invoices are rebuilt
/// from these lines: only lines that match their invoice are applied. A mismatch is corrected in the Excel and the
/// Excel uploaded again (user 2026-10-15: no accepting differences).
/// </summary>
public class RemittanceLine
{
    public int Id { get; set; }
    public int CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;

    /// <summary>Excel row of the block's first line (1-based, as the user sees it).</summary>
    public int RowNo { get; set; }
    public int DocNo { get; set; }
    /// <summary>Warehouse = the invoice no.'s first two digits (the SL file it's in).</summary>
    public int WhseNo { get; set; }
    public string CustKey { get; set; } = string.Empty;
    public DateOnly? DocDate { get; set; }
    public string? BirSeries { get; set; }

    public decimal Gross { get; set; }
    public decimal Cewt { get; set; }
    public decimal RfcBoRtv { get; set; }
    public decimal Other { get; set; }
    public decimal Net { get; set; }
    /// <summary>Each deduction row of the block: [{type, amount, remark}] (JSON).</summary>
    public string DeductionsJson { get; set; } = "[]";

    public int? PostedInvoiceId { get; set; }
    public PostedInvoice? PostedInvoice { get; set; }
    public decimal? SystemTotal { get; set; }
    /// <summary>Returns already posted in BMS for the invoice (positive), netted against the RA's RFC/BO/RTV.</summary>
    public decimal? SystemRfc { get; set; }
    /// <summary>Invoice fetch from HO (InvoiceSyncRequest) while it isn't in HOMSys yet.</summary>
    public int? FetchRequestId { get; set; }

    /// <summary>Comma-separated RemittanceFlags; empty = matched.</summary>
    public string Flags { get; set; } = string.Empty;
}

/// <summary>Why a line doesn't match its invoice. Every flag blocks submit (no accepting differences): correct the RA Excel
/// and upload it again. RfcDiff (the chain's RFC/BO/RTV vs the RFCs posted in BMS) also clears once BMS posts the RFC:
/// HOMSys re-reads the invoice's RFCs from HO. No RFC in BMS for a deducted RTV = the records aren't aligned (user 2026-10-15).</summary>
public static class RemittanceFlags
{
    /// <summary>VatExcluded: the RA gross is the invoice total without its 12% VAT (e.g. SVI's Butuan invoices) —
    /// a GrossDiff with a known cause; the VAT stays open on the invoice.</summary>
    public const string Fetching = "Fetching", NotFound = "NotFound", GrossDiff = "GrossDiff", VatExcluded = "VatExcluded", CustDiff = "CustDiff",
        DateDiff = "DateDiff", RfcDiff = "RfcDiff", OverBalance = "OverBalance", NetMismatch = "NetMismatch", Duplicate = "Duplicate";
}

public class RemittanceHistory
{
    public int Id { get; set; }
    public int RemittanceId { get; set; }
    public Remittance Remittance { get; set; } = null!;
    public string Action { get; set; } = string.Empty;
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public string? Reason { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string By { get; set; } = string.Empty;
}
