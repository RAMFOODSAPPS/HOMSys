using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class NotificationRepository(AppDbContext db) : INotificationRepository
{
    public async Task AddRangeAsync(IEnumerable<Notification> notifications)
    {
        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync();
    }

    public async Task<List<Notification>> GetLatestAsync(int userId, int take) =>
        await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.Id)
            .Take(take)
            .ToListAsync();

    public async Task<int> CountUnreadAsync(int userId) =>
        await db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null);

    public async Task MarkReadAsync(int userId, int id) =>
        await db.Notifications
            .Where(n => n.Id == id && n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow));

    public async Task MarkAllReadAsync(int userId) =>
        await db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow));
}
