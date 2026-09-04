using HOMSys.Application.DTOs.SalesOrders;
using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByCustKeyAsync(string custKey);
    Task<Dictionary<string, Customer>> GetByCustKeysAsync(IEnumerable<string> custKeys);
    Task<IEnumerable<CustomerSuggestionDto>> SearchAsync(string term, int take = 50);
}
