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

public class AuthService(IUserRepository userRepo, IRefreshTokenRepository refreshRepo, IConfiguration config, IUnitOfWork unitOfWork,
    IEmailQueue email)
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

        if (BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            // They remembered it: a pending emailed temporary password is no longer needed.
            if (user.TempPasswordHash is not null) { ClearTempPassword(user); await userRepo.SavePasswordStateAsync(user); }
        }
        else if (user.TempPasswordHash is { } temp && user.TempPasswordExpiresAt > DateTime.UtcNow)
        {
            if (!BCrypt.Net.BCrypt.Verify(request.Password, temp))
            {
                // Wrong tries while a temporary password is out: after 5 it is cancelled (ask again for a new one).
                if (++user.TempPasswordFailures >= MaxTempPasswordFailures) ClearTempPassword(user);
                await userRepo.SavePasswordStateAsync(user);
                return null;
            }
            // Signed in with the emailed temporary password: it becomes the password, to be changed now.
            user.PasswordHash = temp;
            user.MustChangePassword = true;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = user.Username;
            ClearTempPassword(user);
            await userRepo.SavePasswordStateAsync(user);
        }
        else
            return null;

        await userRepo.UpdateLastLoginAsync(user.Id, DateTime.UtcNow);

        return await GenerateTokensAsync(user);
    }

    private const int MaxTempPasswordFailures = 5;
    private static readonly TimeSpan TempPasswordLife = TimeSpan.FromHours(24);
    private static readonly TimeSpan TempPasswordCooldown = TimeSpan.FromMinutes(5);
    /// <summary>Hash of a throwaway value: hashed for unknown usernames too, so the response time gives nothing away.</summary>
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("not-a-user");

    public static void ClearTempPassword(User user)
    {
        user.TempPasswordHash = null;
        user.TempPasswordExpiresAt = null;
        user.TempPasswordFailures = 0;
    }

    /// <summary>
    /// Forgot password (login page; user request 2026-11-24). Emails an active user with an email address a temporary
    /// password, valid 24 hours. Their current password keeps working until the temporary one is used (then it must be
    /// changed), so a request made by someone else can't lock them out. One request per user per 5 minutes. Says nothing
    /// about whether the username exists. Returns false only when email is not set up (then nothing is changed).
    /// </summary>
    public async Task<bool> ForgotPasswordAsync(string username)
    {
        if (!email.IsEnabled) return false;

        var user = await userRepo.GetByUsernameAsync((username ?? "").Trim());
        var temp = PasswordGenerator.GenerateStrong();
        var hash = BCrypt.Net.BCrypt.HashPassword(temp);
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email) || !user.Email.Contains('@'))
        {
            _ = BCrypt.Net.BCrypt.Verify(temp, DummyHash);
            return true;
        }
        if (user.TempPasswordRequestedAt > DateTime.UtcNow - TempPasswordCooldown) return true;

        user.TempPasswordHash = hash;
        user.TempPasswordExpiresAt = DateTime.UtcNow + TempPasswordLife;
        user.TempPasswordRequestedAt = DateTime.UtcNow;
        user.TempPasswordFailures = 0;
        await userRepo.SavePasswordStateAsync(user);

        var name = $"{user.FirstName} {user.LastName}".Trim() is { Length: > 0 } n ? n : user.Username;
        email.Enqueue(new NotificationEmail(user.Email.Trim(), name, "Your temporary password",
            $"Someone (hopefully you) asked to reset the HOMSys password for {user.Username}. Temporary password: {temp} "
            + "— sign in with it within 24 hours and you will be asked to choose a new one. Your current password still "
            + "works until then; if you didn't ask for this, just ignore this email.",
            "/login", TempPasswordHtml(name, user.Username, temp)));
        return true;
    }

    private static string TempPasswordHtml(string name, string username, string temp)
    {
        static string e(string s) => System.Net.WebUtility.HtmlEncode(s);
        const string web = "{{WEB}}"; // the email sender puts the site address here
        return $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1f1f1f;max-width:560px">
              <div style="border-top:4px solid #800000;padding-top:12px;color:#800000;font-weight:700">HOMSys</div>
              <h2 style="font-size:18px;margin:16px 0 8px">Your temporary password</h2>
              <p style="margin:0">Hi {e(name)},</p>
              <p>Someone (hopefully you) asked to reset the HOMSys password for <strong>{e(username)}</strong>.</p>
              <p style="font-size:20px;font-family:Consolas,monospace;background:#f6eaea;padding:10px 14px;display:inline-block;border-radius:4px">{e(temp)}</p>
              <p>Sign in with it within <strong>24 hours</strong>; you will then be asked to choose a new password.
                 Your current password still works until you use this one.</p>
              <p style="margin:24px 0"><a href="{web}/login" style="background:#800000;color:#fff;padding:10px 18px;border-radius:4px;text-decoration:none;font-weight:600">Sign in to HOMSys</a></p>
              <p style="color:#888;font-size:12px">If you didn't ask for this, ignore this email — nothing changes. Automatic message from HOMSys, please do not reply.</p>
            </div>
            """;
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

    public async Task<string?> ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        var user = await userRepo.GetByIdAsync(userId);
        if (user is null)
            return "User not found.";

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return "Current password is incorrect.";

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        ClearTempPassword(user);
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = user.Username;

        await userRepo.UpdateAsync(user);
        return null;
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
                BranchCode = user.BranchCode,
                Roles = roles,
                Permissions = permissions
            }
        };
    }
}
