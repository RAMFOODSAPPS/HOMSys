using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

public interface ICustomerIdentifierMapRepository
{
    Task<List<CustomerIdentifierMap>> GetByIdentifiersAsync(IEnumerable<string> identifiers);

    /// <summary>Insert-or-update by Identifier for each mapping supplied.</summary>
    Task UpsertRangeAsync(IEnumerable<(string Identifier, string CustKey)> mappings, string user);
}
