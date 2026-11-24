using HOMSys.Application.DTOs.Collections;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Collections;

/// <summary>
/// "Return from customer" deductions and the RFCs that close them (user 2026-11-24: "We cannot download in AR unless the
/// RFC for this has been posted. therefore the CNC verifier is enforced to follow up on the RFC posting").
/// A receipt with a return can't go to HOMS (its OR / RPDC is Held, <see cref="HomsPostingBuilder.AwaitingRfcNote"/>) until
/// every return on it is linked to an RFC posted in HOMS A/R (AROF4HO → PostedRfcs):
///   • automatically, when the invoice has exactly one RFC no other return has taken, of the same amount — after every
///     AROF4HO sync and when the receipt is submitted / verified;
///   • otherwise the CNC Verifier picks it on PDC › Returns awaiting RFC.
/// Linking releases the held OR / RPDC; its ordeduc2 BO RETURNS row then carries the RFC no. (REFNO), as HOMS's own do.
/// Once linked, the RFC (in the invoice's RfcTotal) is what reduces the balance, not the deduction (no double count).
/// </summary>
public class RfcFollowUpService(AppDbContext db, HomsPostingService homs)
{
    private static readonly VerificationStatus[] Live = [VerificationStatus.ForVerification, VerificationStatus.Verified];
    private const string ReturnCode = "RETURN_CUSTOMER";
    /// <summary>RfcLinkedBy of a return pre-encoded from an RFC already on the invoice when it was put on the receipt.</summary>
    public const string PreEncoded = "pre-encoded";

    /// <summary>Returns of submitted / verified receipts (not voided, not a bounced PDC); awaiting only unless all.</summary>
    private IQueryable<CollectionDeduction> Returns(bool awaitingOnly) =>
        db.CollectionDeductions.Where(d => d.Type == DeductionType.RfcBoRtv && (d.Reason == null || d.Reason == ReturnCode) && d.Amount > 0
            && Live.Contains(d.Collection.VerificationStatus) && d.Collection.PdcOutcome != PdcOutcome.Bounced
            && (!awaitingOnly || d.RfcNo == null));

    private sealed record Rfc(int No, DateOnly? Date, decimal Amt);

