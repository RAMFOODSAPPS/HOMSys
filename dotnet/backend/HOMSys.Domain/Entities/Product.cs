namespace HOMSys.Domain.Entities;

/// <summary>
/// Product reference data, seeded from the BMS <c>prod4win.DBF</c>.
/// Read-only in HOMSys — maintained in BMS.
///
/// Only the columns the encode flow uses are imported; prod4win has 128.
/// </summary>
public class Product
{
    public int Id { get; set; }

    /// <summary>prod4win.CPRODNO C(4) — the code the operator types into the grid.</summary>
    public string CProdNo { get; set; } = string.Empty;

    /// <summary>prod4win.PRODNO N(4) — numeric form of CProdNo.</summary>
    public int ProdNo { get; set; }

    /// <summary>
    /// prod4win.PRODDESC is C(75) but oowkdet.PRODDESC is only C(50).
    /// The legacy save truncates. Keep the full 75 here and truncate on write.
    /// </summary>
    public string ProdDesc { get; set; } = string.Empty;

    public string PackSize { get; set; } = string.Empty;

    /// <summary>Pieces per case. Divisor for quantity normalisation — never let this be 0.</summary>
    public int Pieces { get; set; }

    public int QtyPerPc { get; set; }
    public int InnerQty { get; set; }

    public string Um { get; set; } = string.Empty;

    public int Supplier { get; set; }

    /// <summary>Shown in red in the legacy grid when false.</summary>
    public bool PriceList { get; set; }

    public decimal TaxRate { get; set; }

    /// <summary>
    /// prod4win.NEWPRICE N(8,2) — national base price, ex-VAT. Sourced from
    /// C:\claude\pricing\pmdm\prod4win.dbf (the Pricing Adjustment subsystem's
    /// staging copy), not the legacy\dbf snapshot — see PricingDataImporter.
    /// Null until the pricing masters import has run at least once.
    /// </summary>
    public decimal? NewPrice { get; set; }

    /// <summary>prod4win.FROM — effectivity date of NewPrice.</summary>
    public DateOnly? PriceFrom { get; set; }

    /// <summary>prod4win.OLDPRICE1 — previous base price, for reference only.</summary>
    public decimal? OldPrice1 { get; set; }

    /// <summary>prod4win.SRP — suggested retail price.</summary>
    public decimal? Srp { get; set; }

    /// <summary>prod4win.CATEGORY C(4) — FK into ProductCategory.CategoryCode.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>prod4win.BARCODE C(16) — per-unit barcode.</summary>
    public string Barcode { get; set; } = string.Empty;

    /// <summary>prod4win.CBARCODE C(18) — per-case barcode.</summary>
    public string CaseBarcode { get; set; } = string.Empty;

    /// <summary>prod4win.BRAND C — used with SBrand for the pricelist "has a brand" gate.</summary>
    public string Brand { get; set; } = string.Empty;

    /// <summary>prod4win.SBRAND C — sub-brand.</summary>
    public string SBrand { get; set; } = string.Empty;

    /// <summary>prod4win.PHOUT L — true once the SKU is phased out.</summary>
    public bool PhOut { get; set; }

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}
