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
        var q = db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Branch))
            q = q.Where(c => c.Branch == query.Branch);

        q = query.Status switch
        {
            "inactive" => q.Where(c => c.Inactive),
            "all"      => q,
            _          => q.Where(c => !c.Inactive),
        };

        // Every word must match somewhere — same pattern as CustomerRepository.SearchAsync.
        foreach (var token in (query.Term ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
            q = q.Where(c => c.CustKey.Contains(token) || c.CusName.Contains(token) || c.AddrLn1.Contains(token)
                          || c.AddrLn2.Contains(token) || c.CZone.Contains(token) || c.Tin.Contains(token));

        var total = await q.CountAsync();

        q = (query.SortField, query.SortDesc) switch
        {
            ("cusName", false)  => q.OrderBy(c => c.CusName).ThenBy(c => c.Id),
            ("cusName", true)   => q.OrderByDescending(c => c.CusName).ThenBy(c => c.Id),
            ("branch", false)   => q.OrderBy(c => c.Branch).ThenBy(c => c.CustKey),
            ("branch", true)    => q.OrderByDescending(c => c.Branch).ThenBy(c => c.CustKey),
            ("cZone", false)    => q.OrderBy(c => c.CZone).ThenBy(c => c.CustKey),
            ("cZone", true)     => q.OrderByDescending(c => c.CZone).ThenBy(c => c.CustKey),
            ("termDays", false) => q.OrderBy(c => c.TermDays).ThenBy(c => c.CustKey),
            ("termDays", true)  => q.OrderByDescending(c => c.TermDays).ThenBy(c => c.CustKey),
            ("firstOrder", false) => q.OrderBy(c => c.FirstOrder).ThenBy(c => c.CustKey),
            ("firstOrder", true)  => q.OrderByDescending(c => c.FirstOrder).ThenBy(c => c.CustKey),
            (_, true)           => q.OrderByDescending(c => c.CustKey).ThenBy(c => c.Branch),
            _                   => q.OrderBy(c => c.CustKey).ThenBy(c => c.Branch),
        };

        var rows = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(c => new CustomerMasterDto
            {
                Id = c.Id, Branch = c.Branch, CustKey = c.CustKey, CusName = c.CusName,
                AddrLn1 = c.AddrLn1, AddrLn2 = c.AddrLn2, DelAddrLn1 = c.DelAddrLn1, DelAddrLn2 = c.DelAddrLn2,
                DelArea = c.DelArea, WhseNo = c.WhseNo, ServeWh = c.ServeWh, DelWhse = c.DelWhse,
                Salesman = c.Salesman, CsMan = c.CsMan, Term = c.Term, TermDays = c.TermDays, CZone = c.CZone,
                Tin = c.Tin, VatId = c.VatId, Subd = c.Subd, Tpc = c.Tpc, Offshore = c.Offshore,
                ExBranch = c.ExBranch, BlockInv = c.BlockInv, Inactive = c.Inactive,
                FirstOrder = c.FirstOrder, ImportedAt = c.ImportedAt
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
