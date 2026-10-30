using HOMSys.Application.DTOs.Collections;
using HOMSys.Application.Services;
using HOMSys.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Data;

/// <summary>
/// Queue between a branch ("fetch these invoices") and LegacyMasterWatcher.exe at HO, which
/// polls <see cref="ClaimPendingAsync"/>. Branches never run the watcher themselves.
/// </summary>
public class InvoiceSyncRequestService(AppDbContext db)
{
    /// <summary>A Processing request not completed within this window is handed out again.</summary>
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);
    private const int MaxItems = 50;

    /// <summary>Custodians may request for their assigned sites; collections-manage for any.</summary>
    public async Task<(InvoiceSyncRequestStatusDto? dto, string? error)> CreateAsync(
        int userId, string userName, bool canManageAll, CreateInvoiceSyncRequestDto dto)
    {
        var items = (dto.Items ?? []).Where(i => i.InvoiceNo > 0).DistinctBy(i => i.InvoiceNo).ToList();
        if (items.Count == 0) return (null, "Enter at least one invoice number and date.");
        if (items.Count > MaxItems) return (null, $"At most {MaxItems} invoices per request.");
        // The invoice date picks the SL{WH}{MMYY} file, so a wrong one means "not found".
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8)).AddDays(1);
        var badDate = items.FirstOrDefault(i => i.InvoiceDate < new DateOnly(2000, 1, 1) || i.InvoiceDate > tomorrow);
        if (badDate is not null) return (null, $"Invoice {badDate.InvoiceNo}: invoice date {badDate.InvoiceDate:yyyy-MM-dd} is not valid.");

        var site = await db.Sites.FirstOrDefaultAsync(s => s.Id == dto.SiteId && s.IsActive);
        if (site is null) return (null, "Branch not found.");
        if (!canManageAll && !await db.UserBranches.AnyAsync(b => b.UserId == userId && b.SiteId == site.Id))
            return (null, "You are not assigned to this branch.");

        var wh = BranchWarehouseMap.Wh(site.Code);
        if (wh is null) return (null, $"No warehouse mapping for branch {site.Code}.");

        // Same branch + same invoices already waiting: hand back that request instead of queueing a duplicate.
        // (Compares number AND date, so correcting a mistyped date queues a new request.)
        var wanted = items.Select(i => (i.InvoiceNo, i.InvoiceDate)).Order().ToList();
        var open = await db.InvoiceSyncRequests.Include(r => r.Items)
            .Where(r => r.SiteId == site.Id && (r.Status == InvoiceSyncStatus.Pending || r.Status == InvoiceSyncStatus.Processing))
            .ToListAsync();
        var dup = open.FirstOrDefault(r => r.Items.Select(i => (i.InvoiceNo, i.InvoiceDate)).Order().SequenceEqual(wanted));
        if (dup is not null) return (ToStatus(dup), null);

        var req = new InvoiceSyncRequest
        {
            SiteId = site.Id, Wh = wh.Value, RequestedBy = userName,
            Items = items.Select(i => new InvoiceSyncRequestItem { InvoiceNo = i.InvoiceNo, InvoiceDate = i.InvoiceDate }).ToList(),
        };
        db.InvoiceSyncRequests.Add(req);
        await db.SaveChangesAsync();
        return (ToStatus(req), null);
    }

    public async Task<InvoiceSyncRequestStatusDto?> GetAsync(int id, int userId, bool canManageAll)
    {
        var req = await db.InvoiceSyncRequests.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == id);
        if (req is null) return null;
        if (!canManageAll && !await db.UserBranches.AnyAsync(b => b.UserId == userId && b.SiteId == req.SiteId)) return null;
        return ToStatus(req);
    }

    /// <summary>Watcher poll: returns Pending (and stale Processing) requests, marking them Processing.</summary>
    public async Task<List<PendingInvoiceSyncDto>> ClaimPendingAsync()
    {
        var cutoff = DateTime.UtcNow - StaleAfter;
        var reqs = await db.InvoiceSyncRequests.Include(r => r.Items)
            .Where(r => r.Status == InvoiceSyncStatus.Pending || (r.Status == InvoiceSyncStatus.Processing && r.ClaimedAt < cutoff))
            .OrderBy(r => r.Id).Take(20).ToListAsync();
        foreach (var r in reqs) { r.Status = InvoiceSyncStatus.Processing; r.ClaimedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync();
        return reqs.Select(r => new PendingInvoiceSyncDto(r.Id, r.Wh,
            r.Items.Select(i => new InvoiceSyncItemDto(i.InvoiceNo, i.InvoiceDate)).ToList())).ToList();
    }

    public async Task<string?> CompleteAsync(int id, CompleteInvoiceSyncDto dto)
    {
        var req = await db.InvoiceSyncRequests.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == id);
        if (req is null) return "Request not found.";

        req.CompletedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto.Error))
        {
            req.Status = InvoiceSyncStatus.Failed;
            req.Message = dto.Error.Length > 500 ? dto.Error[..500] : dto.Error;
        }
        else
        {
            var found = (dto.FoundInvoiceNos ?? []).ToHashSet();
            foreach (var i in req.Items) i.Found = found.Contains(i.InvoiceNo);
            req.Status = InvoiceSyncStatus.Done;
            var missing = req.Items.Count(i => i.Found == false);
            req.Message = missing == 0 ? null : $"{missing} invoice(s) not found in the SL file — check the invoice number and date.";
        }
        await db.SaveChangesAsync();
        return null;
    }

    // SQL hands DateTime back as Unspecified; mark it UTC so the browser doesn't read it as local time.
    private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

    private static InvoiceSyncRequestStatusDto ToStatus(InvoiceSyncRequest r) => new(
        r.Id, r.SiteId, r.Status.ToString(), Utc(r.RequestedAt), r.CompletedAt is { } c ? Utc(c) : null, r.Message,
        r.Items.Select(i => new InvoiceSyncItemStatusDto(i.InvoiceNo, i.InvoiceDate, i.Found)).ToList());
}