    /// <summary>RFCs per invoice no. (one per RFC no.; split rows summed, amount positive) and which return took each one.</summary>
    private async Task<(Dictionary<int, List<Rfc>> Rfcs, Dictionary<(int Inv, int Rfc), (int DedId, int CollectionId, string Receipt)> Taken)>
        RfcsAsync(ICollection<int> invoiceNos)
    {
        var rfcs = (await db.PostedRfcs.Where(r => invoiceNos.Contains(r.InvoiceNo) && r.RfcNo > 0)
                .Select(r => new { r.InvoiceNo, r.RfcNo, r.RfcDate, r.Amt }).ToListAsync())
            .GroupBy(r => (r.InvoiceNo, r.RfcNo))
            .Select(g => (Inv: g.Key.InvoiceNo, Rfc: new Rfc(g.Key.RfcNo, g.Max(r => r.RfcDate), Math.Round(-g.Sum(r => r.Amt), 2))))
            .GroupBy(x => x.Inv)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Rfc).OrderBy(r => r.Date).ThenBy(r => r.No).ToList());
        var taken = (await db.CollectionDeductions.Where(d => d.RfcNo != null && invoiceNos.Contains(d.PostedInvoice.DocNo)
                && d.Collection.VerificationStatus != VerificationStatus.Voided)
                .Select(d => new
                {
                    d.Id, d.CollectionId, Inv = d.PostedInvoice.DocNo, Rfc = d.RfcNo!.Value, T = d.Collection.ReceiptType, d.Collection.ReceiptNo,
                    Site = d.Collection.Site.Code,
                }).ToListAsync())
            .GroupBy(d => (d.Inv, d.Rfc))
            .ToDictionary(g => g.Key, g => (g.First().Id, g.First().CollectionId, $"{g.First().T} {g.First().ReceiptNo} {g.First().Site}"));
        return (rfcs, taken);
    }

    /// <summary>
    /// RFCs posted in HOMS A/R on these invoices that no receipt (other than <paramref name="exceptCollectionId"/>, not voided)
    /// has taken: pre-encoded as "Return from customer" rows when the invoice is put on a receipt (user 2026-11-24: the RFC
    /// already reduces the invoice, so the custodian selecting it again doubled the deduction).
    /// </summary>
    public async Task<Dictionary<int, List<RfcCandidateDto>>> FreeRfcsAsync(ICollection<int> invoiceNos, int? exceptCollectionId = null)
    {
        if (invoiceNos.Count == 0) return [];
        var (rfcs, taken) = await RfcsAsync(invoiceNos);
        return rfcs.ToDictionary(kv => kv.Key, kv => kv.Value
            .Where(r => r.Amt > 0 && !(taken.TryGetValue((kv.Key, r.No), out var t) && t.CollectionId != exceptCollectionId))
            .Select(r => new RfcCandidateDto(r.No, r.Date, r.Amt, null)).ToList());
    }

    /// <summary>
    /// Links every awaiting return (of one receipt, or all) that has exactly one free RFC of the same amount on its invoice,
    /// and releases the receipts' held OR / RPDC. Saves unless <paramref name="save"/> is false (verify saves it with the
    /// rest). Returns how many were linked.
    /// </summary>
    public async Task<int> AutoLinkAsync(int? collectionId = null, bool save = true)
    {
        var waiting = await Returns(true).Where(d => collectionId == null || d.CollectionId == collectionId)
            .Include(d => d.PostedInvoice).OrderBy(d => d.Id).ToListAsync();
        if (waiting.Count == 0) return 0;
        var (rfcs, taken) = await RfcsAsync(waiting.Select(d => d.PostedInvoice.DocNo).Distinct().ToList());
        var linked = new List<CollectionDeduction>();
        foreach (var d in waiting)
        {
            var inv = d.PostedInvoice.DocNo;
            var match = (rfcs.GetValueOrDefault(inv) ?? []).Where(r => r.Amt == d.Amount && !taken.ContainsKey((inv, r.No))).ToList();
            if (match.Count != 1) continue;
            Link(d, match[0].No, "auto");
            taken[(inv, match[0].No)] = (d.Id, d.CollectionId, "");
            linked.Add(d);
        }
        if (linked.Count == 0) return 0;
        foreach (var g in linked.GroupBy(d => d.CollectionId)) await AfterLinkAsync(g.Key, g.ToList(), "auto");
        if (save) await db.SaveChangesAsync();
        return linked.Count;
    }

    private static void Link(CollectionDeduction d, int rfcNo, string by)
    {
        d.RfcNo = rfcNo;
        d.RfcLinkedAt = DateTime.UtcNow;
        d.RfcLinkedBy = by.Length > 100 ? by[..100] : by;
    }

    private async Task AfterLinkAsync(int collectionId, List<CollectionDeduction> ds, string by)
    {
        var c = await db.Collections.FirstAsync(x => x.Id == collectionId);
        foreach (var d in ds)
            c.History.Add(new CollectionHistory
            {
                Action = "RFC linked", By = by,
                Reason = $"inv {d.PostedInvoice.DocNo} return {d.Amount:N2} → RFC {d.RfcNo}"
                         + (by == "auto" ? " (same amount, matched automatically)" : ""),
            });
        await homs.ReleaseRfcHoldAsync(c, by == "auto" ? "system" : by);
    }

    /// <summary>The CNC Verifier links a return to an RFC posted on its invoice (any amount: the difference is theirs to judge).</summary>
    public async Task<string?> LinkAsync(int deductionId, int rfcNo, string user)
    {
        var d = await Returns(false).Include(x => x.PostedInvoice).FirstOrDefaultAsync(x => x.Id == deductionId);
        if (d is null) return "Return not found (or its receipt isn't submitted / verified).";
        if (d.RfcNo is { } had) return had == rfcNo ? null : $"This return is already linked to RFC {had}.";
        var inv = d.PostedInvoice.DocNo;
        var (rfcs, taken) = await RfcsAsync([inv]);
        if (!(rfcs.GetValueOrDefault(inv) ?? []).Any(r => r.No == rfcNo))
            return $"RFC {rfcNo} isn't posted in HOMS A/R against invoice {inv} (as of the last AROF4HO sync).";
        if (taken.TryGetValue((inv, rfcNo), out var t) && t.DedId != d.Id) return $"RFC {rfcNo} is already linked to {t.Receipt}.";
        Link(d, rfcNo, user);
        await AfterLinkAsync(d.CollectionId, [d], user);
        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>Returns awaiting their RFC (oldest first), plus those linked in the last <paramref name="linkedDays"/> days.</summary>
    public async Task<List<RfcFollowUpDto>> ListAsync(int linkedDays = 0)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Max(0, linkedDays));
        var rows = await Returns(false).Where(d => d.RfcNo == null || (linkedDays > 0 && d.RfcLinkedAt >= since && d.RfcLinkedBy != PreEncoded))
            .Select(d => new
            {
                d.Id, d.CollectionId, Branch = d.Collection.Site.Code, d.Collection.ReceiptType, d.Collection.ReceiptNo,
                d.Collection.ReceiptDate, d.Collection.CustKey, d.Collection.CustName, Inv = d.PostedInvoice.DocNo,
                InvDate = d.PostedInvoice.DocDate, InvCust = d.PostedInvoice.CustKey, InvName = d.PostedInvoice.CustName,
                d.Amount, d.Reference, d.Collection.VerificationStatus,
                Since = d.Collection.VerifiedAt ?? d.Collection.SubmittedAt ?? d.Collection.CreatedAt,
                d.RfcNo, d.RfcLinkedAt, d.RfcLinkedBy,
            }).ToListAsync();
        if (rows.Count == 0) return [];
        var (rfcs, taken) = await RfcsAsync(rows.Select(r => r.Inv).Distinct().ToList());
        var ids = rows.Select(r => r.CollectionId).Distinct().ToList();
        var postings = (await db.HomsPostings.Where(p => ids.Contains(p.CollectionId) && p.Status != HomsPostingStatus.Cancelled
                && (p.Kind == HomsPostingKind.Or || p.Kind == HomsPostingKind.Rpdc))
                .Select(p => new { p.CollectionId, p.Kind, p.Status }).ToListAsync())
            .GroupBy(p => p.CollectionId).ToDictionary(g => g.Key, g => g.First());
        var now = DateTime.UtcNow;
        return rows.OrderBy(r => r.RfcNo is not null).ThenBy(r => r.Since).Select(r =>
        {
            var p = postings.GetValueOrDefault(r.CollectionId);
            var posting = p is null
                ? (r.VerificationStatus == VerificationStatus.ForVerification ? "Not verified yet" : null)
                : $"{HomsPostingService.Label(p.Kind)} " + p.Status switch
                {
                    HomsPostingStatus.Held => "on hold", HomsPostingStatus.Done => "in HOMS", HomsPostingStatus.Failed => "failed",
                    _ => "being sent to HOMS",
                };
            var at = DateTime.SpecifyKind(r.Since, DateTimeKind.Utc);
            return new RfcFollowUpDto(r.Id, r.CollectionId, r.Branch, r.ReceiptType.ToString(), r.ReceiptNo, r.ReceiptDate,
                string.IsNullOrEmpty(r.CustKey) ? r.InvCust : r.CustKey, string.IsNullOrEmpty(r.CustName) ? r.InvName : r.CustName,
                r.Inv, r.InvDate, r.Amount, r.Reference, r.VerificationStatus.ToString(), at, Math.Max(0, (int)(now - at).TotalDays),
                posting, r.RfcNo, r.RfcLinkedAt is { } la ? DateTime.SpecifyKind(la, DateTimeKind.Utc) : null, r.RfcLinkedBy,
                (rfcs.GetValueOrDefault(r.Inv) ?? []).Select(x => new RfcCandidateDto(x.No, x.Date, x.Amt,
                    taken.TryGetValue((r.Inv, x.No), out var t) && t.DedId != r.Id ? t.Receipt : null)).ToList());
        }).ToList();
    }
}
