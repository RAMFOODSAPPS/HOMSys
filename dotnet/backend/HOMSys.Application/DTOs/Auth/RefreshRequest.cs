using System.ComponentModel.DataAnnotations;

namespace HOMSys.Application.DTOs.Auth;

public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
