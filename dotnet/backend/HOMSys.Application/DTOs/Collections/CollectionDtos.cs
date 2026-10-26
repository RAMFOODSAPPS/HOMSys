namespace HOMSys.Application.DTOs.Collections;

/// <summary>Who is asking — built by the controller from the JWT.</summary>
/// <remarks>CanConfirmPdc = CNC Verifier: confirms matured PDCs (the only trigger of the PDC's JV).
/// CanEncodeRa = Custodian Verifier: encodes CRs and Remittance Advices for every branch.</remarks>
public record CollectionUser(int Id, string Name, bool CanEncode, bool CanVerify, bool CanManage, bool CanTagPdc = false, bool CanConfirmPdc = false,
    bool CanEncodeRa = false);

public record CollectionSiteDto(int Id, string Code, string Name);

/// <summary>
/// A posted invoice with its open balance: Total + RfcTotal (negative) − Paid, where Paid is the
/// cash applied + deductions of every live collection (not voided, not a bounced PDC).
/// </summary>
public record OpenInvoiceDto(
    int Id, int DocNo, DateOnly DocDate, string CustKey, string CustName, int Term, string? PoNum,
    decimal Total, decimal RfcTotal, decimal Paid, decimal Balance);

/// <summary>Deposit slip. BankAccountId picks the company account; Bank is its label (read-only, filled by the server).</summary>
public record DepositInfoDto(int? BankAccountId, string? Bank, string SlipRefNo, DateOnly DepositDate);

public record SaveAllocationDto(int PostedInvoiceId, decimal AmountApplied);

public record SaveDeductionDto(int PostedInvoiceId, string Type, decimal Amount, string? Reference);

/// <summary>Create / update a Draft (or Returned) collection. Enums travel as their names.</summary>
public record SaveCollectionDto(
    int SiteId,
    string ReceiptType,
    string ReceiptNo,
    DateOnly ReceiptDate,
    string CustKey,
    string PaymentMode,
    decimal Amount,
    string? Bank,
    string? CheckNo,
    DateOnly? CheckDate,
    DepositInfoDto? Deposit,
    string? Remarks,
    List<SaveAllocationDto>? Invoices,
    List<SaveDeductionDto>? Deductions,
    bool MultiCustomer = false);

public record ReasonDto(string? Reason);

public record CollectionListItemDto(
    int Id, int SiteId, string SiteCode, string ReceiptType, string ReceiptNo, DateOnly ReceiptDate,
    string CustKey, string CustName, string PaymentMode, decimal Amount, decimal UnappliedAmount, int InvoiceCount,
    string PaymentStatus, string VerificationStatus, string? PdcState, DateOnly? CheckDate,
    string? DepositSlipRef, string CreatedBy, DateTime CreatedAt, DateTime? SubmittedAt, bool PostedInBms = false,
    string? BankMatch = null,    // null | "Found" (awaiting the verifier) | "Approved"
    string? Homs = null,         // null | "Queued" | "Posted" | "Held" | "Failed" — writing into the HOMS ARCHIVES files
    int? RemittanceId = null);   // part of a Remittance Advice (verified with it)

/// <summary>AvailableBalance = what this collection may still apply (balance with this collection's own lines added back).</summary>
public record CollectionInvoiceDto(
    int PostedInvoiceId, int DocNo, DateOnly DocDate, string CustKey, string CustName,
    decimal Total, decimal RfcTotal, decimal AmountApplied, decimal Balance, decimal AvailableBalance);

public record CollectionDeductionDto(int Id, int PostedInvoiceId, int DocNo, string Type, decimal Amount, string? Reference);

public record CollectionAttachmentDto(int Id, string FileName, string ContentType, long Size, DateTime UploadedAt, string UploadedBy);

public record CollectionHistoryDto(string Action, string? FromStatus, string? ToStatus, string? Reason, DateTime At, string By);

public record CollectionDto(
    int Id, Guid CollectionKey, int SiteId, string SiteCode, string SiteName,
    string ReceiptType, string ReceiptNo, DateOnly ReceiptDate, string CustKey, string CustName,
    string PaymentMode, decimal Amount, decimal UnappliedAmount,
    string? Bank, string? CheckNo, DateOnly? CheckDate, DepositInfoDto? Deposit, bool DepositMatched,
    string PaymentStatus, string VerificationStatus, string? PdcState, string? PdcOutcome,
    DateOnly? PdcOutcomeDate, string? PdcOutcomeReason, string? PdcOutcomeBy,
    string? ReturnReason, string? VoidReason, string? Remarks,
    string CreatedBy, DateTime CreatedAt, DateTime? SubmittedAt, DateTime? VerifiedAt, string? VerifiedBy,
    bool CanEdit, bool CanSubmit, bool CanVoid, bool CanRecordDeposit, bool CanVerify,
    List<CollectionInvoiceDto> Invoices, List<CollectionDeductionDto> Deductions,
    List<CollectionAttachmentDto> Attachments, List<CollectionHistoryDto> History,
    BmsPostingDto? Bms = null,
    bool MultiCustomer = false,
    MatchedLineDto? BankLine = null,
    bool CanConfirmMatch = false,
    List<string>? Missing = null,
    List<HomsPostingDto>? HomsPostings = null,
    bool CanRetryHoms = false,
    int? RemittanceId = null, int? HomsDeptCode = null, int? DcrNo = null,
    bool CanPulloutPdc = false, string? PdcPullout = null, string? PdcPulloutReason = null,
    DateTime? PdcPulloutRequestedAt = null, string? PdcPulloutRequestedBy = null);

