namespace HOMSys.Application.DTOs.Collections;

/// <summary>Custodian: fetch these invoices (number + invoice date) for a branch.</summary>
public record CreateInvoiceSyncRequestDto(int SiteId, List<InvoiceSyncItemDto> Items);

public record InvoiceSyncItemDto(int InvoiceNo, DateOnly InvoiceDate);

public record InvoiceSyncItemStatusDto(int InvoiceNo, DateOnly InvoiceDate, bool? Found);

public record InvoiceSyncRequestStatusDto(
    int Id, int SiteId, string Status, DateTime RequestedAt, DateTime? CompletedAt, string? Message,
    List<InvoiceSyncItemStatusDto> Items);

/// <summary>What the watcher receives: one pending request to satisfy.</summary>
public record PendingInvoiceSyncDto(int Id, int Wh, List<InvoiceSyncItemDto> Items);

/// <summary>Watcher's final report for a request; Found lists the invoice numbers located in the SL files.</summary>
public record CompleteInvoiceSyncDto(List<int>? FoundInvoiceNos, string? Error);
