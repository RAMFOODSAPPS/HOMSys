using System.Text.Json;
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
    ISyncLogRepository syncLogRepo,
    IUnitOfWork uow)
{
    /// <summary>How long a branch's bridge can go quiet before HOMSys treats its
    /// BMS-side state as possibly stale (shared with SalesOrderService's edit
    /// guard). The scheduled drain runs every 5 min, so 15 min = 3 missed runs.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    /// <summary>How far back the reconciliation sweep re-checks orders.</summary>
    private const int ReconcileWindowDays = 60;

    /// <summary>
    /// SoId for a BMS SO# in this branch, or null if it isn't a HOMSys order —
    /// backs the by-sono bridge endpoints, which replaced the bridge's own
    /// per-workstation ledger lookup (that ledger lives in %LOCALAPPDATA%, so a
    /// lock/deallocate fired from any PC other than the one that downloaded the
    /// order used to be silently skipped).
    /// </summary>
    public async Task<int?> FindSoIdBySoNoAsync(int soNo, string branch) =>
        (await orderRepo.GetForUpdateBySoNoAsync(soNo, branch))?.SoId;

    /// <summary>Bridge heartbeat — every bridge run that reaches HOMSys records it,
    /// so the UI can show "branch last synced N min ago" and the edit guard can
    /// tell an offline branch from a quiet one.</summary>
    public Task HeartbeatAsync(string branch) => syncLogRepo.RecordAsync(SyncLogSections.SoBridge(branch));

    /// <summary>HOMSys's current view of this branch's live (non-Cancelled, in-BMS)
    /// orders from the last <see cref="ReconcileWindowDays"/> days, for the bridge's
    /// reconciliation sweep to compare against BMS truth and re-post any gap —
    /// the safety net behind the outbox for events that were never captured.</summary>
    public async Task<IEnumerable<BridgeReconcileCandidateDto>> GetReconcileCandidatesAsync(string branch)
    {
        var todayPh = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var orders = await orderRepo.GetReconcileCandidatesAsync(branch, todayPh.AddDays(-ReconcileWindowDays));
        return orders.Select(o => new BridgeReconcileCandidateDto
        {
            SoNo = o.SoNo!.Value,
            InvNo = o.InvNo,
            CancelledInvNo = o.CancelledInvNo,
            WorkflowStatus = o.WorkflowStatus,
            IsLocked = o.IsLocked,
            NeedsResync = o.NeedsResync,
            Delivered = o.Delivered,
            DeliveryStatus = o.Status,
            RfcNos = o.Rfcs.Select(r => r.RfcNo).OrderBy(n => n).ToList()
        }).ToList();
    }

    public const string Transferring = "Transferring";
    public const string Transferred = "Transferred";

    /// <summary>Origin side: SO#s of offshore orders in this branch's BMS still
    /// waiting to be handed off. The bridge checks each against the live oowkhdr
    /// (clean at the origin: Confirm Clean Orders condition) and uploads the ones that qualify.</summary>
    public async Task<List<int>> GetOffshoreAwaitingAsync(string branch) =>
        (await orderRepo.GetOffshoreAwaitingAsync(branch)).Select(o => o.SoNo!.Value).ToList();

    /// <summary>
    /// Origin side: stores the order's origin BMS rows and hands the order to
    /// ForBranch — Branch flips, so every by-sono/reconcile path now belongs to
    /// the target's bridge. Idempotent: a retried upload is a no-op. Conflict
    /// when the target already has a HOMSys order with this SO# (the SO# is kept
    /// across the hand-off, as UTIL18/UTIL19 always did) — recorded on the order.
    /// </summary>
    public async Task<(bool Found, bool Conflict, string? Error)> UploadOffshoreAsync(int soNo, string branch, BridgeOffshoreUploadDto dto)
    {
        var order = await orderRepo.GetOffshoreForUpdateAsync(soNo, branch);
        if (order is null)
            return (false, false, $"No offshore sales order found for SO {soNo} from branch {branch}.");
        if (order.OffshoreUploadedAt is not null)
            return (true, false, null);
        if (order.WorkflowStatus == "Cancelled")
            return (true, true, $"SO {soNo} is Cancelled; not handed off.");

        if (await orderRepo.SoNoTakenAsync(soNo, order.ForBranch!, order.SoId))
        {
            order.OffshoreError = $"SO# {soNo} already exists in {order.ForBranch} — not handed off.";
            await orderRepo.SaveChangesAsync();
            return (true, true, order.OffshoreError);
        }

        order.OffshoreTransfer = new OffshoreTransfer
        {
            SoId = order.SoId,
            HeaderJson = JsonSerializer.Serialize(dto.Header),
            LinesJson = JsonSerializer.Serialize(dto.Lines),
            DiscountsJson = JsonSerializer.Serialize(dto.Discounts),
            UploadedAt = DateTime.UtcNow
        };
        order.Branch = order.ForBranch;
        order.OffshoreUploadedAt = DateTime.UtcNow;
        order.OffshoreError = null;
        order.IsLocked = true;
        order.WorkflowStatus = Transferring;
        await orderRepo.SaveChangesAsync();
        return (true, false, null);
    }

    /// <summary>Target side: offshore orders handed to this branch, with their
    /// origin rows, not yet appended into its BMS (and not failed).</summary>
    public async Task<IEnumerable<BridgeOffshoreInboundDto>> GetOffshoreInboundAsync(string branch) =>
        (await orderRepo.GetOffshoreInboundAsync(branch))
            .Where(o => o.OffshoreTransfer is not null && o.OffshoreError is null)
            .Select(o => new BridgeOffshoreInboundDto
            {
                SoId = o.SoId,
                SoNo = o.SoNo!.Value,
                OriginBranch = o.OriginBranch,
                Header = JsonSerializer.Deserialize<Dictionary<string, string>>(o.OffshoreTransfer!.HeaderJson) ?? [],
                Lines = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(o.OffshoreTransfer.LinesJson) ?? [],
                Discounts = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(o.OffshoreTransfer.DiscountsJson) ?? []
            })
            .ToList();

    /// <summary>Target side: invoice_appendhomsys.prg's pAppendOffshore appended the
    /// order (status 5 PickListed) — or found its DOCNO already in the target BMS.</summary>
    public async Task<string?> ConfirmOffshoreReceivedAsync(int soId, BridgeOffshoreReceivedDto dto)
    {
        var order = await orderRepo.GetForUpdateAsync(soId);
        if (order is null)
            return $"Sales order {soId} not found.";

        if (dto.Result == "appended")
        {
            order.OffshoreReceivedAt ??= DateTime.UtcNow;
            order.OffshoreError = null;
            if (order.WorkflowStatus == Transferring)
                order.WorkflowStatus = Transferred;
        }
        else
        {
            var message = (dto.Message ?? string.Empty).Trim();
            order.OffshoreError = message.Length == 0
                ? $"SO# {order.SoNo} already exists in {order.Branch}'s BMS — not appended."
                : message[..Math.Min(message.Length, 500)];
        }
        await orderRepo.SaveChangesAsync();
        return null;
    }

    public const string FullRfc = "Full RFC";
    public const string InvoicedWithRfc = "Invoiced with RFC";

    /// <summary>
    /// Posted BMS RFCs (in practice from c1110k2.scx's toRFC button: an approved
    /// RSR moved into an auto-posted RFC), grouped per invoice. The bridge sends
    /// only RFCs it hasn't uploaded before (its rfc_sent.json list), so this
    /// MERGES: an RFC not yet on the order is added, one already there is
    /// rewritten only if its content differs, and nothing is ever deleted — a
    /// posted RFC can't be cancelled in BMS, so a missing RFC just means "already
    /// sent". Then the status is recomputed over ALL the order's RFCs: "Full RFC"
    /// when every invoiced SKU's quantity has been returned, else "Invoiced with
    /// RFC" (quantities are the only reliable signal — BMS stamps every RFC the
    /// same way). Unknown invoices are skipped (most aren't HOMSys orders).
    /// Returns the invoice numbers that matched a HOMSys order — the bridge
    /// records RFCs as sent only for those, so an RFC that arrives before its
    /// order is known to HOMSys is retried instead of being lost.
    /// </summary>
    public async Task<List<int>> SyncRfcsAsync(string branch, BridgeRfcSyncDto dto)
    {
        var matched = new List<int>();
        foreach (var inv in dto.Invoices)
        {
            var order = await orderRepo.GetForRfcSyncAsync(inv.SoNo, inv.InvNo, branch);
            if (order is null)
                continue;
            matched.Add(inv.InvNo);

            var incoming = inv.Rfcs.Select(r => new SalesOrderRfc
            {
                RfcNo = r.RfcNo,
                InvNo = inv.InvNo,
                RsrNo = r.RsrNo,
                RfcDate = r.RfcDate,
                PostedDate = r.PostedDate,
                UserName = r.UserName,
                Remarks = r.Remarks,
                Remarks2 = r.Remarks2,
                SyncedAt = DateTime.UtcNow,
                Lines = r.Lines.Select(l => new SalesOrderRfcLine
                {
                    CProdNo = l.CProdNo,
                    QtyCs = l.QtyCs,
                    QtyPc = l.QtyPc,
                    Pieces = l.Pieces,
                    SpAmt = l.SpAmt,
                    Amt = l.Amt,
                    Tax = l.Tax,
                    DiscAmt1 = l.DiscAmt1,
                    DiscAmt2 = l.DiscAmt2,
                    RetCode = l.RetCode,
                    RsNo = l.RsNo,
                    Remarks = l.Remarks
                }).ToList()
            }).ToList();

            var changed = false;
            foreach (var r in incoming)
            {
                var current = order.Rfcs.FirstOrDefault(x => x.RfcNo == r.RfcNo);
                if (current is not null)
                {
                    if (RfcSignature([current]) == RfcSignature([r]))
                        continue;  // already there, unchanged
                    order.Rfcs.Remove(current);  // required FK: EF deletes it (and its lines, cascade)
                }
                order.Rfcs.Add(r);
                changed = true;
            }
            changed |= ApplyRfcStatus(order);
            if (changed)
                await orderRepo.SaveChangesAsync();
        }
        return matched;
    }

    private static string RfcSignature(IEnumerable<SalesOrderRfc> rfcs) =>
        string.Join("|", rfcs.OrderBy(r => r.RfcNo).Select(r =>
            $"{r.RfcNo};{r.InvNo};{r.RsrNo};{r.RfcDate};{r.PostedDate};{r.UserName};{r.Remarks};{r.Remarks2};" +
            string.Join(",", r.Lines
                .OrderBy(l => l.CProdNo).ThenBy(l => l.QtyCs).ThenBy(l => l.QtyPc).ThenBy(l => l.SpAmt)
                .Select(l => string.Join(":", l.CProdNo, l.QtyCs, l.QtyPc, l.Pieces,
                    l.SpAmt.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    l.Amt.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    l.Tax.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    l.DiscAmt1.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    l.DiscAmt2.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    l.RetCode, l.RsNo, l.Remarks)))));

    /// <summary>Precedence: Cancelled (a1174, untouched here) > Full RFC > Invoiced
    /// with RFC > Invoiced. Losing every RFC reverts an RFC status to Invoiced.</summary>
    private static bool ApplyRfcStatus(SalesOrder order)
    {
        if (order.WorkflowStatus == "Cancelled")
            return false;
        var next = order.Rfcs.Count == 0
            ? (order.WorkflowStatus is FullRfc or InvoicedWithRfc ? "Invoiced" : order.WorkflowStatus)
            : (IsFullyReturned(order) ? FullRfc : InvoicedWithRfc);
        if (next == order.WorkflowStatus)
            return false;
        order.WorkflowStatus = next;
        order.IsLocked = true;  // anything invoiced/returned is never editable again
        return true;
    }

    /// <summary>Returned pieces >= invoiced pieces for every SKU that was invoiced.
    /// Invoiced = the INV CS snapshot (OosSyncLines, what the view shows as INV CS),
    /// falling back to the encoded quantities when the order never got an OOS sync.</summary>
    private static bool IsFullyReturned(SalesOrder order)
    {
        var pieces = order.Lines.GroupBy(l => l.CProdNo).ToDictionary(g => g.Key, g => g.First().Pieces);
        int Pcs(string cProdNo, int cs, int pc, int fallbackPieces) =>
            cs * (pieces.TryGetValue(cProdNo, out var p) && p > 0 ? p : Math.Max(fallbackPieces, 1)) + pc;

        var invoiced = order.OosSyncLines.Count > 0
            ? order.OosSyncLines.GroupBy(l => l.CProdNo).ToDictionary(g => g.Key, g => g.Sum(l => Pcs(l.CProdNo, l.AllocatedQtyCs, l.AllocatedQtyPc, 1)))
            : order.Lines.GroupBy(l => l.CProdNo).ToDictionary(g => g.Key, g => g.Sum(l => Pcs(l.CProdNo, l.QtyCs, l.QtyPc, 1)));
        var returned = order.Rfcs.SelectMany(r => r.Lines).GroupBy(l => l.CProdNo)
            .ToDictionary(g => g.Key, g => g.Sum(l => Pcs(l.CProdNo, l.QtyCs, l.QtyPc, l.Pieces)));

        var due = invoiced.Where(kv => kv.Value > 0).ToList();
        return due.Count > 0 && due.All(kv => returned.TryGetValue(kv.Key, out var r) && r >= kv.Value);
    }

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
            ForBranch = o.ForBranch,
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

        // Cancelled / Full RFC / Invoiced with RFC are downstream of Invoiced and
        // must never be reset by an invoice (re)sync -- an RFC'd invoice keeps
        // its oowkhdr INVNO in BMS, and an RFC can land before the invoice sync
        // did (matched by SO#). Only fill in invoice details still missing.
        if (order.WorkflowStatus is "Cancelled" or FullRfc or InvoicedWithRfc)
        {
            if (order.InvNo is not null)
                return null;
            order.InvNo = invNo;
            order.InvDate ??= invDate;
            order.InvAmt ??= invAmt;
            await orderRepo.SaveChangesAsync();
            return null;
        }

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
    /// editing in HOMSys again and clears its OOS snapshot: BMS released the
    /// allocation, so the last Process's OOS/INV CS no longer applies (it was
    /// still showing on the deallocated, editable order). The next Process
    /// posts a fresh snapshot -- the bridge forgets its "oos:<so>" sent-event
    /// fingerprint on deallocate so an identical re-snapshot isn't skipped.
    /// </summary>
    public async Task<string?> DeallocateAsync(int soId)
    {
        var order = await orderRepo.GetForUpdateAsync(soId);
        if (order is null)
            return $"Sales order {soId} not found.";

        order.IsLocked = false;
        order.WorkflowStatus = "Deallocated";
        await orderRepo.SaveChangesAsync();
        await oosSyncRepo.ReplaceForOrderAsync(soId, []);
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
    /// <summary>
    /// Fired by a1146F's delivery-status maintenance screen via run_bridge.bat's
    /// "DELIVERED" action once VSHDR.DELIVERED/STATUS are saved for an order's
    /// invoice. Keyed on SoNo+branch rather than SoId, since the bridge looks
    /// this up server-side (not through its local ledger) — a1146F can run long
    /// after the order's original confirm, on any workstation. Found=false (a
    /// 404) means not a HOMSys order — a1146F fires this for every invoice it
    /// tags, most of which aren't, so the bridge's outbox drops those quietly
    /// instead of retrying them forever.
    /// </summary>
    public async Task<(bool Found, string? Error)> ConfirmDeliveryAsync(int soNo, string branch, BridgeDeliveryDto dto)
    {
        var order = await orderRepo.GetForUpdateBySoNoAsync(soNo, branch);
        if (order is null)
            return (false, $"No sales order found for SO {soNo} in branch {branch}.");

        // Belt-and-suspenders on top of the SoNo+branch match -- only rejects
        // when both sides actually carry an InvNo and they disagree. HOMSys's
        // own InvNo can still be null here if the invoice sync bridge call
        // hasn't landed yet, which is a normal ordering lag, not an error.
        if (dto.InvNo is not null && order.InvNo is not null && order.InvNo != dto.InvNo)
            return (true, $"SO {soNo} found, but its InvNo ({order.InvNo}) does not match the invoice being tagged ({dto.InvNo}).");

        order.Delivered = dto.Delivered;
        order.Status = dto.Status;
        order.VsNo = dto.VsNo;
        order.VsDate = dto.VsDate;
        order.PlateNo = dto.PlateNo;
        order.Trucker = dto.Trucker;
        order.Driver = dto.Driver;
        order.Vessel = dto.Vessel;
        order.Voyage = dto.Voyage;
        order.BlNo = dto.BlNo;
        order.Edd = dto.Edd;
        order.Eda2 = dto.Eda2;

        var byCProdNo = order.Lines.ToDictionary(l => l.CProdNo);
        foreach (var vsdetLine in dto.Lines)
        {
            // A VSDET row with no matching SalesOrderLine (CProdNo drifted, or
            // the invoice carries a SKU this order never had) is skipped rather
            // than failing the whole save -- the header status is what matters.
            if (byCProdNo.TryGetValue(vsdetLine.CProdNo, out var line))
            {
                line.ReceivedQtyCs = vsdetLine.ReceivedQtyCs;
                line.ReceivedQtyPc = vsdetLine.ReceivedQtyPc;
                line.ReceivedAmt = vsdetLine.ReceivedAmt;
                line.ReceivedStatus = vsdetLine.ReceivedStatus;
            }
        }

        await orderRepo.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>
    /// Fired by a1174.scx Invoice Cancellation ("CANCELINV" via run_bridge.bat),
    /// which deletes the oowkhdr/oowkdet (or posted oocuhdr/oocudet) record
    /// outright. NOT used for RFCs — an RFC is a return against a valid invoice,
    /// see SyncRfcsAsync. Terminal: status Cancelled, locked, CancelledInvNo set.
    /// The invoice details (InvNo/InvDate/InvAmt, OOS lines, delivery fields)
    /// are KEPT for the record -- analytics excludes Cancelled orders from
    /// invoiced/delivered measures instead, and the UI shows the delivery status
    /// as Cancelled. If HOMSys never got the invoice sync, the invoice no./date/
    /// amount are filled in from the cancel payload (DOCCANCEL DOCDATE/AMOUNT).
    /// Matched on SoNo+branch, else InvNo+branch.
    /// Found=false means it's not a HOMSys order — most BMS invoices aren't,
    /// so the bridge treats that as a quiet no-op.
    /// </summary>
    public async Task<(bool Found, string? Error)> CancelInvoiceAsync(string branch, BridgeInvoiceCancelDto dto)
    {
        var order = dto.SoNo is > 0 ? await orderRepo.GetForUpdateBySoNoAsync(dto.SoNo.Value, branch) : null;
        order ??= await orderRepo.GetForUpdateByInvNoAsync(dto.InvNo, branch);
        if (order is null)
            return (false, $"No sales order found for SO {dto.SoNo} / INV {dto.InvNo} in branch {branch}.");
        var soNo = order.SoNo;

        if (order.CancelledInvNo == dto.InvNo)
        {
            // Idempotent retry -- but still fill any invoice detail a previous
            // cancel couldn't (e.g. orders cancelled before details were kept).
            if (FillMissingInvoiceDetails(order, dto))
                await orderRepo.SaveChangesAsync();
            return (true, null);
        }
        if (order.CancelledInvNo is not null)
            return (true, $"SO {soNo} already had INV# {order.CancelledInvNo} cancelled, refusing to overwrite with {dto.InvNo}.");

        // Same cross-check as ConfirmDeliveryAsync: only reject when HOMSys
        // already holds a different live invoice for this SO. A null InvNo is
        // fine — the invoice sync may simply never have landed.
        if (order.InvNo is not null && order.InvNo != dto.InvNo)
            return (true, $"SO {soNo} found, but its InvNo ({order.InvNo}) does not match the cancelled invoice ({dto.InvNo}).");

        order.CancelledInvNo = dto.InvNo;
        order.InvCancelDate = dto.CancelDate;
        order.InvCancelRemarks = dto.Remarks;
        order.InvCancelledBy = dto.CancelledBy;
        FillMissingInvoiceDetails(order, dto);
        order.IsLocked = true;
        order.NeedsResync = false;
        order.WorkflowStatus = "Cancelled";
        await orderRepo.SaveChangesAsync();
        return (true, null);
    }

    private static bool FillMissingInvoiceDetails(SalesOrder order, BridgeInvoiceCancelDto dto)
    {
        var changed = false;
        if (order.InvNo is null) { order.InvNo = dto.InvNo; changed = true; }
        if (order.InvDate is null && dto.InvDate is not null) { order.InvDate = dto.InvDate; changed = true; }
        if (order.InvAmt is null && dto.InvAmt is not null) { order.InvAmt = dto.InvAmt; changed = true; }
        return changed;
    }

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
