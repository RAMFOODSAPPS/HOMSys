using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

public interface IPoLogRepository
{
    /// <summary>
    /// Has this PO number been encoded before? Drives the non-blocking warning
    /// that mirrors the legacy txtPonum.Valid.
    /// </summary>
    Task<bool> ExistsAsync(string poNum);

    Task<PoLog?> GetLatestAsync(string poNum);
    Task AddAsync(PoLog entry);

    /// <summary>
    /// Fills SoNo on the PoLog row(s) created for this order (idempotent —
    /// a no-op if already set). Called by the bridge confirm step alongside
    /// SalesOrder.SoNo/DocNo.
    /// </summary>
    Task SetSoNoBySoIdAsync(int soId, int soNo);
}
