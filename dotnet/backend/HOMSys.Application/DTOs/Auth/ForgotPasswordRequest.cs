namespace HOMSys.Application.DTOs.Auth;

/// <summary>Login page "Forgot password?": the username (Employee ID) to email a temporary password for.</summary>
public class ForgotPasswordRequest
{
    public string Username { get; set; } = string.Empty;
}
