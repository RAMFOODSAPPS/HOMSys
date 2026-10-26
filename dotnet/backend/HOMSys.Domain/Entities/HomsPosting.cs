namespace HOMSys.Domain.Entities;

/// <summary>Or = CR into OH03/OD03; Rpdc = PR/AR into GH03/GD03 (DR 10552 / CR 10301);
/// PdcJv = matured PDC's JV into GH03/GD03 (DR cash in bank + 10507 / CR 10552).</summary>
public enum HomsPostingKind { Or, Rpdc, PdcJv }

/// <summary>Held = needs a fix in HOMSys first (reason in Message); Cancelled = the collection was voided before it was written.</summary>
public enum HomsPostingStatus { Pending, Processing, Done, Held, Failed, Cancelled }

/// <summary>
/// Outbox row: one HOMS document to write into F:\HOMS\RAM\ARCHIVES\{Table}{MonthKey}.dbf (header) and its detail pair
/// (OH→OD, GH→GD), queued by a Finance Verifier action. The server builds every field value (HeaderJson / LinesJson:
/// field name → value); the HO watcher only appends them, so the accounting stays in HOMSys.
/// </summary>
public class HomsPosting
{
    public int Id { get; set; }
    public int CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;

    public HomsPostingKind Kind { get; set; }
    public HomsPostingStatus Status { get; set; } = HomsPostingStatus.Pending;

    /// <summary>"OH03" or "GH03" — the detail file is OD03 / GD03.</summary>
    public string Table { get; set; } = string.Empty;
    /// <summary>MMYY of the posting date (the month file it goes into).</summary>
    public string MonthKey { get; set; } = string.Empty;
    public int DocId { get; set; }
    /// <summary>Null for a JV until the watcher claims the next number from HOMS DOCNUM.</summary>
    public int? DocNo { get; set; }

    public string HeaderJson { get; set; } = "{}";
    public string LinesJson { get; set; } = "[]";

    /// <summary>The file it was written to ("OH031026.dbf").</summary>
    public string? File { get; set; }
    public string? Message { get; set; }
    public int Attempts { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ClaimedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
