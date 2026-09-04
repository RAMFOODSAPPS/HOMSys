using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token);
    Task CreateAsync(RefreshToken token);
    Task RevokeAsync(string token);
    Task RevokeAllForUserAsync(int userId);
}
