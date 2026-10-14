namespace HOMSys.Application.Interfaces;

/// <summary>
/// An email copy of a notification. LinkPath is the in-app route, e.g. "/tasafs?id=42". Html, when
/// given, is the whole body (e.g. the full TASAF); "{{WEB}}" in it becomes the site address.
/// </summary>
public record NotificationEmail(string To, string ToName, string Title, string Message, string? LinkPath, string? Html = null);

/// <summary>
/// Fire-and-forget email outbox: Enqueue returns at once and a background sender delivers it
/// (HOMSys.API EmailSenderService), so a slow or unreachable mail server never delays a
/// request. Dropped when email is not configured (Email:Enabled / ConnectionString / FromAddress).
/// </summary>
public interface IEmailQueue
{
    void Enqueue(NotificationEmail message);
}
