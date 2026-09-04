using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;

namespace HOMSys.Infrastructure.Repositories;

/// <summary>
/// Hardcoded, not DB-backed — docclass.DBF (\\Acastillano\setup\ADC\BMSRAM)
/// only ever has these 4 rows in practice and effectively never changes, so
/// there is no import/sync for this lookup.
/// </summary>
public class DocClassRepository : IDocClassRepository
{
    private static readonly DocClass[] All =
    [
        new() { Code = "", Description = "REGULAR TRANSACTION" },
        new() { Code = "1", Description = "DUS CLEARING" },
        new() { Code = "2", Description = "DOCUMENTATION" },
        new() { Code = "3", Description = "AUTO GENERATED" }
    ];

    public Task<IEnumerable<DocClass>> GetAllAsync() =>
        Task.FromResult<IEnumerable<DocClass>>(All);
}
