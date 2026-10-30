using HOMSys.Application.DTOs.Collections;
using HOMSys.Application.Services;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Collections;

/// <summary>
/// Upserts the watcher's OH03/OD03 receipts (read-only mirror of BMS posted collections) and links them to
/// HOMSys collections, so a collection shows "Posted in BMS" and a PDC shows BMS's bounced flag.
/// </summary>
public class BmsCollectionImporter(AppDbContext db)
{
    private static string Clip(string? s, int max) => (s ?? "").Trim() is var t && t.Length > max ? t[..max] : t;

    public async Task<string> ImportAsync(BmsCollectionSyncRequest req)
    {
        var source = Clip(req.SourceFile, 12).ToUpperInvariant();
        if (source.Length == 0) throw new ArgumentException("sourceFile is required.");

        var removed = 0;
        if (req.Reset)
            removed += await db.BmsPostedCollections.Where(b => b.SourceFile == source && !b.Removed)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.Removed, true).SetProperty(b => b.CollectionId, (int?)null));
        foreach (var k in req.Removed ?? [])
            removed += await db.BmsPostedCollections.Where(b => b.SourceFile == source && b.DocId == k.DocId && b.DocNo == k.DocNo && !b.Removed)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.Removed, true).SetProperty(b => b.CollectionId, (int?)null));

        var docs = req.Docs ?? [];
        var nos = docs.Select(d => d.DocNo).ToList();
        var existing = (await db.BmsPostedCollections.Include(b => b.Lines)
                .Where(b => b.SourceFile == source && nos.Contains(b.DocNo)).ToListAsync())
            .ToDictionary(b => (b.DocId, b.DocNo));
        var sites = await db.Sites.Select(s => new { s.Id, s.Code }).ToDictionaryAsync(s => s.Code, s => s.Id);

        foreach (var d in docs)
        {
            if (!existing.TryGetValue((d.DocId, d.DocNo), out var b))
            {
                b = new BmsPostedCollection { SourceFile = source, DocId = d.DocId, DocNo = d.DocNo };
                db.BmsPostedCollections.Add(b);
            }
            else db.BmsPostedCollectionLines.RemoveRange(b.Lines);

            var lines = (d.Lines ?? []).Select(l => new BmsPostedCollectionLine
            {
                WhseNo = l.WhseNo, CustKey = Clip(l.CustKey, 7), CKey = Clip(l.CKey, 7), RefId = l.RefId, RefNo = l.RefNo, RefDate = l.RefDate,
                DrAmt = l.DrAmt, CrAmt = l.CrAmt, AcctNo = l.AcctNo, DraweeBank = Clip(l.DraweeBank, 20), CheckNo = Clip(l.CheckNo, 16),
                CheckDate = l.CheckDate, BadCheck = l.BadCheck, DepBank = Clip(l.DepBank, 16), PaymentId = Clip(l.PaymentId, 20),
                RecBank = Clip(l.RecBank, 50), Reference = Clip(l.Reference, 40), TrType = l.TrType,
            }).ToList();
            var first = lines.FirstOrDefault(l => l.CheckNo.Length > 0) ?? lines.FirstOrDefault();

            b.DocDate = d.DocDate; b.DcrNo = d.DcrNo; b.DcrDate = d.DcrDate;
            b.UserName = Clip(d.UserName, 20); b.Payor = Clip(d.Payor, 30); b.TCode = d.TCode;
            b.Posted = d.Posted; b.StatDesc = Clip(d.StatDesc, 15); b.DeptCode = d.DeptCode;
            b.Cancelled = b.StatDesc.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);
            b.CustKey = lines.Select(l => l.CustKey).FirstOrDefault(k => k.Length > 0) ?? "";
            b.ArApplied = lines.Where(l => l.AcctNo == BmsPostedCollection.ArAccount).Sum(l => l.CrAmt - l.DrAmt);
            b.Ewt = lines.Where(l => l.AcctNo == BmsPostedCollection.EwtAccount).Sum(l => l.DrAmt - l.CrAmt);
            b.Received = lines.Where(l => l.AcctNo is not (BmsPostedCollection.ArAccount or BmsPostedCollection.EwtAccount)).Sum(l => l.DrAmt - l.CrAmt);
            b.CheckNo = first?.CheckNo is { Length: > 0 } c && c.Trim('0').Length > 0 ? c : null;
            b.CheckDate = first?.CheckDate;
            b.DraweeBank = first?.DraweeBank is { Length: > 0 } db_ ? db_ : null;
            b.DepBank = first?.DepBank is { Length: > 0 } dep ? dep : null;
            b.Bounced = lines.Any(l => l.BadCheck);
            b.SiteId = SiteOf(d.DocNo, lines.FirstOrDefault(l => l.RefId == 1)?.RefNo, sites);
            b.Removed = false;
            b.SyncedAt = DateTime.UtcNow;
            b.Lines = lines;
            if (b.Cancelled) { b.CollectionId = null; b.LinkMethod = null; b.LinkedAt = null; }
        }
        await db.SaveChangesAsync();
        var linked = await LinkAsync();
        return $"{source}: {docs.Count} upserted, {removed} removed, {linked} linked";
    }

    /// <summary>Branch from an 8-digit receipt number's prefix (e.g. 93003540 → 93 = ZAM-B), else from the invoice's.</summary>
    private static int? SiteOf(int docNo, int? invoiceNo, Dictionary<string, int> sites)
    {
        foreach (var n in new[] { docNo, invoiceNo ?? 0 })
            if (n >= 10_000_000 && HomsDocMap.SiteCode(n / 1_000_000) is { } code && sites.TryGetValue(code, out var id))
                return id;
        return null;
    }

    /// <summary>
    /// Links unlinked BMS receipts to live HOMSys collections of the same customer:
    /// 1) Receipt no. — the printed CR/PR number the custodian typed is the BMS OR number (as is, or WH-prefixed);
    /// 2) Invoices — the BMS receipt credits A/R on exactly the collection's invoices, each with the collection's
    ///    applied + deductions for it (cash and EWT both reduce A/R). Only one-to-one candidates link.
    /// </summary>
    public async Task<int> LinkAsync()
    {
        var linkedIds = db.BmsPostedCollections.Where(b => b.CollectionId != null && !b.Removed).Select(b => b.CollectionId);
        var cols = await db.Collections.Include(c => c.Site).Include(c => c.Invoices).ThenInclude(i => i.PostedInvoice).Include(c => c.Deductions)
            .Where(c => c.VerificationStatus != VerificationStatus.Voided && !linkedIds.Contains(c.Id)).ToListAsync();
        if (cols.Count == 0) return 0;
        var keys = cols.Select(c => c.CustKey).Distinct().ToList();
        var docs = await db.BmsPostedCollections.Include(b => b.Lines)
            .Where(b => b.CollectionId == null && !b.Removed && !b.Cancelled && keys.Contains(b.CustKey)).ToListAsync();
        if (docs.Count == 0) return 0;

        var linked = 0;
        var used = new HashSet<int>();
        void Link(BmsPostedCollection b, Collection c, string method)
        {
            b.CollectionId = c.Id; b.LinkMethod = method; b.LinkedAt = DateTime.UtcNow;
            used.Add(b.Id); linked++;
        }

        foreach (var c in cols)
        {
            var mine = docs.Where(b => !used.Contains(b.Id) && b.CustKey == c.CustKey).ToList();
            // BMS keeps the printed CR/PR series either as printed (HO: 32102) or behind the branch's
            // 2-digit WH (Davao 5014 -> 92005014).
            var digits = new string(c.ReceiptNo.Where(char.IsDigit).ToArray());
            var nos = new List<long>();
            if (long.TryParse(digits, out var n))
            {
                nos.Add(n);
                // HOMS numbers branch receipts with its own prefix (MEX = 85, not its SL warehouse 10).
                if (n < 1_000_000 && HomsDocMap.Prefix(c.Site.Code) is > 0 and var wh) nos.Add(wh * 1_000_000L + n);
            }
            var byNo = mine.Where(b => nos.Contains(b.DocNo)).ToList();
            if (byNo.Count == 1) { Link(byNo[0], c, "Receipt no."); continue; }

            if (c.Invoices.Count == 0) continue;
            var want = c.Invoices.ToDictionary(i => i.PostedInvoice.DocNo,
                i => i.AmountApplied + c.Deductions.Where(d => d.PostedInvoiceId == i.PostedInvoiceId).Sum(d => d.Amount));
            var byInv = mine.Where(b =>
            {
                var ar = b.Lines.Where(l => l.AcctNo == BmsPostedCollection.ArAccount && l.RefId == 1)
                    .GroupBy(l => l.RefNo).ToDictionary(g => g.Key, g => g.Sum(l => l.CrAmt - l.DrAmt));
                return ar.Count == want.Count && want.All(w => ar.TryGetValue(w.Key, out var a) && Math.Abs(a - w.Value) < 0.01m);
            }).ToList();
            // The same receipt must not fit another unlinked collection too.
            if (byInv.Count == 1 && cols.Count(o => o != c && o.CustKey == c.CustKey && o.Amount == c.Amount && o.Invoices.Count == c.Invoices.Count) == 0)
                Link(byInv[0], c, "Invoices");
        }
        await db.SaveChangesAsync();
        return linked;
    }
}
