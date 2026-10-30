namespace HOMSys.Domain.Entities;

/// <summary>
/// A supporting computation the proponent fills out and attaches to a TASAF ("PLEASE FILL OUT AND ATTACH TO THE
/// TASAF FOR APPROVAL" — TASA Form_MT version template 2026.xlsx sheets "LF Breakdown" and "Near Ex"). No approval of
/// its own: it is approved with the TASAF it is attached to, and locked while that TASAF is For Approval / Approved.
/// Every typed column stays typed; the computed ones follow the workbook's formulas (TasafWorksheetService).
/// </summary>
public class TasafWorksheet
{
    public int Id { get; set; }
    /// <summary><see cref="TasafWorksheetType"/>.</summary>
    public string Type { get; set; } = TasafWorksheetType.ListingFee;
    public string Title { get; set; } = string.Empty;

    public int ProponentUserId { get; set; }
    public User? ProponentUser { get; set; }
    public string ProponentName { get; set; } = string.Empty;

    /// <summary>The TASAF it is attached to; null = not attached.</summary>
    public int? TasafId { get; set; }
    public Tasaf? Tasaf { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public List<TasafListingFeeLine> ListingFeeLines { get; set; } = [];
    public List<TasafNearExLine> NearExLines { get; set; } = [];
}

public static class TasafWorksheetType
{
    /// <summary>Sheet "LF Breakdown".</summary>
    public const string ListingFee = "ListingFee";
    /// <summary>Sheet "Near Ex".</summary>
    public const string NearEx = "NearEx";

    public static readonly string[] All = [ListingFee, NearEx];
}

/// <summary>How a listing-fee row's TOTAL is computed — the two formulas used on the "LF Breakdown" sheet.</summary>
public static class TasafListingFeeBasis
{
    /// <summary>"Negotiated package rate for all SKUs": TOTAL = LF/SKU × # of Doors.</summary>
    public const string AllSkus = "AllSkus";
    /// <summary>"Negotiated package rate for all doors": TOTAL = LF/SKU × # of SKUs.</summary>
    public const string AllDoors = "AllDoors";

    public static readonly string[] All = [AllSkus, AllDoors];
}

/// <summary>A row of the LF Breakdown sheet.</summary>
public class TasafListingFeeLine
{
    public int Id { get; set; }
    public int WorksheetId { get; set; }
    public int LineNo { get; set; }
    public string SalesGroup { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal? LfPerSku { get; set; }
    public int? SkuCount { get; set; }
    public int? DoorCount { get; set; }
    /// <summary><see cref="TasafListingFeeBasis"/>.</summary>
    public string Basis { get; set; } = TasafListingFeeBasis.AllSkus;
    /// <summary>Computed: LF/SKU × Doors (AllSkus) or × SKUs (AllDoors).</summary>
    public decimal Total { get; set; }
    public decimal? ProjectedAnnualSales { get; set; }
    /// <summary>Computed: Total ÷ Projected Annual Sales (a fraction, 0.33 = 33%); null without sales.</summary>
    public decimal? Cts { get; set; }
    public string Remarks { get; set; } = string.Empty;
}

/// <summary>A row of the Near Ex sheet.</summary>
public class TasafNearExLine
{
    public int Id { get; set; }
    public int WorksheetId { get; set; }
    public int LineNo { get; set; }
    public string SalesGroup { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal? LspPerCase { get; set; }
    public decimal? LspPerPc { get; set; }
    public int? PcsPerCase { get; set; }
    /// <summary>EXPIRY (Month-Year): stored as the 1st of the month.</summary>
    public DateOnly? Expiry { get; set; }
    public int? InvCases { get; set; }
    public int? InvPieces { get; set; }
    /// <summary>Computed: Cases × Pcs/Cs + Pieces.</summary>
    public int TotalPcs { get; set; }
    /// <summary>Computed: Cases × LSP per Case + Pieces × LSP per Pc.</summary>
    public decimal TotalValue { get; set; }
    public string MoveoutScheme { get; set; } = string.Empty;
    /// <summary>Discount / Rebate as a fraction (0.5 = 50%).</summary>
    public decimal? Discount { get; set; }
    /// <summary>Computed: Total Value × Discount.</summary>
    public decimal MoveoutCost { get; set; }
}
