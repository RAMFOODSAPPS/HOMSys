namespace HOMSys.Domain.Entities;

/// <summary>
/// Product category reference data, seeded from F:\PMDM\prodcat.dbf.
/// Drives pricelist grouping/order and the category header row text
/// ("GroupDesc - SubCat"). Read-only in HOMSys — maintained in BMS.
/// </summary>
public class ProductCategory
{
    /// <summary>prodcat.CATEGORY C(4) — matches Product.Category.</summary>
    public string CategoryCode { get; set; } = string.Empty;

    /// <summary>prodcat.GROUP N(2) — sort key across categories.</summary>
    public int GroupNo { get; set; }

    /// <summary>prodcat.GROUPDESC C(30).</summary>
    public string GroupDesc { get; set; } = string.Empty;

    /// <summary>prodcat.SUBCAT C(50).</summary>
    public string SubCat { get; set; } = string.Empty;

    /// <summary>prodcat.SEQNO N(2) — sort key within a group.</summary>
    public int SeqNo { get; set; }
}
