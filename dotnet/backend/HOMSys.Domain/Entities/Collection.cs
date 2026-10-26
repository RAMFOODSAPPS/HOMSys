namespace HOMSys.Domain.Entities;

/// <summary>
/// A customer payment encoded by an Invoice Custodian. Amount is the money received:
/// Amount = SUM(Invoices.AmountApplied) + UnappliedAmount (on-account). Deductions (EWT/2307,
/// discount, ...) are non-cash credits per invoice: an invoice's balance drops by its applied
/// amount plus its deductions. Payment and verification are separate tracks.
/// </summary>
public class Collection
{
    public int Id { get; set; }

    /// <summary>Stable external key for the future BMS / FoxPro write-back.</summary>
    public Guid CollectionKey { get; set; } = Guid.NewGuid();

    public int SiteId { get; set; }
    public Site Site { get; set; } = null!;

    public ReceiptType ReceiptType { get; set; }

    /// <summary>Manually typed by the custodian; unique per (SiteId, ReceiptType).</summary>
    public string ReceiptNo { get; set; } = string.Empty;
    public DateOnly ReceiptDate { get; set; }

    public string CustKey { get; set; } = string.Empty;
    public string CustName { get; set; } = string.Empty;

    /// <summary>Chain account paying several customers' invoices on one receipt (e.g. SM for its branches); CustKey is then the first invoice's.</summary>
    public bool MultiCustomer { get; set; }

    public PaymentMode PaymentMode { get; set; }
    public decimal Amount { get; set; }
    public decimal UnappliedAmount { get; set; }

    /// <summary>Drawee bank of the customer's check (not the deposit bank — see DepositBatch.Bank).</summary>
    public string? Bank { get; set; }
    public string? CheckNo { get; set; }
    public DateOnly? CheckDate { get; set; }

    /// <summary>The deposit slip / bank transfer this money went in with (slip ref, deposit bank, date).</summary>
    public int? DepositBatchId { get; set; }
    public DepositBatch? DepositBatch { get; set; }

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Draft;
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Draft;
    public string? ReturnReason { get; set; }
    public string? VoidReason { get; set; }
    public string? Remarks { get; set; }

    // PDC outcome, tagged by finance after deposit.
    public PdcOutcome? PdcOutcome { get; set; }
    public DateOnly? PdcOutcomeDate { get; set; }
    public string? PdcOutcomeReason { get; set; }
    public string? PdcOutcomeBy { get; set; }

    // Bounced PDC follow-up (user 2026-10-09): Letter of Dishonor -> the customer settles with one new CR
    // covering the full amount, or it's endorsed for legal action once the LOD's deadline has passed.
    public DateOnly? LodIssuedDate { get; set; }
    public string? LodIssuedBy { get; set; }
    /// <summary>Days the customer has to settle, counted from the day they received the LOD.</summary>
    public int? LodSettleDays { get; set; }
    public DateOnly? LodReceivedDate { get; set; }
    public string? LodReceivedBy { get; set; }
    /// <summary>The customer's received copy of the LOD (proof of notice), one of Attachments.</summary>
    public int? LodCopyAttachmentId { get; set; }
    /// <summary>LodReceivedDate + LodSettleDays: unsettled after this day = due for legal action.</summary>
    public DateOnly? LodDueDate { get; set; }
    /// <summary>The replacement CR that settled this bounced PDC in full.</summary>
    public int? SettledByCollectionId { get; set; }
    public Collection? SettledByCollection { get; set; }
    public DateTime? SettledAt { get; set; }
    public string? SettledBy { get; set; }
    public DateOnly? LegalEndorsedDate { get; set; }
    public string? LegalEndorsedBy { get; set; }
    /// <summary>In-house legal or External counsel.</summary>
    public string? LegalEndorsedTo { get; set; }
    public string? LegalRemarks { get; set; }

    /// <summary>When its OR (CR) / RPDC (PDC) was written into the HOMS ARCHIVES files (see HomsPosting).</summary>
    public DateTime? PostedToHomsAt { get; set; }
    public ICollection<HomsPosting> HomsPostings { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerifiedBy { get; set; }

    /// <summary>Set when the CR is part of a Remittance Advice: its invoices come from the RA Excel and it's
    /// submitted / verified / returned / voided only through the RA.</summary>
    public int? RemittanceId { get; set; }
    public Remittance? Remittance { get; set; }
    /// <summary>RA CR: HOMS desk from the Excel (FOR LUZON → 32, FOR VISMIN → 31); null = the site's own.</summary>
    public int? HomsDeptCode { get; set; }
    /// <summary>RA CR: DCR no. typed by the Custodian Verifier (HOMS OR header DCRNO).</summary>
    public int? DcrNo { get; set; }
    public ICollection<RemittanceLine> RemittanceLines { get; set; } = [];

    public ICollection<CollectionInvoice> Invoices { get; set; } = [];
    public ICollection<CollectionDeduction> Deductions { get; set; } = [];
    public ICollection<CollectionAttachment> Attachments { get; set; } = [];
    public ICollection<CollectionHistory> History { get; set; } = [];
}

/// <summary>Amount of a collection applied to one posted invoice.</summary>
public class CollectionInvoice
{
    public int Id { get; set; }
    public int CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;
    public int PostedInvoiceId { get; set; }
    public PostedInvoice PostedInvoice { get; set; } = null!;
    public decimal AmountApplied { get; set; }
}

/// <summary>Non-cash credit against one invoice of the collection (e.g. EWT with its 2307).</summary>
public class CollectionDeduction
{
    public int Id { get; set; }
    public int CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;
    public int PostedInvoiceId { get; set; }
    public PostedInvoice PostedInvoice { get; set; } = null!;
    public DeductionType Type { get; set; }
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
}

/// <summary>Soft copy of the CR / PR / AR (or 2307) in blob storage; only the path is kept here.</summary>
public class CollectionAttachment
{
    public int Id { get; set; }
    public int CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;
    public string BlobPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string UploadedBy { get; set; } = string.Empty;
}

/// <summary>Audit trail: every status change, edit, return, void and PDC tag.</summary>
public class CollectionHistory
{
    public int Id { get; set; }
    public int CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;
    public string Action { get; set; } = string.Empty;
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public string? Reason { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string By { get; set; } = string.Empty;
}
