using HOMSys.Application.DTOs.SalesOrders;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;

namespace HOMSys.Application.Services;

/// <summary>
/// Read/confirm surface for the Python SO write-back bridge (watcher\salesorder_bridge.py).
/// All DBF I/O and docnum.dbf locking happens on the Python side — this
/// service only exposes which orders are unpushed and records the numbers
/// the bridge assigned. See legacy/vfp/ANALYSIS.md.
/// </summary>
public class SalesOrderBridgeService(
    ISalesOrderRepository orderRepo,
    IPoLogRepository poLogRepo,
    ICustomerRepository customerRepo,
    IOosSyncRepository oosSyncRepo,
    IUnitOfWork uow)
{
    public async Task<IEnumerable<BridgePendingOrderDto>> GetPendingAsync(string branch)
    {
        var orders = await orderRepo.GetPendingBridgeAsync(branch);
        var result = new List<BridgePendingOrderDto>();
        foreach (var o in orders)
            result.Add(await ToPendingDtoAsync(o));
        return result;
    }

    private async Task<BridgePendingOrderDto> ToPendingDtoAsync(SalesOrder o)
    {
        var customer = await customerRepo.GetByCustKeyAsync(o.CustKey);
        var serveWh = customer?.ServeWh ?? 0;
        if (serveWh == 0)
            serveWh = o.WhseNo;

        return new BridgePendingOrderDto
        {
            SoId = o.SoId,
            SoNo = o.SoNo,
            DocNo = o.DocNo,
            CustKey = o.CustKey,
            CusName = o.CusName,
            CKey = o.CKey,
            OrderDate = o.OrderDate,
            PoNum = o.PoNum,
            PoDate = o.PoDate,
            InvRem = o.InvRem,
            CCode = o.CCode,
            WhseNo = o.WhseNo,
            ShipToLn1 = o.ShipToLn1,
            ShipToLn2 = o.ShipToLn2,
            Term = o.Term,
            TermDays = o.TermDays,
            Salesman = o.Salesman,
            CsMan = o.CsMan,
            CreatedBy = o.CreatedBy,
            ServeWh = serveWh,
            DelWhse = customer?.DelWhse ?? 0,
            Lines = o.Lines.OrderBy(l => l.LineNo).Select(l => new BridgePendingLineDto
            {
                CProdNo = l.CProdNo,
                ProdNo = l.ProdNo,
                ProdDesc = l.ProdDesc,
                PackSize = l.PackSize,
                QtyCs = l.QtyCs,
                QtyPc = l.QtyPc,
                Pieces = l.Pieces,
                Um = l.Um,
                Supplier = l.Supplier,
                CSupplier = l.CSupplier,
                PriceList = l.PriceList,
                TaxRate = l.TaxRate
            }).ToList()
        };
    }

    /// <summary>
    /// Records the BMS-assigned SoNo/DocNo for an order the bridge just wrote
    /// to oowkhdr/oowkdet. Idempotent — calling twice with the same numbers
    /// (e.g. a retried POST after a dropped response) is a no-op the second
    /// time since SoNo is already set.
    /// Does NOT lock the order — a freshly-appended order still sits in
    /// oowkhdr with STATUS "1" (Entered), unprocessed, and stays editable in
    /// HOMSys until BMS's Process Orders (optn_init1) actually processes it.
    /// See LockAsync / GetLockPendingAsync for the real lock trigger.
    /// </summary>
    public async Task<string?> ConfirmAsync(int soId, int soNo, int docNo)
    {
        var order = await orderRepo.GetForUpdateAsync(soId);
        if (order is null)
            return $"Sales order {soId} not found.";

        if (order.SoNo is not null)
            return order.SoNo == soNo ? null : $"Sales order {soId} already has SoNo {order.SoNo}, refusing to overwrite with {soNo}.";

        order.SoNo = soNo;
        order.DocNo = docNo;
        order.WorkflowStatus = "Downloaded";
        foreach (var line in order.Lines)
            line.DocNo = docNo;

        await uow.BeginTransactionAsync();
        try
        {
            await orderRepo.SaveChangesAsync();
            await poLogRepo.SetSoNoBySoIdAsync(soId, soNo);
            await uow.CommitAsync();
            return null;
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Records the BMS-assigned INVNO/INVDATE/INVAMT once the order is
    /// invoiced (printinvoice2). Idempotent — a retried or repeated POST
    /// with the same InvNo is a no-op.
    /// </summary>
    public async Task<string?> ConfirmInvoiceAsync(int soId, int invNo, DateOnly invDate, decimal invAmt)
    {
        var order = await orderRepo.GetForUpdateAsync(soId);
        if (order is null)
            return $"Sales order {soId} not found.";
        if (order.SoNo is null)
            return $"Sales order {soId} has not been pushed to BMS yet.";
        if (order.InvNo == invNo)
            return null;

        order.InvNo = invNo;
        order.InvDate = invDate;
        order.InvAmt = invAmt;
        order.WorkflowStatus = "Invoiced";
        await orderRepo.SaveChangesAsync();
        return null;
    }

    /// <summary>
    /// Dumps the live oowkdet snapshot the bridge just read (recover_one ->
    /// sync_oos, fired on every existing Forward-to-Invoice / printinvoice2
    /// call) into OosSyncLine — a full overwrite of that SO's rows, since
    /// the bridge always posts every line still present in oowkdet at that
    /// moment. A CProdNo missing from the new set (already deleted as a
    /// full stockout) is simply not re-inserted; see SalesOrderService's
    /// ApplyOosStatus for how that absence is reported.
    /// </summary>
    public async Task<string?> SyncOosStatusAsync(int soId, BridgeOosStatusDto dto)
    {
        var order = await orderRepo.GetByIdAsync(soId);
        if (order is null)
            return $"Sales order {soId} not found.";

        var rows = dto.Lines.Select(l => new OosSyncLine
        {
            SoId = soId,
            CProdNo = l.CProdNo,
            AllocatedQtyCs = l.QtyCs,
            AllocatedQtyPc = l.QtyPc,
            StkFlag = l.StkFlag,
            NetAmt = l.NetAmt,
            SyncedAt = DateTime.UtcNow
        });

        await oosSyncRepo.ReplaceForOrderAsync(soId, rows);
        return null;
    }

    /// <summary>
    /// Fired by a1112.scx's drunbridge(SO#, "DEALLOCATE") once BMS has reset
    /// this order's oowkhdr status back to Entered. Unlocks the order for
    /// editing in HOMSys again. Deliberately leaves OosSyncLine rows alone --
    /// they're the only record of a real stockout that happened at Process
    /// time, and BMS's own deallocate wipes that evidence out of oowkdet, so
    /// clearing them here too would erase it from the OOS report as well.
    /// A later re-Process (lock_order/sync_oos) still full-overwrites them
    /// with whatever actually happens next time.
    /// </summary>
    public async Task<string?> DeallocateAsync(int soId)
    {
        var order = await orderRepo.GetForUpdateAsync(soId);
        if (order is null)
            return $"Sales order {soId} not found.";

        order.IsLocked = false;
        order.WorkflowStatus = "Deallocated";
        await orderRepo.SaveChangesAsync();
        return null;
    }

    /// <summary>
    /// Fired directly from invoice.SCX's cnt1.cmdproc.Click via
    /// drunbridge(soNo, "PROCESS") — the moment BMS actually processes the order
    /// out of the Process Orders queue (optn_init1), not merely appends/confirms
    /// its SoNo. Only past this point is the order considered committed in BMS
    /// and locked for editing in HOMSys.
    /// </summary>
    public async Task<string?> LockAsync(int soId)
    {
        var order = await orderRepo.GetForUpdateAsync(soId);
        if (order is null)
            return $"Sales order {soId} not found.";

        order.IsLocked = true;
        order.WorkflowStatus = "Processed";
        await orderRepo.SaveChangesAsync();
        return null;
    }

    /// <summary>
    /// Orders already pushed to BMS (SoNo assigned) but edited again in HOMSys since
    /// deallocation — need their live oowkhdr/oowkdet record updated in place.
    /// </summary>
    public async Task<IEnumerable<BridgePendingOrderDto>> GetResyncPendingAsync(string branch)
    {
        var orders = await orderRepo.GetResyncPendingAsync(branch);
        var result = new List<BridgePendingOrderDto>();
        foreach (var o in orders)
            result.Add(await ToPendingDtoAsync(o));
        return result;
    }

    /// <summary>
    /// Fired by invoice_appendhomsys.prg's pResyncOrder once it has applied (or failed to
    /// apply) a staged resync onto the live oowkhdr/oowkdet record. On success, clears the
    /// resync flags so the order becomes editable again. On failure (DOCNO not found), flags
    /// ResyncFailed instead of leaving NeedsResync stuck true with no HOMSys-side indication.
    /// </summary>
    public async Task<string?> ConfirmResyncAsync(int soId, bool ok)
    {
        var order = await orderRepo.GetForUpdateAsync(soId);
        if (order is null)
            return $"Sales order {soId} not found.";

        if (ok)
        {
            order.NeedsResync = false;
            order.ResyncFailed = false;
            order.IsLocked = false;
        }
        else
        {
            order.ResyncFailed = true;
        }

        await orderRepo.SaveChangesAsync();
        return null;
    }
}
