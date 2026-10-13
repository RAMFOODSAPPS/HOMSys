using HOMSys.Application.DTOs.Customers;

namespace HOMSys.Application.Interfaces;

/// <summary>Read-only queries for the Customers page (paged — the table holds every branch's cust4win).</summary>
public interface ICustomerMasterRepository
{
    /// <summary>query is already normalised (page ≥ 1, page size capped, sort field whitelisted).</summary>
    Task<CustomerPageDto> GetPageAsync(CustomerQuery query);
    Task<List<CustomerBranchCountDto>> GetBranchCountsAsync();
}
