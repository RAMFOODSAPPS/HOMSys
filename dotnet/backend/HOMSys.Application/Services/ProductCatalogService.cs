using HOMSys.Application.DTOs.Products;
using HOMSys.Application.Interfaces;

namespace HOMSys.Application.Services;

/// <summary>
/// Read-only Products page. Products are synced from BMS PROD4WIN.DBF (see
/// PricingDataImporter / the master watcher), so HOMSys never edits them.
/// </summary>
public class ProductCatalogService(IProductRepository repo)
{
    public const string Active = "Active";
    public const string PhasedOut = "Phased Out";
    public const string Delisted = "Delisted";

    public async Task<List<ProductCatalogDto>> GetAllAsync()
    {
        var rows = await repo.GetCatalogAsync();
        foreach (var r in rows)
        {
            r.ProdDesc  = r.ProdDesc.Trim();
            r.PackSize  = r.PackSize.Trim();
            r.Brand     = r.Brand.Trim();
            r.GroupDesc = r.GroupDesc?.Trim();
            r.SubCat    = r.SubCat?.Trim();
            r.Status = string.Equals(r.GroupDesc, Delisted, StringComparison.OrdinalIgnoreCase) ? Delisted
                     : r.PhOut ? PhasedOut
                     : Active;
        }
        return rows;
    }

    public async Task<List<PriceHistoryDto>> GetPriceHistoryAsync(int prodNo) =>
        (await repo.GetPriceHistoryAsync(prodNo))
            .Select(h => new PriceHistoryDto { Effective = h.Effective, NpAfVat = h.NpAfVat })
            .ToList();
}
