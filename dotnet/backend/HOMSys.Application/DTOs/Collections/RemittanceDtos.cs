namespace HOMSys.Application.DTOs.Collections;

/// <summary>Create / update a Draft (or Returned) Remittance Advice header. PaymentMode: Check or BankTransfer.</summary>
public record SaveRemittanceDto(
    string? ChainName, string PaymentMode, string? Bank, string? CheckNo, DateOnly? CheckDate, decimal CheckAmount,
    DepositInfoDto? Deposit, string? Remarks);

/// <summary>A CR's typed OR no. / DCR no. and its (editable) OR date; the rest comes from its RA Excel.</summary>
public record SaveRemittanceCrDto(string? ReceiptNo, string? DcrNo, DateOnly? ReceiptDate);

public record RemittanceListItemDto(
    int Id, string ChainName, string PaymentMode, string? CheckNo, DateOnly? CheckDate, decimal CheckAmount,
    int CrCount, decimal CrTotal, string ReceiptNos, int OpenFlags, string VerificationStatus,
    string? DepositSlipRef, string? BankMatch, string? Homs, string CreatedBy, DateTime CreatedAt, DateTime? SubmittedAt);

public record RemittanceDeductionDto(string Type, decimal Amount, string? Remark);

/// <summary>An RFC posted in BMS against the line's invoice (Amount positive). RfcNo 0 = not known yet (older sync).</summary>
public record RemittanceRfcDto(int RfcNo, DateOnly? RfcDate, decimal Amount);

/// <summary>One invoice block of the RA Excel vs the system invoice. Flags: Fetching, NotFound, GrossDiff, CustDiff,
/// DateDiff, RfcDiff, OverBalance, NetMismatch, Duplicate. Applied = it's in the CR's applied invoices.
/// OpenBalance = what this CR may still apply: total + posted RFCs − every other live receipt on the invoice;
/// AppliedElsewhere names those receipts (e.g. "CR 23897 DAV-B (Draft) 7,606.08").</summary>
public record RemittanceLineDto(
    int Id, int RowNo, int DocNo, int WhseNo, string? SiteCode, string CustKey, DateOnly? DocDate, string? BirSeries,
    decimal Gross, decimal Cewt, decimal RfcBoRtv, decimal Other, decimal Net, List<RemittanceDeductionDto> Deductions,
    int? PostedInvoiceId, decimal? SystemTotal, decimal? SystemRfc, string? SystemCustKey, string? SystemCustName, DateOnly? SystemDocDate,
    List<string> Flags, bool Applied,
    decimal? OpenBalance = null, string? AppliedElsewhere = null, List<RemittanceRfcDto>? Rfcs = null);

/// <summary>Desk: HOMS DEPTCODE from the Excel (32 Luzon, 31 VisMin). ExcelFile = the RA Excel attachment.
/// Deductions = the RA's CEWT + RFC/BO/RTV + other — information only: they stay open on the invoices (as in HOMS).</summary>
public record RemittanceCrDto(
    int Id, string ReceiptNo, int? DcrNo, DateOnly ReceiptDate, int? Desk, string? DeskLabel, decimal Amount,
    decimal Applied, decimal Deductions, decimal Unapplied, string VerificationStatus, string PaymentStatus,
    CollectionAttachmentDto? ExcelFile, List<CollectionAttachmentDto> Attachments, List<RemittanceLineDto> Lines,
    List<string> Missing, List<HomsPostingDto> HomsPostings);

public record RemittanceDto(
    int Id, int SiteId, string SiteCode, string ChainName, string PaymentMode, string? Bank, string? CheckNo, DateOnly? CheckDate,
    decimal CheckAmount, DepositInfoDto? Deposit, MatchedLineDto? BankLine, string VerificationStatus,
    string? ReturnReason, string? VoidReason, string? Remarks,
    string CreatedBy, DateTime CreatedAt, DateTime? SubmittedAt, DateTime? VerifiedAt, string? VerifiedBy,
    decimal CrTotal, List<RemittanceCrDto> Crs, List<CollectionHistoryDto> History, List<string> Missing,
    bool CanEdit, bool CanSubmit, bool CanVerify, bool CanVoid);
