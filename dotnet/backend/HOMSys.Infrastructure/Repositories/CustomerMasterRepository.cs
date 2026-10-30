using HOMSys.Application.DTOs.Customers;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Repositories;

public class CustomerMasterRepository(AppDbContext db) : ICustomerMasterRepository
{
    public async Task<CustomerPageDto> GetPageAsync(CustomerQuery query)
    {
        // Left join on CustKey: the HOMSys classification (unique per CustKey, so rows don't multiply).
        var q = from c in db.Customers.AsNoTracking()
                join k in db.CustomerClassifications.AsNoTracking() on c.CustKey equals k.CustKey into kk
                from k in kk.DefaultIfEmpty()
                select new { c, k };

        if (!string.IsNullOrWhiteSpace(query.Branch))
            q = q.Where(x => x.c.Branch == query.Branch);

        q = query.Status switch
        {
            "inactive" => q.Where(x => x.c.Inactive),
            "all"      => q,
            _          => q.Where(x => !x.c.Inactive),
        };

        // Every word must match somewhere — same pattern as CustomerRepository.SearchAsync.
        foreach (var token in (query.Term ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
            q = q.Where(x => x.c.CustKey.Contains(token) || x.c.CusName.Contains(token) || x.c.AddrLn1.Contains(token)
                          || x.c.AddrLn2.Contains(token) || x.c.CZone.Contains(token) || x.c.Tin.Contains(token)
                          || x.c.NSlGrp.Contains(token) || x.c.NRegion.Contains(token)
                          || (x.k != null && (x.k.Region.Contains(token) || x.k.SalesGroup.Contains(token)
                                              || x.k.ServicingBranch.Contains(token) || x.k.ChainGroup.Contains(token))));

        var total = await q.CountAsync();

        q = (query.SortField, query.SortDesc) switch
        {
            ("cusName", false)  => q.OrderBy(x => x.c.CusName).ThenBy(x => x.c.Id),
            ("cusName", true)   => q.OrderByDescending(x => x.c.CusName).ThenBy(x => x.c.Id),
            ("branch", false)   => q.OrderBy(x => x.c.Branch).ThenBy(x => x.c.CustKey),
            ("branch", true)    => q.OrderByDescending(x => x.c.Branch).ThenBy(x => x.c.CustKey),
            ("cZone", false)    => q.OrderBy(x => x.c.CZone).ThenBy(x => x.c.CustKey),
            ("cZone", true)     => q.OrderByDescending(x => x.c.CZone).ThenBy(x => x.c.CustKey),
            ("nRegion", false)  => q.OrderBy(x => x.c.NRegion).ThenBy(x => x.c.CustKey),
            ("nRegion", true)   => q.OrderByDescending(x => x.c.NRegion).ThenBy(x => x.c.CustKey),
            ("nSlGrp", false)   => q.OrderBy(x => x.c.NSlGrp).ThenBy(x => x.c.CustKey),
            ("nSlGrp", true)    => q.OrderByDescending(x => x.c.NSlGrp).ThenBy(x => x.c.CustKey),
            ("region", false)   => q.OrderBy(x => x.k!.Region).ThenBy(x => x.c.CustKey),
            ("region", true)    => q.OrderByDescending(x => x.k!.Region).ThenBy(x => x.c.CustKey),
            ("salesGroup", false) => q.OrderBy(x => x.k!.SalesGroup).ThenBy(x => x.c.CustKey),
            ("salesGroup", true)  => q.OrderByDescending(x => x.k!.SalesGroup).ThenBy(x => x.c.CustKey),
            ("servicingBranch", false) => q.OrderBy(x => x.k!.ServicingBranch).ThenBy(x => x.c.CustKey),
            ("servicingBranch", true)  => q.OrderByDescending(x => x.k!.ServicingBranch).ThenBy(x => x.c.CustKey),
            ("chainGroup", false) => q.OrderBy(x => x.k!.ChainGroup).ThenBy(x => x.c.CustKey),
            ("chainGroup", true)  => q.OrderByDescending(x => x.k!.ChainGroup).ThenBy(x => x.c.CustKey),
            ("termDays", false) => q.OrderBy(x => x.c.TermDays).ThenBy(x => x.c.CustKey),
            ("termDays", true)  => q.OrderByDescending(x => x.c.TermDays).ThenBy(x => x.c.CustKey),
            ("firstOrder", false) => q.OrderBy(x => x.c.FirstOrder).ThenBy(x => x.c.CustKey),
            ("firstOrder", true)  => q.OrderByDescending(x => x.c.FirstOrder).ThenBy(x => x.c.CustKey),
            (_, true)           => q.OrderByDescending(x => x.c.CustKey).ThenBy(x => x.c.Branch),
            _                   => q.OrderBy(x => x.c.CustKey).ThenBy(x => x.c.Branch),
        };

        var rows = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new CustomerMasterDto
            {
                Id = x.c.Id, Branch = x.c.Branch, CustKey = x.c.CustKey, CusName = x.c.CusName,
                AddrLn1 = x.c.AddrLn1, AddrLn2 = x.c.AddrLn2, DelAddrLn1 = x.c.DelAddrLn1, DelAddrLn2 = x.c.DelAddrLn2,
                DelArea = x.c.DelArea, WhseNo = x.c.WhseNo, ServeWh = x.c.ServeWh, DelWhse = x.c.DelWhse,
                Salesman = x.c.Salesman, CsMan = x.c.CsMan, Term = x.c.Term, TermDays = x.c.TermDays, CZone = x.c.CZone,
                NSlGrp = x.c.NSlGrp, NRegion = x.c.NRegion,
                Region = x.k != null ? x.k.Region : "", SalesGroup = x.k != null ? x.k.SalesGroup : "",
                ServicingBranch = x.k != null ? x.k.ServicingBranch : "", ChainGroup = x.k != null ? x.k.ChainGroup : "",
                Tin = x.c.Tin, VatId = x.c.VatId, Subd = x.c.Subd, Tpc = x.c.Tpc, Offshore = x.c.Offshore,
                ExBranch = x.c.ExBranch, BlockInv = x.c.BlockInv, Inactive = x.c.Inactive,
                FirstOrder = x.c.FirstOrder, ImportedAt = x.c.ImportedAt
            })
            .ToListAsync();

        return new CustomerPageDto { Total = total, Rows = rows };
    }

    public async Task<List<CustomerBranchCountDto>> GetBranchCountsAsync() =>
        await db.Customers.AsNoTracking()
            .GroupBy(c => c.Branch)
            .Select(g => new CustomerBranchCountDto
            {
                Branch = g.Key,
                Active = g.Count(c => !c.Inactive),
                Total  = g.Count()
            })
            .OrderBy(b => b.Branch)
            .ToListAsync();
}
