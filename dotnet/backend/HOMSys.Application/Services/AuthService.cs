using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HOMSys.Application.DTOs.Auth;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace HOMSys.Application.Services;

public class AuthService(IUserRepository userRepo, IRefreshTokenRepository refreshRepo, IConfiguration config, IUnitOfWork unitOfWork)
{
    private readonly string _jwtKey = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured");
    private readonly string _jwtIssuer = config["Jwt:Issuer"] ?? "HOMSys";
    private readonly string _jwtAudience = config["Jwt:Audience"] ?? "HOMSys";
    private readonly int _accessTokenMinutes = int.Parse(config["Jwt:AccessTokenMinutes"] ?? "15");
    private readonly int _refreshTokenDays = int.Parse(config["Jwt:RefreshTokenDays"] ?? "7");

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var user = await userRepo.GetByUsernameAsync(request.Username);
        if (user is null || !user.IsActive)
            return null;

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return null;

        return await GenerateTokensAsync(user);
    }

    public async Task<AuthResponse?> RefreshAsync(string refreshToken)
    {
        var stored = await refreshRepo.GetByTokenAsync(refreshToken);
        if (stored is null || stored.IsRevoked || stored.ExpiresAt < DateTime.UtcNow)
            return null;

        var user = stored.User;
        if (!user.IsActive)
            return null;

        await unitOfWork.BeginTransactionAsync();
        try
        {
            await refreshRepo.RevokeAsync(refreshToken);
            var tokens = await GenerateTokensAsync(user);
            await unitOfWork.CommitAsync();
            return tokens;
        }
        catch
        {
            await unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task LogoutAsync(string refreshToken)
    {
        await refreshRepo.RevokeAsync(refreshToken);
    }

    private async Task<AuthResponse> GenerateTokensAsync(User user)
    {
        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Key)
            .Distinct()
            .ToList();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        if (!string.IsNullOrEmpty(user.BranchCode))
            claims.Add(new Claim("branch", user.BranchCode));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(_accessTokenMinutes);

        var token = new JwtSecurityToken(
            issuer: _jwtIssuer,
            audience: _jwtAudience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshTokenValue = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        await refreshRepo.CreateAsync(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenValue,
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenDays)
        });

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            ExpiresAt = expiresAt,
            MustChangePassword = user.MustChangePassword,
            User = new UserInfo
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                MustChangePassword = user.MustChangePassword,
                Roles = roles,
                Permissions = permissions
            }
        };
    }
}
