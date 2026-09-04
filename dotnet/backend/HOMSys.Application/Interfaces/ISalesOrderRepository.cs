using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

public interface ISalesOrderRepository
{
    Task<IEnumerable<SalesOrder>> GetAllAsync();
    Task<SalesOrder?> GetByIdAsync(int soId);
    Task<SalesOrder?> GetForUpdateAsync(int soId);

    /// <summary>Orders not yet pushed to BMS — SoNo IS NULL. For the Python bridge.</summary>
    Task<IEnumerable<SalesOrder>> GetPendingBridgeAsync(string branch);

    /// <summary>Orders already pushed to BMS but edited again in HOMSys since (post-deallocation edit) — SoNo IS NOT NULL and NeedsResync. For the Python bridge.</summary>
    Task<IEnumerable<SalesOrder>> GetResyncPendingAsync(string branch);

    /// <summary>Earliest order carrying this import file hash, or null if none.</summary>
    Task<SalesOrder?> FindByFileHashAsync(string fileHash);

    /// <summary>Existing orders whose PoNum is in the given set — for the fallback Customer+PO duplicate check.</summary>
    Task<IEnumerable<SalesOrder>> FindByPoNumsAsync(IEnumerable<string> poNums);

    Task<SalesOrder> CreateAsync(SalesOrder order);
    Task DeleteAsync(int soId);
    Task SaveChangesAsync();
}
