namespace HOMSys.Domain.Entities;

public enum InvoiceSyncStatus { Pending, Processing, Done, Failed }

/// <summary>
/// A branch's request to pull specific invoices (and their RFCs) from the EOD SL files into
/// HOMSys. Azure can't reach F:\, so LegacyMasterWatcher.exe polls for Pending requests,
/// reads only the SL{WH}{MMYY}.DBF files those invoices live in, and reports back.
/// </summary>
public class InvoiceSyncRequest
{
    public int Id { get; set; }
    public int SiteId { get; set; }
    public Site Site { get; set; } = null!;

    /// <summary>Branch warehouse (the WH in the SL file name), resolved from the Site at request time.</summary>
    public int Wh { get; set; }

    public InvoiceSyncStatus Status { get; set; } = InvoiceSyncStatus.Pending;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the watcher claimed it; a Processing request older than the stale window goes back to Pending.</summary>
    public DateTime? ClaimedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Message { get; set; }

    public ICollection<InvoiceSyncRequestItem> Items { get; set; } = [];
}

public class InvoiceSyncRequestItem
{
    public int Id { get; set; }
    public int InvoiceSyncRequestId { get; set; }
    public InvoiceSyncRequest Request { get; set; } = null!;

    public int InvoiceNo { get; set; }

    /// <summary>Invoice date as typed by the custodian — selects the SL{WH}{MMYY} file.</summary>
    public DateOnly InvoiceDate { get; set; }

    /// <summary>Null until the watcher reports; true when the invoice was found in the SL file.</summary>
    public bool? Found { get; set; }
}
