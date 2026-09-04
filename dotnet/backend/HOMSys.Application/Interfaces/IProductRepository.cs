using HOMSys.Application.DTOs.Pricing;
using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByCProdNoAsync(string cProdNo);

    /// <summary>Bulk lookup for the save path so lines don't hit the DB one at a time.</summary>
    Task<Dictionary<string, Product>> GetByCProdNosAsync(IEnumerable<string> cProdNos);

    Task<IEnumerable<Product>> SearchAsync(string term, int take = 50);

    /// <summary>
    /// All PriceList=true products joined to ProductCategory, ordered by
    /// GroupNo, SeqNo, ProdNo â€” the exact row order the pricelist export
    /// renders in.
    /// </summary>
    Task<List<PriceListProductRow>> GetPriceListWithCategoryAsync();
}
