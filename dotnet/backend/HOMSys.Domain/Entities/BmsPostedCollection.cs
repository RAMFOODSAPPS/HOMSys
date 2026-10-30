namespace HOMSys.Domain.Entities;

/// <summary>
/// Read-only mirror of a BMS posted collection (OR / RPDC) from the HO archives OH03{MMYY} header,
/// with its OD03 double-entry lines. Synced by LegacyMasterWatcher; HOMSys never writes it back.
/// Totals: ArApplied = Σ(CR−DR) on A/R Trade (10301); Ewt = Σ(DR−CR) on Prepaid CEWT (10507);
/// Received = Σ(DR−CR) on every other account (cash in bank). A receipt balances: Received + Ewt = ArApplied.
/// </summary>
public class BmsPostedCollection
{
    public const int ArAccount = 10301;
    public const int EwtAccount = 10507;

    public int Id { get; set; }
    /// <summary>"OH03{MMYY}" — the month file it came from.</summary>
    public string SourceFile { get; set; } = string.Empty;
    public int DocId { get; set; }
    public int DocNo { get; set; }
    public DateOnly? DocDate { get; set; }
    public int DcrNo { get; set; }
    public DateOnly? DcrDate { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Payor { get; set; } = string.Empty;
    public int TCode { get; set; }
    public DateOnly? Posted { get; set; }
    public string StatDesc { get; set; } = string.Empty;
    public int DeptCode { get; set; }

    /// <summary>Branch from the receipt's (else the first invoice's) 2-digit number prefix.</summary>
    public int? SiteId { get; set; }
    public Site? Site { get; set; }
    public string CustKey { get; set; } = string.Empty;
    public decimal ArApplied { get; set; }
    public decimal Ewt { get; set; }
    public decimal Received { get; set; }
    public string? CheckNo { get; set; }
    public DateOnly? CheckDate { get; set; }
    public string? DraweeBank { get; set; }
    public string? DepBank { get; set; }
    /// <summary>Any line flagged BADCQF (bounced check) in BMS.</summary>
    public bool Bounced { get; set; }
    public bool Cancelled { get; set; }
    /// <summary>Gone from the archive file (deleted in BMS) — kept, never counted.</summary>
    public bool Removed { get; set; }
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The HOMSys collection this posting is (ReceiptNo = DocNo, or same customer + same invoice amounts).</summary>
    public int? CollectionId { get; set; }
    public Collection? Collection { get; set; }
    public string? LinkMethod { get; set; }
    public DateTime? LinkedAt { get; set; }

    public ICollection<BmsPostedCollectionLine> Lines { get; set; } = [];
}

public class BmsPostedCollectionLine
{
    public int Id { get; set; }
    public int BmsPostedCollectionId { get; set; }
    public BmsPostedCollection BmsPostedCollection { get; set; } = null!;
    public int WhseNo { get; set; }
    public string CustKey { get; set; } = string.Empty;
    public string CKey { get; set; } = string.Empty;
    public int RefId { get; set; }
    /// <summary>Invoice number (REFID 1).</summary>
    public int RefNo { get; set; }
    public DateOnly? RefDate { get; set; }
    public decimal DrAmt { get; set; }
    public decimal CrAmt { get; set; }
    public int AcctNo { get; set; }
    public string DraweeBank { get; set; } = string.Empty;
    public string CheckNo { get; set; } = string.Empty;
    public DateOnly? CheckDate { get; set; }
    public bool BadCheck { get; set; }
    public string DepBank { get; set; } = string.Empty;
    public string PaymentId { get; set; } = string.Empty;
    public string RecBank { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public int TrType { get; set; }
}
