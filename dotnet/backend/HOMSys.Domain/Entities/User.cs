namespace HOMSys.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int? SiteId { get; set; }
    public Site? Site { get; set; }

    /// <summary>BMS branch abbreviation (e.g. "luc", "hon") — same codes used by
    /// F:\AUTOPROG\{ADDON,CUSTOMER}\{branch} and CustomerBranchZone.Branch. Null/blank
    /// = HO/unscoped user, sees and can be assigned orders across all branches.</summary>
    public string? BranchCode { get; set; }

    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // Forgot password (login page): an emailed temporary password, valid until it expires, is used, or 5 wrong tries.
    // The current password keeps working meanwhile, so a reset asked for by someone else can't lock the user out.
    public string? TempPasswordHash { get; set; }
    public DateTime? TempPasswordExpiresAt { get; set; }
    public DateTime? TempPasswordRequestedAt { get; set; }
    public int TempPasswordFailures { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
