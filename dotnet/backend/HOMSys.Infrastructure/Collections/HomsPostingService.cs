using System.Text.Json;
using HOMSys.Application.DTOs.Collections;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Collections;

/// <summary>
/// Outbox for writing verified collections into the HOMS ARCHIVES files. Postings are queued inside the verifier's
/// own SaveChanges (Verify → OR / RPDC; approving a verified PDC's bank match → JV) and written by the HO watcher
/// (LegacyMasterWatcher --requests): claim → (JV: report the DOCNUM number) → append → complete.
/// </summary>
public class HomsPostingService(AppDbContext db)
{
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);
    private static DateOnly TodayPh => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
    private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

    public static string Label(HomsPostingKind k) => k switch
    {
        HomsPostingKind.Or => "OR", HomsPostingKind.Rpdc => "RPDC", HomsPostingKind.RpdcCancel => "RPDC-CANCEL", _ => "JV",
    };

    private static readonly HomsPostingStatus[] Open = [HomsPostingStatus.Pending, HomsPostingStatus.Held, HomsPostingStatus.Failed];

    /// <summary>Adds the posting (not saved). One live posting per collection and kind; a cancelled one may be queued again.</summary>
    public async Task QueueAsync(Collection c, HomsPostingKind kind, string user)
    {
        if (await db.HomsPostings.AnyAsync(p => p.CollectionId == c.Id && p.Kind == kind && p.Status != HomsPostingStatus.Cancelled)
            || db.HomsPostings.Local.Any(p => p.Collection == c && p.Kind == kind && p.Status != HomsPostingStatus.Cancelled))
            return;
        await LoadForBuildAsync(c);
        var p = new HomsPosting { Collection = c, Kind = kind, CreatedBy = user };
        if (kind == HomsPostingKind.Or && await db.BmsPostedCollections.AnyAsync(b => b.CollectionId == c.Id && !b.Removed && !b.Cancelled))
        {
            p.Status = HomsPostingStatus.Done;
            p.Table = "OH03";
            p.DocId = 2;
            p.Message = "Already in HOMS (encoded there) — not written again.";
            p.CompletedAt = DateTime.UtcNow;
        }
        else Fill(p, c, user);
        db.HomsPostings.Add(p);
        c.History.Add(new CollectionHistory
        {
            Action = p.Status == HomsPostingStatus.Held ? $"HOMS {Label(kind)} on hold" : $"Queued for HOMS ({Label(kind)})",
            Reason = p.Message, By = user,
        });
    }

    private async Task LoadForBuildAsync(Collection c)
    {
        if (c.Id == 0) return;
        await db.Collections.Where(x => x.Id == c.Id)
            .Include(x => x.Site).Include(x => x.Invoices).ThenInclude(i => i.PostedInvoice)
            .Include(x => x.Deductions).Include(x => x.DepositBatch).ThenInclude(b => b!.BankAccount)
            .AsSplitQuery().LoadAsync();
    }

    private void Fill(HomsPosting p, Collection c, string user)
    {
        // A JV's RPDC no. is the same receipt-derived number the RPDC was written with.
        var post = TodayPh;
        var (doc, error) = HomsPostingBuilder.Build(p.Kind, new HomsPostingBuilder.Input(c, post, user));
        p.Table = p.Kind == HomsPostingKind.Or ? "OH03" : "GH03";
        p.DocId = p.Kind switch { HomsPostingKind.Or => 2, HomsPostingKind.Rpdc => 33, _ => 52 };
        p.MonthKey = post.ToString("MMyy");
        if (doc is null)
        {
            p.Status = HomsPostingStatus.Held;
            p.Message = error;
            return;
        }
        p.Status = HomsPostingStatus.Pending;
        p.Message = null;
        p.DocNo = doc.DocNo ?? p.DocNo;
        p.HeaderJson = HomsPostingBuilder.Json(doc.Header);
        p.LinesJson = HomsPostingBuilder.Json(doc.Lines);
    }

    /// <summary>PDC pulled out / replaced (not saved). Its RPDC never reached HOMS → cancelled here, the PDC can be voided
    /// now (viaHoms false). Written, or tried already (an interrupted run may have left part of it) → an RpdcCancel is
    /// queued: the watcher cancels whatever HOMS has of it, then <see cref="CompleteAsync"/> voids the PDC (viaHoms true).</summary>
    public async Task<(bool viaHoms, string? error)> CancelRpdcAsync(Collection c, string user)
    {
        var list = await db.HomsPostings.Where(p => p.CollectionId == c.Id && p.Status != HomsPostingStatus.Cancelled).ToListAsync();
        if (list.Any(p => p.Kind == HomsPostingKind.PdcJv))
            return (false, "Its JV (the deposit) is already queued or in HOMS — a deposited PDC can't be pulled out.");
        if (list.Any(p => p.Kind == HomsPostingKind.RpdcCancel)) return (false, "Its RPDC is already being cancelled in HOMS.");
        if (list.Any(p => p.Status == HomsPostingStatus.Processing)) return (false, "It is being written to HOMS right now — try again in a minute.");

        var rpdc = list.FirstOrDefault(p => p.Kind == HomsPostingKind.Rpdc);
        if (rpdc is null) return (false, null);
        if (rpdc.Status == HomsPostingStatus.Held || (rpdc.Status != HomsPostingStatus.Done && rpdc.Attempts == 0))
        {
            rpdc.Status = HomsPostingStatus.Cancelled;
            return (false, null);
        }
        if (rpdc.Status != HomsPostingStatus.Done) rpdc.Status = HomsPostingStatus.Cancelled;   // never written again
        db.HomsPostings.Add(new HomsPosting
        {
            Collection = c, Kind = HomsPostingKind.RpdcCancel, Table = rpdc.Table, MonthKey = rpdc.MonthKey, DocId = rpdc.DocId,
            DocNo = rpdc.DocNo, LinesJson = rpdc.LinesJson, CreatedBy = user,
        });
        return (true, null);
    }

    // ── watcher ─────────────────────────────────────────────────────────────

    /// <summary>Claims up to 20 postings (oldest first). A JV waits until its RPDC is written.</summary>
    public async Task<List<HomsPostingJobDto>> ClaimPendingAsync()
    {
        var now = DateTime.UtcNow;
        var cutoff = now - StaleAfter;
        var ids = await db.HomsPostings
            .Where(p => (p.Status == HomsPostingStatus.Pending || (p.Status == HomsPostingStatus.Processing && p.ClaimedAt < cutoff))
                        && !(p.Kind == HomsPostingKind.PdcJv && db.HomsPostings.Any(r => r.CollectionId == p.CollectionId
                              && r.Kind == HomsPostingKind.Rpdc && r.Status != HomsPostingStatus.Done)))
            .OrderBy(p => p.Id).Select(p => p.Id).Take(20).ToListAsync();
        var claimed = new List<int>();
        foreach (var id in ids)
        {
            var n = await db.HomsPostings
                .Where(p => p.Id == id && (p.Status == HomsPostingStatus.Pending || (p.Status == HomsPostingStatus.Processing && p.ClaimedAt < cutoff)))
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, HomsPostingStatus.Processing)
                    .SetProperty(p => p.ClaimedAt, now).SetProperty(p => p.Attempts, p => p.Attempts + 1));
            if (n == 1) claimed.Add(id);
        }
        var rows = await db.HomsPostings.AsNoTracking().Where(p => claimed.Contains(p.Id)).OrderBy(p => p.Id).ToListAsync();
        return rows.Select(p => new HomsPostingJobDto(p.Id, Label(p.Kind), p.Table, p.Table.Replace("OH", "OD").Replace("GH", "GD"),
            p.MonthKey, p.DocId, p.DocNo, JsonDocument.Parse(p.HeaderJson).RootElement, JsonDocument.Parse(p.LinesJson).RootElement)).ToList();
    }

    /// <summary>A JV's number, claimed from HOMS DOCNUM before it is written — kept so a retry reuses it.</summary>
    public async Task<string?> SetDocNoAsync(int id, int docNo)
    {
        var p = await db.HomsPostings.FindAsync(id);
        if (p is null) return "Posting not found.";
        if (p.DocNo is { } had && had != docNo) return $"This posting already has number {had}.";
        p.DocNo = docNo;
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> CompleteAsync(int id, CompleteHomsPostingDto dto)
    {
        var p = await db.HomsPostings.Include(x => x.Collection).ThenInclude(c => c.History).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return "Posting not found.";
        if (p.Status is not (HomsPostingStatus.Processing or HomsPostingStatus.Pending)) return null;   // late duplicate report
        if (dto.RetryLater)
        {
            p.Status = HomsPostingStatus.Pending;
            p.ClaimedAt = null;
            p.Message = Cut(dto.Error);
        }
        else if (!string.IsNullOrWhiteSpace(dto.Error))
        {
            p.Status = HomsPostingStatus.Failed;
            p.Message = Cut(dto.Error);
            p.Collection.History.Add(new CollectionHistory { Action = $"HOMS {Label(p.Kind)} failed", Reason = p.Message, By = "watcher" });
        }
        else if (p.Kind == HomsPostingKind.RpdcCancel)
        {
            // AlreadyPresent here = HOMS had none of it (never written, or cancelled there by hand).
            p.Status = HomsPostingStatus.Done;
            p.File = dto.File is { Length: > 0 } f ? f[..Math.Min(f.Length, 40)] : null;
            p.CompletedAt = DateTime.UtcNow;
            p.Message = dto.AlreadyPresent ? "HOMS didn't have it — nothing to cancel there." : null;
            var c = p.Collection;
            foreach (var rpdc in await db.HomsPostings.Where(x => x.CollectionId == p.CollectionId && x.Kind == HomsPostingKind.Rpdc
                                                                   && x.Status == HomsPostingStatus.Done).ToListAsync())
                rpdc.Message = $"Cancelled in HOMS {DateTime.UtcNow.AddHours(8):yyyy-MM-dd} ({c.PdcPullout ?? "pulled out"}).";
            c.History.Add(new CollectionHistory
            {
                Action = dto.AlreadyPresent ? "RPDC not in HOMS" : "RPDC cancelled in HOMS",
                Reason = dto.AlreadyPresent ? $"RPDC {p.DocNo}" : $"RPDC {p.DocNo} in {p.File}", By = "watcher",
            });
            if (c.VerificationStatus != VerificationStatus.Voided && c.PdcPullout is { } kind)
            {
                var from = c.VerificationStatus;
                c.VerificationStatus = VerificationStatus.Voided;
                c.VoidReason = Cut($"{kind}: {c.PdcPulloutReason}");
                c.History.Add(new CollectionHistory
                {
                    Action = "Voided", FromStatus = from.ToString(), ToStatus = c.VerificationStatus.ToString(), Reason = c.VoidReason,
                    By = c.PdcPulloutRequestedBy ?? "watcher",
                });
            }
        }
        else
        {
            p.Status = HomsPostingStatus.Done;
            p.File = dto.File is { Length: > 0 } f ? f[..Math.Min(f.Length, 40)] : null;
            p.CompletedAt = DateTime.UtcNow;
            p.Message = dto.AlreadyPresent ? $"Already in {p.File} — not written again." : null;
            if (p.Kind != HomsPostingKind.PdcJv) p.Collection.PostedToHomsAt = p.CompletedAt;
            p.Collection.History.Add(new CollectionHistory
            {
                Action = $"Posted to HOMS ({Label(p.Kind)})", Reason = $"{Label(p.Kind)} {p.DocNo} in {p.File}", By = "watcher",
            });
        }
        await db.SaveChangesAsync();
        return null;
    }

    private static string? Cut(string? s) => s is { Length: > 500 } ? s[..500] : s;

    // ── HOMSys side ─────────────────────────────────────────────────────────

    /// <summary>Re-builds Held postings (after the fix) and re-queues Failed ones.</summary>
    public async Task<string?> RetryAsync(int collectionId, string user)
    {
        var c = await db.Collections.Include(x => x.History).FirstOrDefaultAsync(x => x.Id == collectionId);
        if (c is null) return "Collection not found.";
        var list = await db.HomsPostings.Where(p => p.CollectionId == collectionId
            && (p.Status == HomsPostingStatus.Held || p.Status == HomsPostingStatus.Failed)).OrderBy(p => p.Id).ToListAsync();
        if (list.Count == 0) return "Nothing on hold or failed for this collection.";
        await LoadForBuildAsync(c);
        foreach (var p in list)
        {
            if (p.Status == HomsPostingStatus.Held) Fill(p, c, user);
            else { p.Status = HomsPostingStatus.Pending; p.Message = null; }
            c.History.Add(new CollectionHistory { Action = $"HOMS {Label(p.Kind)} retried", Reason = p.Message, By = user });
        }
        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>Before voiding (or un-matching): refuse if HOMS already has the document; else cancel what's queued.</summary>
    public async Task<string?> CancelOpenAsync(int collectionId, params HomsPostingKind[] kinds)
    {
        var list = await db.HomsPostings.Where(p => p.CollectionId == collectionId && kinds.Contains(p.Kind)).ToListAsync();
        if (list.FirstOrDefault(p => p.Status == HomsPostingStatus.Done) is { } done)
            return done.Kind == HomsPostingKind.Rpdc && !list.Any(p => p.Kind == HomsPostingKind.PdcJv && p.Status != HomsPostingStatus.Cancelled)
                ? $"RPDC {done.DocNo} is already posted in HOMS — use Pull out / Replace, which cancels it there too."
                : $"{Label(done.Kind)} {done.DocNo} is already posted in HOMS — cancel it there first.";
        if (list.Any(p => p.Status == HomsPostingStatus.Processing)) return "It is being written to HOMS right now — try again in a minute.";
        foreach (var p in list.Where(p => Open.Contains(p.Status))) p.Status = HomsPostingStatus.Cancelled;
        return null;
    }

    public async Task<List<HomsPostingDto>> ForCollectionAsync(int collectionId) =>
        (await db.HomsPostings.AsNoTracking().Where(p => p.CollectionId == collectionId).OrderBy(p => p.Id).ToListAsync())
        .Select(p => new HomsPostingDto(p.Id, Label(p.Kind), p.Status.ToString(), p.DocNo, p.File, p.Message, Utc(p.CreatedAt),
            p.CompletedAt is { } d ? Utc(d) : null)).ToList();
}
