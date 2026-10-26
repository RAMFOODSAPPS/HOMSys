namespace HOMSys.Domain.Entities;

/// <summary>In-app notification (topbar bell). Generic: Link + EntityType/EntityId say what to open.</summary>
public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    /// <summary>Frontend route to open, e.g. "/tasafs".</summary>
    public string Link { get; set; } = string.Empty;
    /// <summary>e.g. "tasaf" — the page receives { tasafId: EntityId } as navigation state.</summary>
    public string EntityType { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
