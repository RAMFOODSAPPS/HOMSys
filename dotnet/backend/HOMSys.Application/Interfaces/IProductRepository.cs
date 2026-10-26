using HOMSys.Application.DTOs.Pricing;
using HOMSys.Application.DTOs.Products;
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

    /// <summary>Every product joined to its category (Products page), ordered GroupNo, SeqNo, ProdNo. Status is left blank.</summary>
    Task<List<ProductCatalogDto>> GetCatalogAsync();
    /// <summary>Product master categories (prodcat): code, SubCat, GroupDesc, GroupNo — by GroupNo, SeqNo.</summary>
    Task<List<HOMSys.Domain.Entities.ProductCategory>> GetCategoriesAsync();

    /// <summary>PRCHST rows for one product, newest first.</summary>
    Task<List<PriceHistory>> GetPriceHistoryAsync(int prodNo);
}
