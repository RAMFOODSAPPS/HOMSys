using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

public interface INotificationRepository
{
    Task AddRangeAsync(IEnumerable<Notification> notifications);
    Task<List<Notification>> GetLatestAsync(int userId, int take);
    Task<int> CountUnreadAsync(int userId);
    Task MarkReadAsync(int userId, int id);
    Task MarkAllReadAsync(int userId);
}
