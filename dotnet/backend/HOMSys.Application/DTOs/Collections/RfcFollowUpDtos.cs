namespace HOMSys.Application.DTOs.Collections;

/// <summary>An RFC posted in HOMS A/R against the return's invoice. LinkedTo = the receipt another return already took it for.</summary>
public record RfcCandidateDto(int RfcNo, DateOnly? RfcDate, decimal Amount, string? LinkedTo);

/// <summary>
/// A "Return from customer" deduction and its RFC (CNC Verifier follow-up, user 2026-11-24): its receipt's OR / RPDC waits
/// until every return on it has the RFC posted in HOMS A/R. Posting = that OR / RPDC's state ("OR on hold", "RPDC in HOMS").
/// </summary>
public record RfcFollowUpDto(
    int DeductionId, int CollectionId, string Branch, string ReceiptType, string ReceiptNo, DateOnly ReceiptDate,
    string CustKey, string CustName, int InvoiceNo, DateOnly InvoiceDate, decimal Amount, string? Reference,
    string Status, DateTime Since, int DaysWaiting, string? Posting,
    int? RfcNo, DateTime? RfcLinkedAt, string? RfcLinkedBy, List<RfcCandidateDto> Rfcs);

public record LinkRfcDto(int RfcNo);
