namespace HOMSys.Application.DTOs.Products;

/// <summary>Products page row — Product joined to its ProductCategory.</summary>
public class ProductCatalogDto
{
    public int Id { get; set; }
    public int ProdNo { get; set; }
    public string CProdNo { get; set; } = string.Empty;
    public string ProdDesc { get; set; } = string.Empty;
    public string PackSize { get; set; } = string.Empty;
    public int Pieces { get; set; }
    public string Um { get; set; } = string.Empty;
    /// <summary>prod4win.NEWPRICE — list price per case, ex-VAT.</summary>
    public decimal? NewPrice { get; set; }
    public DateOnly? PriceFrom { get; set; }
    public decimal? OldPrice1 { get; set; }
    public decimal? Srp { get; set; }
    public decimal TaxRate { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? GroupDesc { get; set; }
    public string? SubCat { get; set; }
    public int GroupNo { get; set; }
    /// <summary>Book2 Product Category of GroupNo ("Peas & Beans"), from the TASAF Product Categories list
    /// (its code "02" = GroupNo 2); null when none matches.</summary>
    public string? ProductCategory { get; set; }
    public int SeqNo { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string SBrand { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string CaseBarcode { get; set; } = string.Empty;
    public bool PriceList { get; set; }
    public bool PhOut { get; set; }
    /// <summary>Active, Phased Out (PHOUT) or Delisted (category group "Delisted").</summary>
    public string Status { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
}

public class PriceHistoryDto
{
    public DateOnly? Effective { get; set; }
    /// <summary>PRCHST.NPAFVAT — price with VAT as of Effective.</summary>
    public decimal NpAfVat { get; set; }
}
