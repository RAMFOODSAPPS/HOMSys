namespace HOMSys.Application.DTOs.Tasaf;

/// <summary>A row of the LF Breakdown sheet. Total / Cts are computed by the server (ignored on save).</summary>
public class TasafListingFeeLineDto
{
    public string SalesGroup { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal? LfPerSku { get; set; }
    public int? SkuCount { get; set; }
    public int? DoorCount { get; set; }
    /// <summary>"AllSkus" (TOTAL = LF × Doors) or "AllDoors" (TOTAL = LF × SKUs).</summary>
    public string Basis { get; set; } = "AllSkus";
    public decimal Total { get; set; }
    public decimal? ProjectedAnnualSales { get; set; }
    /// <summary>Fraction (0.33 = 33%).</summary>
    public decimal? Cts { get; set; }
    public string Remarks { get; set; } = string.Empty;
}

/// <summary>A row of the Near Ex sheet. TotalPcs / TotalValue / MoveoutCost are computed by the server.</summary>
public class TasafNearExLineDto
{
    public string SalesGroup { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal? LspPerCase { get; set; }
    public decimal? LspPerPc { get; set; }
    public int? PcsPerCase { get; set; }
    /// <summary>Month-year; any day is saved as the 1st of its month.</summary>
    public DateOnly? Expiry { get; set; }
    public int? InvCases { get; set; }
    public int? InvPieces { get; set; }
    public int TotalPcs { get; set; }
    public decimal TotalValue { get; set; }
    public string MoveoutScheme { get; set; } = string.Empty;
    /// <summary>Fraction (0.5 = 50%).</summary>
    public decimal? Discount { get; set; }
    public decimal MoveoutCost { get; set; }
}

public class SaveTasafWorksheetDto
{
    /// <summary>"ListingFee" or "NearEx"; fixed once created.</summary>
    public string Type { get; set; } = "ListingFee";
    public string Title { get; set; } = string.Empty;
    public List<TasafListingFeeLineDto> ListingFeeLines { get; set; } = [];
    public List<TasafNearExLineDto> NearExLines { get; set; } = [];
}

public class TasafWorksheetDto : SaveTasafWorksheetDto
{
    public int Id { get; set; }
    public string TypeLabel { get; set; } = string.Empty;
    public int ProponentUserId { get; set; }
    public string ProponentName { get; set; } = string.Empty;
    public int? TasafId { get; set; }
    public string? TasafRefNo { get; set; }
    public string? TasafTitle { get; set; }
    public string? TasafStatus { get; set; }
    /// <summary>Listing Fee: Σ TOTAL; Near Ex: Σ Moveout Cost.</summary>
    public decimal Total { get; set; }
    /// <summary>Attached to a TASAF that is For Approval / Approved / Rejected / Cancelled — read-only, can't detach.</summary>
    public bool Locked { get; set; }
    public bool CanEdit { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class TasafWorksheetListItemDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ProponentName { get; set; } = string.Empty;
    public int? TasafId { get; set; }
    public string? TasafRefNo { get; set; }
    public string? TasafTitle { get; set; }
    public string? TasafStatus { get; set; }
    public int LineCount { get; set; }
    public decimal Total { get; set; }
    public bool IsMine { get; set; }
    public bool Locked { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AttachTasafWorksheetDto
{
    public int TasafId { get; set; }
}
