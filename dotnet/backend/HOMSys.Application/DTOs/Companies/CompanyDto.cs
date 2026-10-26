namespace HOMSys.Application.DTOs.Companies;

public class CompanyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool HasLogo { get; set; }
    /// <summary>Logo file size in KB (1 decimal).</summary>
    public decimal? LogoSizeKb { get; set; }
    /// <summary>Changes on every upload (cache-busting ?v= on the logo URL).</summary>
    public long? LogoVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
