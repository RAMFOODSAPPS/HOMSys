using HOMSys.Application.DTOs.Customers;
using HOMSys.Application.Interfaces;

namespace HOMSys.Application.Services;

/// <summary>
/// Withholding-tax flag set by the branch in the legacy fm01 (Customer File
/// Maintenance) form. fm01 calls this before saving CUST4WIN and refuses the
/// save unless it succeeds, so HOMSys and the branch's CUST4WIN.WTHOL agree.
/// CustKey is branch-unique in practice; every copy of it (e.g. hon + DAG) is updated.
/// </summary>
public class CustomerWithholdingService(ICustomerRepository customerRepo)
{
    public async Task<(CustomerWithholdingDto? Dto, string? Error)> SetAsync(string? custKey, bool withholding, string? updatedBy)
    {
        custKey = custKey?.Trim();
        if (string.IsNullOrEmpty(custKey))
            return (null, "custKey is required.");

        var by = (updatedBy ?? string.Empty).Trim();
        if (by.Length > 100) by = by[..100];

        var rows = await customerRepo.SetWithholdingAsync(custKey, withholding, by);
        if (rows == 0)
            return (null, $"Customer {custKey} is not in HOMSys yet.");

        return (new CustomerWithholdingDto(custKey, withholding, rows), null);
    }
}
