using HOMSys.Application.DTOs.Collections;
using HOMSys.Application.Services;
using HOMSys.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Data;

/// <summary>
/// Applies one SL file's worth of posted invoices + RFC sums sent by LegacyMasterWatcher.
/// Idempotent: invoices upsert on (WhseNo, DocNo) and only rows that actually differ are
/// written; RFC rows for (SourceFile, WhseNo) are replaced and every touched invoice's
/// RfcTotal is re-summed across all files, so an RFC posted in a later month still lands.
/// </summary>
public class PostedInvoiceSyncImporter(AppDbContext db)
{
    private const int Chunk = 1000;

    public async Task<PostedInvoiceSyncResult> ApplyAsync(PostedInvoiceSyncRequest req)
    {
        var invoices = req.Invoices ?? [];
        var rfcs = req.Rfcs ?? [];

        int? siteId = null;
        var code = BranchWarehouseMap.SiteCode(req.Wh);
        if (code is not null)
            siteId = await db.Sites.Where(s => s.Code == code).Select(s => (int?)s.Id).FirstOrDefaultAsync();

        // ── invoices ─────────────────────────────────────────────────────
        int inserted = 0, updated = 0, unchanged = 0;
        var existing = new Dictionary<int, PostedInvoice>();
        foreach (var docNos in invoices.Select(i => i.DocNo).Distinct().Chunk(Chunk))
        {
            var batch = await db.PostedInvoices.Where(p => p.WhseNo == req.Wh && docNos.Contains(p.DocNo)).ToListAsync();
            foreach (var p in batch) existing[p.DocNo] = p;
        }

        foreach (var i in invoices)
        {
            if (existing.TryGetValue(i.DocNo, out var p))
            {
                if (p.DocDate == i.DocDate && p.CustKey == i.CustKey && p.CKey == i.CKey && p.CustName == (i.CustName ?? "")
                    && p.Term == i.Term && p.PoNum == i.PoNum && p.Total == i.Total
                    && p.Salesman == i.Salesman && p.EmpNo == i.EmpNo && p.CustWhseNo == i.CustWhseNo
                    && p.SiteId == siteId && p.SourceFile == req.SourceFile)
                {
                    unchanged++;
                    continue;
                }
                p.DocDate = i.DocDate; p.CustKey = i.CustKey; p.CKey = i.CKey; p.CustName = i.CustName ?? "";
                p.Term = i.Term; p.PoNum = i.PoNum; p.Total = i.Total;
                p.Salesman = i.Salesman; p.EmpNo = i.EmpNo; p.CustWhseNo = i.CustWhseNo;
                p.SiteId = siteId; p.SourceFile = req.SourceFile; p.SyncedAt = DateTime.UtcNow;
                updated++;
            }
            else
            {
                db.PostedInvoices.Add(new PostedInvoice
                {
                    WhseNo = req.Wh, DocNo = i.DocNo, DocDate = i.DocDate, SiteId = siteId,
                    CustKey = i.CustKey, CKey = i.CKey, CustName = i.CustName ?? "", Term = i.Term, PoNum = i.PoNum,
                    Total = i.Total, SourceFile = req.SourceFile,
                    Salesman = i.Salesman, EmpNo = i.EmpNo, CustWhseNo = i.CustWhseNo,
                });
                inserted++;
            }
        }

        // ── RFC sums for this file: replace the file's rows, remember which invoices moved ──
        var touched = new HashSet<int>(invoices.Select(i => i.DocNo));
        // A targeted sync only scanned for its own invoices, so it must not delete other invoices' RFC rows.
        var scope = req.ScopeInvoiceNos?.ToHashSet();
        var oldRfc = await db.PostedRfcs.Where(r => r.WhseNo == req.Wh && r.SourceFile == req.SourceFile).ToListAsync();
        if (scope is not null) oldRfc = oldRfc.Where(r => scope.Contains(r.InvoiceNo)).ToList();
        // One row per (invoice, RFC no.); an older watcher's single sum per invoice comes as RfcNo 0 and is
        // replaced by the numbered rows the next time the invoice is fetched.
        var newRfc = rfcs.Where(r => scope is null || scope.Contains(r.InvoiceNo))
            .GroupBy(r => (r.InvoiceNo, r.RfcNo))
            .ToDictionary(g => g.Key, g => (Amt: g.Sum(x => x.Amt), Date: g.Select(x => x.RfcDate).FirstOrDefault(d => d is not null)));

        foreach (var o in oldRfc)
        {
            var key = (o.InvoiceNo, o.RfcNo);
            if (!newRfc.TryGetValue(key, out var v)) { db.PostedRfcs.Remove(o); touched.Add(o.InvoiceNo); }
            else if (o.Amt != v.Amt || o.RfcDate != v.Date) { o.Amt = v.Amt; o.RfcDate = v.Date; o.SyncedAt = DateTime.UtcNow; touched.Add(o.InvoiceNo); }
            newRfc.Remove(key);
        }
        foreach (var ((invNo, rfcNo), v) in newRfc)
        {
            db.PostedRfcs.Add(new PostedRfc { WhseNo = req.Wh, SourceFile = req.SourceFile, InvoiceNo = invNo, RfcNo = rfcNo, RfcDate = v.Date, Amt = v.Amt });
            touched.Add(invNo);
        }
        await db.SaveChangesAsync();

        // ── re-sum RfcTotal across every file for the touched invoices ──
        int recomputed = 0;
        foreach (var nos in touched.Chunk(Chunk))
        {
            var sums = await db.PostedRfcs.Where(r => r.WhseNo == req.Wh && nos.Contains(r.InvoiceNo))
                .GroupBy(r => r.InvoiceNo).Select(g => new { InvoiceNo = g.Key, Sum = g.Sum(x => x.Amt) })
                .ToDictionaryAsync(x => x.InvoiceNo, x => x.Sum);
            var invs = await db.PostedInvoices.Where(p => p.WhseNo == req.Wh && nos.Contains(p.DocNo)).ToListAsync();
            foreach (var p in invs)
            {
                var sum = sums.GetValueOrDefault(p.DocNo);
                if (p.RfcTotal == sum) continue;
                p.RfcTotal = sum;
                recomputed++;
            }
        }
        await db.SaveChangesAsync();

        return new PostedInvoiceSyncResult(inserted, updated, unchanged, rfcs.Count, recomputed, siteId is not null);
    }
}
