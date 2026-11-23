namespace HOMSys.Domain.Entities;

/// <summary>
/// A supporting-document type for collections ("What is it?" on an uploaded file), maintained by the Finance Manager
/// (user 2026-11-23). CollectionAttachment.Kind holds its Code, so the Code never changes once made; the label can.
/// </summary>
public class CollectionDocumentKind
{
    public int Id { get; set; }
    /// <summary>Stored on the files (CollectionAttachment.Kind); fixed once created.</summary>
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>Receipt types it is offered for, comma-separated ("CR,PR"); blank = every receipt type. The receipt's own
    /// copy is offered only for its type (Collection Receipt for a CR; a PR / AR hides it).</summary>
    public string ReceiptTypes { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Used by the code itself (Service Invoice scans, the Other fallback): can't be deleted or deactivated.</summary>
    public bool IsSystem { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
