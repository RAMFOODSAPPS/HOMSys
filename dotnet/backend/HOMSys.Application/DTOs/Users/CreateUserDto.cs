using System.ComponentModel.DataAnnotations;

namespace HOMSys.Application.DTOs.Users;

public class CreateUserDto
{
    [Required, MinLength(3), MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    public int? CompanyId { get; set; }
    public int? DepartmentId { get; set; }
    public int? SiteId { get; set; }
    public string? BranchCode { get; set; }

    public List<int> RoleIds { get; set; } = [];
}
