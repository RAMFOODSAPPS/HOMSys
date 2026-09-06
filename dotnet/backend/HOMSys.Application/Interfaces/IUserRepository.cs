using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByEmailAsync(string email);
    Task<IEnumerable<User>> GetAllAsync();
    Task<User> CreateAsync(User user);
    Task UpdateAsync(User user);
    Task UpdateLastLoginAsync(int userId, DateTime at);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(string username, string email, int? excludeId = null);
}
