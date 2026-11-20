using HOMSys.Application.DTOs.Collections;
using HOMSys.Application.Services;
using HOMSys.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Data;

/// <summary>
/// Applies new / changed invoices from HOMS's A/R open file (F:\HOMS\RAM\AROF4HO.DBF), sent by LegacyMasterWatcher
/// (arof_sync.py, user 2026-11-12). Idempotent. Per invoice no.:
/// - the invoice row upserts on DocNo (an SL-sourced copy is taken over in place, so collections keep pointing at it);
///   its branch WH = DocNo / 1,000,000, the same WH the SL files used;
/// - its RFC set is replaced by the group's (rows from SL files are dropped: AROF4HO owns the invoice now) and RfcTotal
///   is their sum;
/// - an invoice gone from AROF4HO is deleted, unless a collection / remittance line points at it (then it is kept).
/// </summary>
public class ArofInvoiceSyncImporter(AppDbContext db)
{
    public const string Source = "AROF4HO";
    private const int Chunk = 1000;

    public async Task<ArofSyncResult> ApplyAsync(ArofSyncRequest req)
    {
        var groups = (req.Groups ?? []).GroupBy(g => g.InvoiceNo).Select(g => g.Last()).ToDictionary(g => g.InvoiceNo);
        var removed = (req.Removed ?? []).Where(n => !groups.ContainsKey(n)).Distinct().ToList();
        var sites = await db.Sites.Select(s => new { s.Id, s.Code }).ToListAsync();
        var siteByCode = sites.GroupBy(s => s.Code, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        int? SiteOf(int wh) => BranchWarehouseMap.SiteCode(wh) is { } code && siteByCode.TryGetValue(code, out var id) ? id : null;

        int inserted = 0, updated = 0, unchanged = 0, deleted = 0, kept = 0, rfcRows = 0;
        foreach (var nos in groups.Keys.Concat(removed).Chunk(Chunk))
        {
            var invs = await db.PostedInvoices.Where(p => nos.Contains(p.DocNo)).ToListAsync();
            var byNo = invs.GroupBy(p => p.DocNo).ToDictionary(g => g.Key, g => g.ToList());
            var rfcs = await db.PostedRfcs.Where(r => nos.Contains(r.InvoiceNo)).ToListAsync();
            var rfcByNo = rfcs.GroupBy(r => r.InvoiceNo).ToDictionary(g => g.Key, g => g.ToList());

            // Invoices a collection or remittance line points at are never deleted.
            var ids = invs.Select(p => p.Id).ToList();
            var inUse = new HashSet<int>(await db.CollectionInvoices.Where(c => ids.Contains(c.PostedInvoiceId)).Select(c => c.PostedInvoiceId).ToListAsync());
            inUse.UnionWith(await db.CollectionDeductions.Where(c => ids.Contains(c.PostedInvoiceId)).Select(c => c.PostedInvoiceId).ToListAsync());
            inUse.UnionWith(await db.RemittanceLines.Where(l => l.PostedInvoiceId != null && ids.Contains(l.PostedInvoiceId.Value))
                .Select(l => l.PostedInvoiceId!.Value).ToListAsync());

            // Returns true when the invoice stays (in use): it then keeps its last known RFCs too.
            bool Drop(PostedInvoice p)
            {
                if (inUse.Contains(p.Id)) { kept++; return true; }
                db.PostedInvoices.Remove(p);
                deleted++;
                return false;
            }

            foreach (var no in nos)
            {
                var wh = no / 1_000_000;
                var existing = byNo.GetValueOrDefault(no) ?? [];
                var oldRfcs = rfcByNo.GetValueOrDefault(no) ?? [];
                if (!groups.TryGetValue(no, out var g))
                {
                    // Gone from AROF4HO: drop what came from it (an SL-only copy is left alone). An invoice a collection
                    // uses stays as last synced, RFCs included, so its balance doesn't move.
                    var stays = existing.Where(p => p.SourceFile == Source).Select(Drop).ToList().Any(x => x);
                    if (!stays) db.PostedRfcs.RemoveRange(oldRfcs.Where(r => r.SourceFile == Source));
                    continue;
                }

                // ── the invoice ──
                PostedInvoice? inv = existing.FirstOrDefault(p => p.WhseNo == wh) ?? existing.FirstOrDefault();
                if (g.Invoice is { } i)
                {
                    var siteId = SiteOf(wh);
                    var custKey = Cut(i.CustKey, 7);
                    var cKey = Cut(i.CKey, 5);
                    var custName = Cut(i.CustName, 50);
                    var poNum = i.PoNum is { Length: > 0 } po ? Cut(po, 15) : null;
                    var same = inv is not null && inv.WhseNo == wh && inv.DocDate == i.DocDate && inv.CustKey == custKey
                               && inv.CKey == cKey && inv.CustName == custName && inv.Term == i.Term && inv.PoNum == poNum
                               && inv.Total == i.Total && inv.Salesman == i.Salesman && inv.EmpNo == i.EmpNo
                               && inv.CustWhseNo == i.CustWhseNo && inv.HomsBalance == i.HomsBalance && inv.SiteId == siteId
                               && inv.SourceFile == Source;
                    if (same) unchanged++;
                    else
                    {
                        if (inv is null)
                        {
                            inv = new PostedInvoice { DocNo = no };
                            db.PostedInvoices.Add(inv);
                            inserted++;
                        }
                        else updated++;
                        inv.WhseNo = wh; inv.DocDate = i.DocDate; inv.SiteId = siteId;
                        inv.CustKey = custKey; inv.CKey = cKey; inv.CustName = custName;
                        inv.Term = i.Term; inv.PoNum = poNum; inv.Total = i.Total;
                        inv.Salesman = i.Salesman; inv.EmpNo = i.EmpNo; inv.CustWhseNo = i.CustWhseNo;
                        inv.HomsBalance = i.HomsBalance; inv.SourceFile = Source; inv.SyncedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    // Only RFCs left in AROF4HO for this number: its invoice row was removed there.
                    foreach (var p in existing.Where(p => p.SourceFile == Source)) Drop(p);
                    inv = existing.FirstOrDefault(p => p.SourceFile != Source || inUse.Contains(p.Id));   // what is left of it
                }

                // ── its RFC set: replace with the group's; SL rows go (AROF4HO owns the invoice) ──
                var want = (g.Rfcs ?? []).GroupBy(r => r.RfcNo)
                    .ToDictionary(x => x.Key, x => (Amt: x.Sum(r => r.Amt), Date: x.Select(r => r.RfcDate).FirstOrDefault(d => d is not null)));
                foreach (var o in oldRfcs)
                {
                    if (o.SourceFile != Source || !want.TryGetValue(o.RfcNo, out var v)) { db.PostedRfcs.Remove(o); continue; }
                    if (o.Amt != v.Amt || o.RfcDate != v.Date || o.WhseNo != wh) { o.Amt = v.Amt; o.RfcDate = v.Date; o.WhseNo = wh; o.SyncedAt = DateTime.UtcNow; }
                    want.Remove(o.RfcNo);
                }
                foreach (var (rfcNo, v) in want)
                    db.PostedRfcs.Add(new PostedRfc { WhseNo = wh, SourceFile = Source, InvoiceNo = no, RfcNo = rfcNo, RfcDate = v.Date, Amt = v.Amt });
                rfcRows += g.Rfcs?.Count ?? 0;

                var total = (g.Rfcs ?? []).Sum(r => r.Amt);
                if (inv is not null && inv.RfcTotal != total) inv.RfcTotal = total;
            }
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
        return new ArofSyncResult(inserted, updated, unchanged, deleted, kept, rfcRows);
    }

    private static string Cut(string? s, int n) => (s ?? "").Trim() is var t && t.Length > n ? t[..n] : t;
}
