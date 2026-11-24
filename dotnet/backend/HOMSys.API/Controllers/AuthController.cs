using System.Security.Claims;
using HOMSys.Application.DTOs.Auth;
using HOMSys.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await authService.LoginAsync(request);
        if (result is null)
            return Unauthorized(new { success = false, message = "Invalid credentials." });

        Response.Cookies.Append("refreshToken", result.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        return Ok(new { success = true, mustChangePassword = result.MustChangePassword, data = result });
    }

    /// <summary>
    /// Forgot password: emails a temporary password to the user's address. The answer is the same whether or not the
    /// username exists; only "email is not set up" is reported.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(new { success = false, message = "Enter your username (Employee ID)." });
        if (!await authService.ForgotPasswordAsync(request.Username))
            return Ok(new { success = false, message = "Password reset by email is not available. Please contact your system administrator." });
        return Ok(new { success = true, message =
            "If this username has an email address in HOMSys, a temporary password has been sent to it. " +
            "It works for 24 hours; your current password keeps working until you use it." });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(new { success = false, message = "Invalid or expired refresh token." });

        var result = await authService.RefreshAsync(refreshToken);
        if (result is null)
            return Unauthorized(new { success = false, message = "Invalid or expired refresh token." });

        Response.Cookies.Append("refreshToken", result.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        return Ok(new { success = true, data = result });
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var error = await authService.ChangePasswordAsync(userId, request);
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true, message = "Password changed successfully." });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (!string.IsNullOrEmpty(refreshToken))
            await authService.LogoutAsync(refreshToken);

        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None
        });
        return Ok(new { success = true, message = "Logged out successfully." });
    }
}
