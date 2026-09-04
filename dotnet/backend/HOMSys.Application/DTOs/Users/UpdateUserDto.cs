using System.ComponentModel.DataAnnotations;

namespace HOMSys.Application.DTOs.Users;

public class UpdateUserDto
{
    [Required, EmailAddress, MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    public int? CompanyId { get; set; }
    public int? DepartmentId { get; set; }
    public int? SiteId { get; set; }
    public string? BranchCode { get; set; }

    public bool IsActive { get; set; }

    public List<int> RoleIds { get; set; } = [];

    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    public string? NewPassword { get; set; }
}
