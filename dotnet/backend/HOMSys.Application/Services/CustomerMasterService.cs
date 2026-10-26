using HOMSys.Application.DTOs.Customers;
using HOMSys.Application.Interfaces;

namespace HOMSys.Application.Services;

/// <summary>
/// Read-only Customers page. Customers are synced from each branch's BMS
/// CUST4WIN.DBF (F:\AUTOPROG\CUSTOMER\{branch}), so HOMSys never edits them.
/// </summary>
public class CustomerMasterService(ICustomerMasterRepository repo)
{
    public static readonly string[] SortFields = ["custKey", "cusName", "branch", "cZone", "nSlGrp", "nRegion",
        "region", "salesGroup", "servicingBranch", "chainGroup", "termDays", "firstOrder"];
    public const int MaxPageSize = 20000; // export of a filtered list

    public async Task<CustomerPageDto> GetPageAsync(CustomerQuery query)
    {
        query.Term      = query.Term?.Trim();
        query.Branch    = string.IsNullOrWhiteSpace(query.Branch) ? null : query.Branch.Trim();
        query.Status    = query.Status?.Trim().ToLowerInvariant() is "inactive" or "all" ? query.Status.Trim().ToLowerInvariant() : "active";
        query.Page      = Math.Max(1, query.Page);
        query.PageSize  = Math.Clamp(query.PageSize, 1, MaxPageSize);
        query.SortField = SortFields.Contains(query.SortField) ? query.SortField : "custKey";

        var page = await repo.GetPageAsync(query);
        foreach (var r in page.Rows)
        {
            r.CustKey    = r.CustKey.Trim();
            r.CusName    = r.CusName.Trim();
            r.AddrLn1    = r.AddrLn1.Trim();
            r.AddrLn2    = r.AddrLn2.Trim();
            r.DelAddrLn1 = r.DelAddrLn1.Trim();
            r.DelAddrLn2 = r.DelAddrLn2.Trim();
            r.CZone      = r.CZone.Trim();
            r.Tin        = r.Tin.Trim();
        }
        return page;
    }

    public Task<List<CustomerBranchCountDto>> GetBranchesAsync() => repo.GetBranchCountsAsync();
}
