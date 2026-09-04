using System.ComponentModel.DataAnnotations;

namespace HOMSys.Application.DTOs.Roles;

public class UpdateRoleDto
{
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}
