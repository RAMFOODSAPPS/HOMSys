using System.ComponentModel.DataAnnotations;

namespace HOMSys.Application.DTOs.Sites;

public class CreateSiteDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    public int CompanyId { get; set; }

    [MaxLength(300)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ContactPerson { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    public int? SiteTypeId { get; set; }
}
