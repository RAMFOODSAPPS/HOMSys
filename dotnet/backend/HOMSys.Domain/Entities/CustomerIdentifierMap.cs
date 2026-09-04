namespace HOMSys.Domain.Entities;

/// <summary>
/// Remembers which CustKey a free-text customer name/identifier on an
/// encoder-supplied PO batch sheet (e.g. "PUREGOLD - ISABELA") resolves to,
/// so the "Import by Customer Name" SO import flow doesn't ask the encoder
/// to re-map the same identifier on every batch.
/// </summary>
public class CustomerIdentifierMap
{
    public int Id { get; set; }

    /// <summary>Free-text customer identifier as written on the PO batch sheet.</summary>
    public string Identifier { get; set; } = string.Empty;

    public string CustKey { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
