namespace HOMSys.Application.Interfaces;

/// <summary>An email copy of a notification. LinkPath is the in-app route, e.g. "/tasafs?id=42".</summary>
public record NotificationEmail(string To, string ToName, string Title, string Message, string? LinkPath);

/// <summary>
/// Fire-and-forget email outbox: Enqueue returns at once and a background sender delivers it
/// (HOMSys.API EmailSenderService), so a slow or unreachable mail server never delays a
/// request. Dropped when email is not configured (Email:Enabled / ConnectionString / FromAddress).
/// </summary>
public interface IEmailQueue
{
    void Enqueue(NotificationEmail message);
}
