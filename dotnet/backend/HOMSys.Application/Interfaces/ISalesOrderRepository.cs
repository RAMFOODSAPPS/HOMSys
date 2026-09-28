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

    /// <summary>Live order matching a BMS SO number for this branch — for the Python bridge's delivery-status sync, looked up server-side rather than through the bridge's local SoId ledger.</summary>
    Task<SalesOrder?> GetForUpdateBySoNoAsync(int soNo, string branch);

    /// <summary>Order carrying this BMS invoice number (live InvNo, or CancelledInvNo once cancelled) for this branch — invoice-cancel fallback when the caller couldn't resolve the SO#.</summary>
    Task<SalesOrder?> GetForUpdateByInvNoAsync(int invNo, string branch);

    /// <summary>Orders already in BMS (SoNo set), not Cancelled, ordered on/after
    /// <paramref name="since"/> — what the bridge's reconciliation sweep re-checks
    /// against BMS truth. Read-only.</summary>
    Task<IEnumerable<SalesOrder>> GetReconcileCandidatesAsync(string branch, DateOnly since);

    /// <summary>Tracked order (with Lines, OosSyncLines, Rfcs.Lines) for an RFC sync —
    /// matched on SO# + branch when given, else on InvNo/CancelledInvNo + branch.</summary>
    Task<SalesOrder?> GetForRfcSyncAsync(int? soNo, int invNo, string branch);

    /// <summary>Earliest order carrying this import file hash, or null if none.</summary>
    Task<SalesOrder?> FindByFileHashAsync(string fileHash);

    /// <summary>Existing orders whose PoNum is in the given set — for the fallback Customer+PO duplicate check.</summary>
    Task<IEnumerable<SalesOrder>> FindByPoNumsAsync(IEnumerable<string> poNums);

    Task<SalesOrder> CreateAsync(SalesOrder order);
    Task DeleteAsync(int soId);
    Task SaveChangesAsync();
}
