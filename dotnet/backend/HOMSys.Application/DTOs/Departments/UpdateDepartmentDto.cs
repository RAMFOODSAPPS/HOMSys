using System.ComponentModel.DataAnnotations;

namespace HOMSys.Application.DTOs.Departments;

public class UpdateDepartmentDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public int CompanyId { get; set; }

    public bool IsActive { get; set; }
}