/// <summary>Type: "PulledOut" (customer took the check back) or "Replaced" (another check / payment took its place).</summary>
public record PdcPulloutDto(string? Type, string? Reason);

/// <summary>The bank credit the deposit slip is matched to. Confirmed = approved by a Finance Verifier
/// (DepositMatched is true only then); otherwise it's an automatic match awaiting approval.</summary>
public record MatchedLineDto(int Id, string Account, DateOnly TxnDate, decimal Credit, string? Reference, string Description,
    string? Method, bool Confirmed, string? ConfirmedBy);

/// <summary>
/// PDC module row. State: Immature / Matured / Deposited / Succeeded / Bounced.
/// DaysToMaturity is check date − today (negative once matured); NearMaturity = Immature within the window.
/// </summary>
public record PdcItemDto(
    int Id, int SiteId, string SiteCode, string ReceiptType, string ReceiptNo, DateOnly ReceiptDate,
    string CustKey, string CustName, string? Bank, string? CheckNo, DateOnly CheckDate, int DaysToMaturity, decimal Amount,
    string State, bool NearMaturity, string VerificationStatus,
    string? DepositBank, string? DepositSlipRef, DateOnly? DepositDate,
    string? Outcome, DateOnly? OutcomeDate, string? OutcomeReason, string? OutcomeBy, bool CanTag,
    int? BmsDocNo = null, bool BmsBounced = false,
    MatchedLineDto? BankLine = null,     // the bank credit the deposit is matched to (Confirmed = CNC confirmed it)
    string? DepositBy = null,            // who recorded the deposit slip
    bool CanConfirmMatch = false,        // CNC Verifier may confirm / reject the match (posts the JV)
    bool CanRecordDeposit = false,
    PdcDishonorDto? Dishonor = null);    // bounced PDCs only: the LOD -> settlement / legal follow-up

/// <summary>Outcome: Succeeded, Bounced, or None (clears a wrong tag; reason required).</summary>
public record PdcTagDto(string Outcome, DateOnly? Date, string? Reason);

/// <summary>
/// One posted invoice's payment history: its total, RFC returns and every HOMSys collection applied to it.
/// Counts = false for voided receipts and bounced PDCs (they don't reduce the balance). Paid / OpenBalance use the
/// same rule as every other balance in HOMSys (PaidAsync + Balance); only HOMSys collections are included.
/// </summary>
public record InvoiceHistoryDto(
    int Id, int DocNo, DateOnly DocDate, string SiteCode, string CustKey, string CustName, string? PoNum, int Term,
    decimal Total, decimal RfcTotal, List<decimal> Rfcs, List<InvoicePaymentDto> Payments, decimal Paid, decimal OpenBalance);

public record InvoicePaymentDto(
    int CollectionId, string SiteCode, string ReceiptType, string ReceiptNo, DateOnly ReceiptDate, string PaymentMode,
    string? CheckNo, DateOnly? CheckDate, string VerificationStatus, string? PdcState, string CreatedBy,
    decimal Applied, decimal Deductions, List<InvoiceDeductionDto> DeductionLines, bool Counts, string? NotCountedReason,
    string PaymentStatus = "");

public record InvoiceDeductionDto(string Type, decimal Amount, string? Reference);

/// <summary>
/// Bounced PDC follow-up. State: For LOD / LOD Issued / Awaiting Settlement / Overdue / For Legal Action / Settled.
/// CanFollowUp = pdc-tag holder (issues the LOD, records receipt, settlement and legal endorsement).
/// </summary>
public record PdcDishonorDto(
    string State, bool CanFollowUp,
    DateOnly? LodIssuedDate, string? LodIssuedBy, int? LodSettleDays,
    DateOnly? LodReceivedDate, int? LodCopyAttachmentId, DateOnly? LodDueDate,
    int? SettledByCollectionId, string? SettledByReceipt, DateOnly? SettledReceiptDate, string? SettledBy,
    DateOnly? LegalEndorsedDate, string? LegalEndorsedBy, string? LegalRemarks, string? LegalEndorsedTo = null);

public record IssueLodDto(DateOnly? Date, int? SettleDays);
public record SettleBouncedDto(int CollectionId);
/// <summary>EndorsedTo: In-house legal or External counsel (default In-house legal).</summary>
public record EndorseLegalDto(DateOnly? Date, string? Remarks, string? EndorsedTo = null);

/// <summary>A verified CR of the same customer that can settle a bounced PDC (covers its full amount).</summary>
public record SettlementCandidateDto(int Id, string SiteCode, string ReceiptType, string ReceiptNo, DateOnly ReceiptDate,
    string PaymentMode, decimal Amount);

/// <summary>Everything the printed Letter of Dishonor shows.</summary>
public record LodLetterDto(
    string CompanyName, string CompanyAddress, string CompanyPhone, string SiteName, string SiteAddress,
    string CustKey, string CustName, string? CustAddress1, string? CustAddress2,
    string ReceiptType, string ReceiptNo, string? Bank, string? CheckNo, DateOnly CheckDate, decimal Amount,
    DateOnly? DepositDate, DateOnly BouncedDate, string? BounceReason,
    DateOnly IssuedDate, string IssuedBy, int SettleDays);
