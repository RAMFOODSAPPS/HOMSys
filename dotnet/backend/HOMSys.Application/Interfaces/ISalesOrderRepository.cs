using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

public interface ISalesOrderRepository
{
    Task<IEnumerable<SalesOrder>> GetAllAsync();
    Task<SalesOrder?> GetByIdAsync(int soId);
    Task<SalesOrder?> GetForUpdateAsync(int soId);

    /// <summary>Orders not yet pushed to BMS — SoNo IS NULL — and not claimed by
    /// another bridge PC (unclaimed, or claimed by <paramref name="claimant"/>). For the Python bridge.</summary>
    Task<IEnumerable<SalesOrder>> GetPendingBridgeAsync(string branch, string? claimant);

    /// <summary>Atomic claim (one UPDATE ... WHERE): succeeds only while SoNo IS NULL and
    /// the order is unclaimed or already claimed by this same claimant. True if it claimed.</summary>
    Task<bool> TryClaimAsync(int soId, string branch, string claimant, DateTime now);

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

    /// <summary>Offshore orders this (origin) branch's BMS holds that haven't been
    /// handed off yet — SoNo set, ForBranch set, not uploaded, not Cancelled.</summary>
    Task<IEnumerable<SalesOrder>> GetOffshoreAwaitingAsync(string branch);

    /// <summary>Offshore orders already uploaded to this (ForBranch) branch but not
    /// yet appended into its BMS, with their OffshoreTransfer payload.</summary>
    Task<IEnumerable<SalesOrder>> GetOffshoreInboundAsync(string branch);

    /// <summary>True if another order already owns this SO# in this branch — the
    /// (SoNo, Branch) key every by-sono bridge lookup relies on.</summary>
    Task<bool> SoNoTakenAsync(int soNo, string branch, int excludeSoId);

    /// <summary>Tracked offshore order by its origin SO# — found both before the
    /// hand-off (Branch = origin) and after it, so a retried upload is idempotent.</summary>
    Task<SalesOrder?> GetOffshoreForUpdateAsync(int soNo, string originBranch);

    /// <summary>Earliest order carrying this import file hash, or null if none.</summary>
    Task<SalesOrder?> FindByFileHashAsync(string fileHash);

    /// <summary>Existing orders whose PoNum is in the given set — for the fallback Customer+PO duplicate check.</summary>
    Task<IEnumerable<SalesOrder>> FindByPoNumsAsync(IEnumerable<string> poNums);

    Task<SalesOrder> CreateAsync(SalesOrder order);
    Task DeleteAsync(int soId);
    Task SaveChangesAsync();
}
