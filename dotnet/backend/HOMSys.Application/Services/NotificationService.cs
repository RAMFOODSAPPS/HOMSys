using System.Security.Claims;
using HOMSys.Application.DTOs.Notifications;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace HOMSys.Application.Services;

/// <summary>In-app notifications for the topbar bell. Any module can Send; users read their own.</summary>
public class NotificationService(INotificationRepository repo, IHttpContextAccessor http)
{
    private int CurrentUserId =>
        int.TryParse(http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    /// <summary>One notification per distinct recipient; null ids are skipped.</summary>
    public async Task SendAsync(IEnumerable<int?> userIds, string title, string message,
        string link, string entityType, int? entityId)
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

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max];
}
