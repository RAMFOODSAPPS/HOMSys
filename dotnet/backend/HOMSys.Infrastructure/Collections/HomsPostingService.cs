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
        HomsPostingKind.Or => "OR", HomsPostingKind.Rpdc => "RPDC", HomsPostingKind.RpdcCancel => "RPDC-CANCEL",
        HomsPostingKind.Deductions => "DEDUCTIONS", _ => "JV",
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
        p.Message = CreditsAr(p.Kind) ? HomsPostingBuilder.DeductionNote(c) : null;
        p.DocNo = doc.DocNo ?? p.DocNo;
        p.HeaderJson = HomsPostingBuilder.Json(doc.Header);
        p.LinesJson = HomsPostingBuilder.Json(doc.Lines);
        SetDeductions(p, c, post, user);
    }

    /// <summary>The OR / RPDC's ordeduc2 rows (every deduction but EWT; a Service Invoice only with its scanned copy).</summary>
    private static void SetDeductions(HomsPosting p, Collection c, DateOnly posted, string user)
    {
        var (rows, ids) = CreditsAr(p.Kind) && p.DocNo is { } no
            ? HomsPostingBuilder.DeductionRows(c, p.DocId, no, user, posted, TodayPh)
            : ([], []);
        p.DeductionsJson = HomsPostingBuilder.Json(rows);
        p.DeductionIds = ids.Count == 0 ? null : string.Join(",", ids);
    }

    private static List<int> Ids(string? s) =>
        (s ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n > 0).ToList();

    /// <summary>The OR / RPDC's posting date (its header POSTED), for ordeduc2 rows sent after it.</summary>
    private static DateOnly PostedOf(HomsPosting main)
    {
        try
        {
            if (JsonDocument.Parse(main.HeaderJson).RootElement.TryGetProperty("POSTED", out var v)
                && v.ValueKind == JsonValueKind.String && DateOnly.TryParse(v.GetString(), out var d)) return d;
        }
        catch (JsonException) { }
        return main.CompletedAt is { } at ? DateOnly.FromDateTime(at.AddHours(8)) : TodayPh;
    }

    /// <summary>
    /// After a deduction became ready for HOMS (a Service Invoice's scanned copy uploaded later, user 2026-11-21), not saved:
    /// its OR / RPDC still waiting → rebuilt with it; already in HOMS → an ordeduc2-only Deductions posting is queued (or a
    /// waiting one refreshed). While one is being written nothing changes here: <see cref="CompleteAsync"/> queues the rest.
    /// </summary>
    public async Task QueueDeductionsAsync(Collection c, string user)
    {
        await LoadForBuildAsync(c);
        if (!c.Deductions.Any(d => d.ReadyForHoms)) return;
        var list = await db.HomsPostings.Where(p => p.CollectionId == c.Id && p.Status != HomsPostingStatus.Cancelled).ToListAsync();
        list.AddRange(db.HomsPostings.Local.Where(p => p.Collection == c && p.Id == 0 && p.Status != HomsPostingStatus.Cancelled));
        if (list.Any(p => p.Kind == HomsPostingKind.RpdcCancel)) return;   // pulled-out PDC: nothing goes to HOMS any more
        var main = list.Where(p => CreditsAr(p.Kind)).OrderBy(p => p.Id == 0 ? int.MaxValue : p.Id).FirstOrDefault();
        if (main is null || main.Status == HomsPostingStatus.Held) return;   // not verified yet / Retry rebuilds it
        if (main.Status is HomsPostingStatus.Pending or HomsPostingStatus.Failed)
        {
            SetDeductions(main, c, TodayPh, user);
            main.Message = HomsPostingBuilder.DeductionNote(c);
            return;
        }
        // Processing: CompleteAsync follows up. No DocNo = encoded in HOMS itself (QueueAsync found it there), and "Already
        // in" = HOMS had the OR before HOMSys wrote it: their deductions are HOMS's own. (The BMS mirror links HOMSys's own
        // ORs too, so it can't tell these apart.)
        if (main.Status != HomsPostingStatus.Done || main.DocNo is not { } docNo) return;
        if (main.Message?.StartsWith("Already in", StringComparison.Ordinal) == true) return;
        if (list.Any(p => p.Kind == HomsPostingKind.Deductions && p.Status == HomsPostingStatus.Processing)) return;

        var (rows, ids) = HomsPostingBuilder.DeductionRows(c, main.DocId, docNo, user, PostedOf(main), TodayPh);
        if (rows.Count == 0) return;
        var open = list.FirstOrDefault(p => p.Kind == HomsPostingKind.Deductions && Open.Contains(p.Status));
        var job = open ?? new HomsPosting
        {
            Collection = c, Kind = HomsPostingKind.Deductions, Table = main.Table, MonthKey = main.MonthKey, DocId = main.DocId,
            DocNo = docNo, CreatedBy = user,
        };
        job.Status = HomsPostingStatus.Pending;
        job.DeductionsJson = HomsPostingBuilder.Json(rows);
        job.DeductionIds = string.Join(",", ids);
        job.Message = null;
        if (open is not null) return;
        db.HomsPostings.Add(job);
        c.History.Add(new CollectionHistory
        {
            Action = "Queued for HOMS (ded.)", By = user,
            Reason = Cut($"{rows.Count} deduction row(s) to ordeduc2 for {Label(main.Kind)} {docNo}"),
        });
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
        // Its ordeduc2 rows (written with the RPDC or later) are taken out with it.
        var deds = list.Where(p => p.Kind is HomsPostingKind.Rpdc or HomsPostingKind.Deductions)
            .SelectMany(p => JsonDocument.Parse(p.DeductionsJson).RootElement.EnumerateArray().Select(e => e.Clone())).ToList();
        foreach (var d in list.Where(p => p.Kind == HomsPostingKind.Deductions && Open.Contains(p.Status))) d.Status = HomsPostingStatus.Cancelled;
        db.HomsPostings.Add(new HomsPosting
        {
            Collection = c, Kind = HomsPostingKind.RpdcCancel, Table = rpdc.Table, MonthKey = rpdc.MonthKey, DocId = rpdc.DocId,
            DocNo = rpdc.DocNo, LinesJson = rpdc.LinesJson, DeductionsJson = JsonSerializer.Serialize(deds), CreatedBy = user,
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
            p.MonthKey, p.DocId, p.DocNo, JsonDocument.Parse(p.HeaderJson).RootElement, JsonDocument.Parse(p.LinesJson).RootElement,
            JsonDocument.Parse(p.DeductionsJson).RootElement)).ToList();
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
        else if (p.Kind == HomsPostingKind.Deductions)
        {
            // Rows already there count as written (an earlier run got them in); the watcher never adds one twice.
            p.Status = HomsPostingStatus.Done;
            p.File = dto.File is { Length: > 0 } f ? f[..Math.Min(f.Length, 40)] : null;
            p.CompletedAt = DateTime.UtcNow;
            var n = await StampSentAsync(p);
            p.Message = dto.AlreadyPresent ? "Already in ordeduc2 — not written again." : null;
            p.Collection.History.Add(new CollectionHistory
            {
                Action = "Deductions to HOMS", Reason = Cut($"{n} row(s) in ordeduc2 for OR/RPDC {p.DocNo}"), By = "watcher",
            });
            await db.SaveChangesAsync();
            await QueueDeductionsAsync(p.Collection, "watcher");   // a Service Invoice copy uploaded meanwhile
        }
        else
        {
            p.Status = HomsPostingStatus.Done;
            p.File = dto.File is { Length: > 0 } f ? f[..Math.Min(f.Length, 40)] : null;
            p.CompletedAt = DateTime.UtcNow;
            string? left = null;
            if (CreditsAr(p.Kind))
            {
                await db.Entry(p.Collection).Collection(x => x.Deductions).LoadAsync();
                await db.Entry(p.Collection).Collection(x => x.Invoices).Query().Include(i => i.PostedInvoice).LoadAsync();
                if (dto.DeductionsWritten) await StampSentAsync(p);
                left = HomsPostingBuilder.DeductionNote(p.Collection);
                if (!dto.DeductionsWritten && p.DeductionIds is not null)
                    left = $"Deductions NOT written to ordeduc2 ({Label(p.Kind)} was already in HOMS). {left}";
            }
            p.Message = dto.AlreadyPresent ? $"Already in {p.File} — not written again." : left;
            if (p.Kind != HomsPostingKind.PdcJv) p.Collection.PostedToHomsAt = p.CompletedAt;
            p.Collection.History.Add(new CollectionHistory
            {
                Action = $"Posted to HOMS ({Label(p.Kind)})",
                Reason = Cut($"{Label(p.Kind)} {p.DocNo} in {p.File}" + (left is null ? "" : $". {left}")), By = "watcher",
            });
            if (CreditsAr(p.Kind) && dto.DeductionsWritten)
            {
                await db.SaveChangesAsync();
                await QueueDeductionsAsync(p.Collection, "watcher");   // a Service Invoice copy uploaded while it was being written
            }
        }
        await db.SaveChangesAsync();
        return null;
    }

    private static string? Cut(string? s) => s is { Length: > 500 } ? s[..500] : s;

    /// <summary>Marks the deductions a posting carried as in ordeduc2 (not saved); returns how many it carried.</summary>
    private async Task<int> StampSentAsync(HomsPosting p)
    {
        var ids = Ids(p.DeductionIds);
        if (ids.Count == 0) return 0;
        await db.Entry(p.Collection).Collection(x => x.Deductions).LoadAsync();
        var now = DateTime.UtcNow;
        foreach (var d in p.Collection.Deductions.Where(d => ids.Contains(d.Id) && d.HomsSentAt is null)) d.HomsSentAt = now;
        return ids.Count;
    }

    /// <summary>The kinds that credit the invoice in HOMS's A/R (so a deduction left out stays open there).</summary>
    private static bool CreditsAr(HomsPostingKind k) => k is HomsPostingKind.Or or HomsPostingKind.Rpdc;

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
            if (p.Status == HomsPostingStatus.Held && p.Kind != HomsPostingKind.Deductions) Fill(p, c, user);
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
