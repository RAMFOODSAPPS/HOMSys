namespace HOMSys.Domain.Entities;

public class Site
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Comma-separated cust4win.WHSENO values for this branch (C:\dump\branch.xlsx "cuwhsenos") — used to filter a shared pricing folder's zones down to this branch's own customers. See ZonePricelistExportService.HonBranchWhseNos.</summary>
    public string Cuwhsenos { get; set; } = string.Empty;

    /// <summary>True if this branch has no F:\AUTOPROG\ADDON folder of its own and prices off the shared "hon" ZONE/ZONE2 tables (optionally narrowed by Cuwhsenos). False for independent branches that price off their own ADDON folder.</summary>
    public bool PricesOffHon { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public int? SiteTypeId { get; set; }
    public SiteType? SiteType { get; set; }
}
