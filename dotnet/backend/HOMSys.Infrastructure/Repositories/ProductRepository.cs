using HOMSys.Application.DTOs.Pricing;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class ProductRepository(AppDbContext db) : IProductRepository
{
    public async Task<Product?> GetByCProdNoAsync(string cProdNo) =>
        await db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.CProdNo == cProdNo);

    public async Task<Dictionary<string, Product>> GetByCProdNosAsync(IEnumerable<string> cProdNos)
    {
        var keys = cProdNos.Distinct().ToList();
        return await db.Products.AsNoTracking()
            .Where(p => keys.Contains(p.CProdNo))
            .ToDictionaryAsync(p => p.CProdNo);
    }

    public async Task<IEnumerable<Product>> SearchAsync(string term, int take = 50) =>
        await db.Products.AsNoTracking()
            .Where(p => p.CProdNo.StartsWith(term) || p.ProdDesc.Contains(term))
            .OrderByDescending(p => p.CProdNo.StartsWith(term))
            .ThenBy(p => p.CProdNo)
            .Take(take)
            .ToListAsync();

    public async Task<List<PriceListProductRow>> GetPriceListWithCategoryAsync()
    {
        var rows = await (
            from p in db.Products.AsNoTracking()
            where p.PriceList
                && (p.Brand + p.SBrand) != ""
                && p.Brand != "INACTIVE"
                && p.Pieces > 0
                && !p.PhOut
            join c in db.ProductCategories.AsNoTracking()
                on p.Category equals c.CategoryCode into cj
            from c in cj.DefaultIfEmpty()
            orderby c != null ? c.GroupNo : int.MaxValue, c != null ? c.SeqNo : int.MaxValue, p.ProdNo
            select new
            {
                Product = p,
                CategoryHeader = c == null ? null : c.GroupDesc + " - " + c.SubCat
            }).ToListAsync();

        return rows.Select(r => new PriceListProductRow(r.Product, r.CategoryHeader)).ToList();
    }
}
