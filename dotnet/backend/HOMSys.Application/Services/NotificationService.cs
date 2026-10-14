using System.Security.Claims;
using HOMSys.Application.DTOs.Notifications;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace HOMSys.Application.Services;

/// <summary>
/// In-app notifications for the topbar bell, each also emailed to the recipient (IEmailQueue — off
/// until Email is configured). Any module can Send; users read their own.
/// </summary>
public class NotificationService(INotificationRepository repo, IUserRepository users, IEmailQueue email, IHttpContextAccessor http)
{
    private int CurrentUserId =>
        int.TryParse(http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    /// <summary>
    /// One notification per distinct recipient; null ids are skipped. emailHtml replaces the email body;
    /// emailRef (e.g. a TASAF number) leads the email subject only: "2026-NKA-SA-01 — TASAF for your approval".
    /// </summary>
    public async Task SendAsync(IEnumerable<int?> userIds, string title, string message,
        string link, string entityType, int? entityId, string? emailHtml = null, string? emailRef = null)
    {
        var rows = userIds.Where(u => u is > 0).Select(u => u!.Value).Distinct()
            .Select(u => new Notification
            {
                UserId     = u,
                Title      = Trim(title, 200),
                Message    = Trim(message, 500),
                Link       = link,
                EntityType = entityType,
                EntityId   = entityId
            })
            .ToList();
        if (rows.Count > 0) await repo.AddRangeAsync(rows);

        // Email copy. Link "/tasafs" + id → "/tasafs?id=42", which the page opens directly.
        var path = entityId is int eid ? $"{link}?id={eid}" : link;
        foreach (var r in rows)
            if (await users.GetByIdAsync(r.UserId) is { IsActive: true } u && !string.IsNullOrWhiteSpace(u.Email))
                email.Enqueue(new NotificationEmail(u.Email.Trim(), DisplayName(u),
                    string.IsNullOrWhiteSpace(emailRef) ? r.Title : $"{emailRef} — {r.Title}", r.Message, path, emailHtml));
    }

    public async Task<NotificationFeedDto> GetFeedAsync(int take = 30) => new()
    {
        UnreadCount = await repo.CountUnreadAsync(CurrentUserId),
        Items = (await repo.GetLatestAsync(CurrentUserId, Math.Clamp(take, 1, 100)))
            .Select(n => new NotificationDto
            {
                Id         = n.Id,
                Title      = n.Title,
                Message    = n.Message,
                Link       = n.Link,
                EntityType = n.EntityType,
                EntityId   = n.EntityId,
                CreatedAt  = n.CreatedAt,
                IsRead     = n.ReadAt != null
            })
            .ToList()
    };

    public Task MarkReadAsync(int id) => repo.MarkReadAsync(CurrentUserId, id);
    public Task MarkAllReadAsync() => repo.MarkAllReadAsync(CurrentUserId);

    private static string DisplayName(User u) =>
        $"{u.FirstName} {u.LastName}".Trim() is { Length: > 0 } n ? n : u.Username;

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max];
}
