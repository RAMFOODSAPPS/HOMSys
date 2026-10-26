using HOMSys.Application.DTOs.Collections;
using HOMSys.Application.Interfaces;
using HOMSys.Application.Services;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HOMSys.Infrastructure.Collections;

/// <summary>
/// Branch collection encoding (Invoice Custodian): draft -> attach receipt -> submit for verification.
/// Invoice balances are computed here from the SL-mirrored PostedInvoices — never from arof4win.
/// </summary>
public partial class CollectionService(AppDbContext db, IAttachmentStore store, BankMatchService matcher, HomsPostingService homs,
    InvoiceSyncRequestService invoiceRequests, IConfiguration config)
{
    public const long MaxAttachmentBytes = 10 * 1024 * 1024;
    private const int MaxAttachments = 10;

    private static DateOnly Today => AnalyticsService.TodayPh();
    private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

    // ── access ──────────────────────────────────────────────────────────────

    /// <summary>Encode/edit: the custodian's assigned sites; Finance Manager and Custodian Verifier: any site.</summary>
    private async Task<bool> CanWriteSiteAsync(CollectionUser u, int siteId) =>
        u.CanManage || u.CanEncodeRa || (u.CanEncode && await db.UserBranches.AnyAsync(b => b.UserId == u.Id && b.SiteId == siteId));

    /// <summary>Read: assigned sites, or any site for finance (verify/manage) and the CNC Verifier.</summary>
    private async Task<bool> CanReadSiteAsync(CollectionUser u, int siteId) =>
        u.CanVerify || u.CanConfirmPdc || await CanWriteSiteAsync(u, siteId);

    /// <summary>A live PDC is on this slip: its bank match is confirmed by the CNC Verifier only (that posts the JV).</summary>
    private Task<bool> PdcSlipAsync(int batchId) =>
        db.Collections.AnyAsync(x => x.DepositBatchId == batchId && x.PaymentMode == PaymentMode.PostDatedCheck
                                     && x.VerificationStatus != VerificationStatus.Voided);

    public async Task<List<CollectionSiteDto>> GetMySitesAsync(CollectionUser u)
    {
        var q = db.Sites.Where(s => s.IsActive);
        if (!u.CanManage && !u.CanVerify && !u.CanConfirmPdc && !u.CanTagPdc && !u.CanEncodeRa)
            q = q.Where(s => db.UserBranches.Any(b => b.UserId == u.Id && b.SiteId == s.Id));
        var sites = await q.OrderBy(s => s.Name).Select(s => new CollectionSiteDto(s.Id, s.Code, s.Name)).ToListAsync();
        // Only branches with an SL warehouse can have posted invoices.
        return sites.Where(s => BranchWarehouseMap.Wh(s.Code) is not null).ToList();
    }

    // ── balances ────────────────────────────────────────────────────────────

    /// <summary>Applied + deductions per invoice from live collections (not voided, not a bounced PDC).</summary>
    private async Task<Dictionary<int, decimal>> PaidAsync(ICollection<int> invoiceIds, int? excludeCollectionId = null)
    {
        var paid = new Dictionary<int, decimal>();
        if (invoiceIds.Count == 0) return paid;
        var applied = await db.CollectionInvoices
            .Where(x => invoiceIds.Contains(x.PostedInvoiceId) && x.CollectionId != excludeCollectionId
                        && x.Collection.VerificationStatus != VerificationStatus.Voided && x.Collection.PdcOutcome != PdcOutcome.Bounced)
            .GroupBy(x => x.PostedInvoiceId).Select(g => new { g.Key, Sum = g.Sum(x => x.AmountApplied) }).ToListAsync();
        var deducted = await db.CollectionDeductions
            .Where(x => invoiceIds.Contains(x.PostedInvoiceId) && x.CollectionId != excludeCollectionId
                        && x.Collection.VerificationStatus != VerificationStatus.Voided && x.Collection.PdcOutcome != PdcOutcome.Bounced)
            .GroupBy(x => x.PostedInvoiceId).Select(g => new { g.Key, Sum = g.Sum(x => x.Amount) }).ToListAsync();
        foreach (var a in applied) paid[a.Key] = a.Sum;
        foreach (var d in deducted) paid[d.Key] = paid.GetValueOrDefault(d.Key) + d.Sum;
        return paid;
    }

    private static decimal Balance(PostedInvoice i, decimal paid) => i.Total + i.RfcTotal - paid;

    /// <summary>One invoice's payment history (read-only): RFCs and every collection applied to it, voided and
    /// bounced ones included but flagged as not counted.</summary>
    public async Task<(InvoiceHistoryDto? data, string? error)> InvoiceHistoryAsync(CollectionUser u, int postedInvoiceId)
    {
        var inv = await db.PostedInvoices.Include(i => i.Site).FirstOrDefaultAsync(i => i.Id == postedInvoiceId);
        if (inv?.SiteId is not { } siteId || !await CanReadSiteAsync(u, siteId)) return (null, "Invoice not found.");

        var rfcs = await db.PostedRfcs.Where(r => r.WhseNo == inv.WhseNo && r.InvoiceNo == inv.DocNo)
            .OrderBy(r => r.Id).Select(r => r.Amt).ToListAsync();
        var lines = await db.CollectionInvoices.Where(x => x.PostedInvoiceId == inv.Id)
            .Select(x => new
            {
                x.CollectionId, x.AmountApplied, Site = x.Collection.Site.Code, x.Collection.ReceiptType, x.Collection.ReceiptNo,
                x.Collection.ReceiptDate, x.Collection.PaymentMode, x.Collection.PaymentStatus, x.Collection.CheckNo, x.Collection.CheckDate,
                x.Collection.VerificationStatus, x.Collection.PdcOutcome, x.Collection.CreatedBy,
            }).ToListAsync();
        var deds = (await db.CollectionDeductions.Where(d => d.PostedInvoiceId == inv.Id)
                .Select(d => new { d.CollectionId, d.Type, d.Amount, d.Reference }).ToListAsync())
            .GroupBy(d => d.CollectionId).ToDictionary(g => g.Key, g => g.ToList());

        var today = Today;
        var payments = lines.OrderBy(l => l.ReceiptDate).ThenBy(l => l.CollectionId).Select(l =>
        {
            var mine = deds.GetValueOrDefault(l.CollectionId) ?? [];
            var why = l.VerificationStatus == VerificationStatus.Voided ? "Voided"
                : l.PdcOutcome == PdcOutcome.Bounced ? "Bounced PDC" : null;
            return new InvoicePaymentDto(l.CollectionId, l.Site, l.ReceiptType.ToString(), l.ReceiptNo, l.ReceiptDate,
                l.PaymentMode.ToString(), l.CheckNo, l.CheckDate, l.VerificationStatus.ToString(),
                PdcState(l.PaymentMode, l.PaymentStatus, l.CheckDate, l.PdcOutcome, today), l.CreatedBy,
                l.AmountApplied, mine.Sum(d => d.Amount), mine.Select(d => new InvoiceDeductionDto(d.Type.ToString(), d.Amount, d.Reference)).ToList(),
                why is null, why, l.PaymentStatus.ToString());
        }).ToList();
        // Deductions on a collection that has no applied line for this invoice (shouldn't happen, but never hide money).
        foreach (var (cid, list) in deds.Where(d => lines.All(l => l.CollectionId != d.Key)))
            payments.Add(new InvoicePaymentDto(cid, "", "", $"#{cid}", inv.DocDate, "", null, null, "", null, "", 0, list.Sum(d => d.Amount),
                list.Select(d => new InvoiceDeductionDto(d.Type.ToString(), d.Amount, d.Reference)).ToList(), true, null));

        var paid = (await PaidAsync([inv.Id])).GetValueOrDefault(inv.Id);
        return (new InvoiceHistoryDto(inv.Id, inv.DocNo, inv.DocDate, inv.Site?.Code ?? "", inv.CustKey, inv.CustName, inv.PoNum, inv.Term,
            inv.Total, inv.RfcTotal, rfcs, payments, paid, Balance(inv, paid)), null);
    }

    /// <summary>Posted invoices of a branch, by customer and/or invoice numbers, with open balances.</summary>
    public async Task<(List<OpenInvoiceDto>? data, string? error)> GetInvoicesAsync(
        CollectionUser u, int siteId, string? custKey, IReadOnlyCollection<int>? docNos, bool openOnly)
    {
        if (!await CanReadSiteAsync(u, siteId)) return (null, "You are not assigned to this branch.");
        if (string.IsNullOrWhiteSpace(custKey) && (docNos is null || docNos.Count == 0))
            return (null, "Give a customer key or invoice numbers.");

        var q = db.PostedInvoices.Where(i => i.SiteId == siteId);
        if (!string.IsNullOrWhiteSpace(custKey)) q = q.Where(i => i.CustKey == custKey.Trim());
        if (docNos is { Count: > 0 }) q = q.Where(i => docNos.Contains(i.DocNo));
        var invoices = await q.OrderBy(i => i.DocDate).ThenBy(i => i.DocNo).Take(500).ToListAsync();

        var paid = await PaidAsync(invoices.Select(i => i.Id).ToList());
        var rows = invoices.Select(i => new OpenInvoiceDto(i.Id, i.DocNo, i.DocDate, i.CustKey, i.CustName, i.Term, i.PoNum,
            i.Total, i.RfcTotal, paid.GetValueOrDefault(i.Id), Balance(i, paid.GetValueOrDefault(i.Id))));
        return ((openOnly ? rows.Where(r => r.Balance != 0) : rows).ToList(), null);
    }

    // ── list / get ──────────────────────────────────────────────────────────

    public async Task<List<CollectionListItemDto>> ListAsync(CollectionUser u, int? siteId, string? status)
    {
        var q = db.Collections.AsQueryable();
        if (!u.CanManage && !u.CanVerify && !u.CanEncodeRa)
            q = q.Where(c => db.UserBranches.Any(b => b.UserId == u.Id && b.SiteId == c.SiteId));
        if (siteId is not null) q = q.Where(c => c.SiteId == siteId);
        if (Enum.TryParse<VerificationStatus>(status, true, out var vs)) q = q.Where(c => c.VerificationStatus == vs);

        var today = Today;
        var rows = await q.OrderByDescending(c => c.Id).Take(2000).Select(c => new
        {
            c.Id, c.SiteId, SiteCode = c.Site.Code, c.ReceiptType, c.ReceiptNo, c.ReceiptDate, c.CustKey, c.CustName,
            c.PaymentMode, c.Amount, c.UnappliedAmount, InvoiceCount = c.Invoices.Count, c.PaymentStatus,
            c.VerificationStatus, c.CheckDate, c.PdcOutcome, Slip = c.DepositBatch != null ? c.DepositBatch.SlipRefNo : null,
            c.CreatedBy, c.CreatedAt, c.SubmittedAt, c.RemittanceId,
            InBms = db.BmsPostedCollections.Any(b => b.CollectionId == c.Id && !b.Removed),
            BankMatch = c.DepositBatch == null || c.DepositBatch.MatchedBankLineId == null ? null
                : c.DepositBatch.MatchConfirmedAt != null ? "Approved" : "Found",
            Homs = c.HomsPostings.Any(p => p.Status == HomsPostingStatus.Failed) ? "Failed"
                : c.HomsPostings.Any(p => p.Status == HomsPostingStatus.Held) ? "Held"
                : c.HomsPostings.Any(p => p.Status == HomsPostingStatus.Pending || p.Status == HomsPostingStatus.Processing) ? "Queued"
                : c.HomsPostings.Any(p => p.Status == HomsPostingStatus.Done) ? "Posted" : null,
        }).ToListAsync();
        return rows.Select(c => new CollectionListItemDto(c.Id, c.SiteId, c.SiteCode, c.ReceiptType.ToString(), c.ReceiptNo,
            c.ReceiptDate, c.CustKey, c.CustName, c.PaymentMode.ToString(), c.Amount, c.UnappliedAmount, c.InvoiceCount,
            c.PaymentStatus.ToString(), c.VerificationStatus.ToString(),
            PdcState(c.PaymentMode, c.PaymentStatus, c.CheckDate, c.PdcOutcome, today), c.CheckDate, c.Slip,
            c.CreatedBy, Utc(c.CreatedAt), c.SubmittedAt is { } s ? Utc(s) : null, c.InBms, c.BankMatch, c.Homs, c.RemittanceId)).ToList();
    }

    /// <summary>Immature / Matured / Deposited / Succeeded / Bounced for PDCs; null otherwise.</summary>
    public static string? PdcState(PaymentMode mode, PaymentStatus pay, DateOnly? checkDate, PdcOutcome? outcome, DateOnly today) =>
        mode != PaymentMode.PostDatedCheck ? null
        : outcome is { } o ? o.ToString()
        : pay == PaymentStatus.Deposited ? "Deposited"
        : checkDate > today ? "Immature" : "Matured";

    private Task<Collection?> LoadAsync(int id) => db.Collections
        .Include(c => c.Site).Include(c => c.DepositBatch)
        .Include(c => c.Invoices).ThenInclude(i => i.PostedInvoice)
        .Include(c => c.Deductions).ThenInclude(d => d.PostedInvoice)
        .Include(c => c.Attachments).Include(c => c.History).Include(c => c.RemittanceLines)
        .AsSplitQuery().FirstOrDefaultAsync(c => c.Id == id);

    public async Task<CollectionDto?> GetAsync(CollectionUser u, int id)
    {
        var c = await LoadAsync(id);
        if (c is null || !await CanReadSiteAsync(u, c.SiteId)) return null;
        return await ToDtoAsync(u, c);
    }

    private async Task<CollectionDto> ToDtoAsync(CollectionUser u, Collection c)
    {
        var canWrite = await CanWriteSiteAsync(u, c.SiteId);
        var editable = c.VerificationStatus is VerificationStatus.Draft or VerificationStatus.Returned;
        var single = c.RemittanceId is null;   // an RA's CR is edited / submitted / voided through its RA
        var live = c.VerificationStatus != VerificationStatus.Voided && c.PdcOutcome != PdcOutcome.Bounced;
        var paid = await PaidAsync(c.Invoices.Select(i => i.PostedInvoiceId).ToList());
        var dedByInv = c.Deductions.GroupBy(d => d.PostedInvoiceId).ToDictionary(g => g.Key, g => g.Sum(d => d.Amount));
        var today = Today;

        var invoices = c.Invoices.OrderBy(i => i.PostedInvoice.DocNo).Select(i =>
        {
            var bal = Balance(i.PostedInvoice, paid.GetValueOrDefault(i.PostedInvoiceId));
            var own = live ? i.AmountApplied + dedByInv.GetValueOrDefault(i.PostedInvoiceId) : 0;
            return new CollectionInvoiceDto(i.PostedInvoiceId, i.PostedInvoice.DocNo, i.PostedInvoice.DocDate, i.PostedInvoice.CustKey,
                i.PostedInvoice.CustName, i.PostedInvoice.Total, i.PostedInvoice.RfcTotal, i.AmountApplied, bal, bal + own);
        }).ToList();

        var isPdc = c.PaymentMode == PaymentMode.PostDatedCheck;
        var bms = await db.BmsPostedCollections.Where(b => b.CollectionId == c.Id && !b.Removed)
            .Select(b => new BmsPostingDto(b.DocNo, b.DocDate, b.Posted, b.Received, b.Ewt, b.ArApplied, b.Bounced, b.LinkMethod ?? ""))
            .FirstOrDefaultAsync();
        var postings = await homs.ForCollectionAsync(c.Id);
        var batch = c.DepositBatch;
        var bankLine = batch?.MatchedBankLineId is { } lineId
            ? await db.BankStatementLines.Where(l => l.Id == lineId)
                .Select(l => new MatchedLineDto(l.Id, l.BankAccount.BankCode + " " + l.BankAccount.AccountNo, l.TxnDate, l.Credit,
                    l.Reference, l.Description, l.MatchMethod, batch.MatchConfirmedAt != null, batch.MatchConfirmedBy))
                .FirstOrDefaultAsync()
            : null;
        return new CollectionDto(
            c.Id, c.CollectionKey, c.SiteId, c.Site.Code, c.Site.Name,
            c.ReceiptType.ToString(), c.ReceiptNo, c.ReceiptDate, c.CustKey, c.CustName,
            c.PaymentMode.ToString(), c.Amount, c.UnappliedAmount,
            c.Bank, c.CheckNo, c.CheckDate,
            c.DepositBatch is { } b ? new DepositInfoDto(b.BankAccountId, b.Bank, b.SlipRefNo, b.DepositDate) : null,
            bankLine?.Confirmed == true,
            c.PaymentStatus.ToString(), c.VerificationStatus.ToString(),
            PdcState(c.PaymentMode, c.PaymentStatus, c.CheckDate, c.PdcOutcome, today), c.PdcOutcome?.ToString(),
            c.PdcOutcomeDate, c.PdcOutcomeReason, c.PdcOutcomeBy,
            c.ReturnReason, c.VoidReason, c.Remarks,
            c.CreatedBy, Utc(c.CreatedAt), c.SubmittedAt is { } s ? Utc(s) : null, c.VerifiedAt is { } v ? Utc(v) : null, c.VerifiedBy,
            CanEdit: single && canWrite && editable,
            CanSubmit: single && canWrite && editable,
            CanVoid: single && c.VerificationStatus != VerificationStatus.Voided && (u.CanManage || (canWrite && editable)),
            CanRecordDeposit: (canWrite || u.CanVerify || u.CanConfirmPdc) && isPdc && c.VerificationStatus != VerificationStatus.Voided
                              && c.PdcOutcome is null && c.CheckDate <= today && c.DepositBatch?.MatchedBankLineId is null,
            CanVerify: CanVerifyThis(u, c),
            invoices,
            c.Deductions.OrderBy(d => d.Id).Select(d => new CollectionDeductionDto(d.Id, d.PostedInvoiceId, d.PostedInvoice.DocNo,
                d.Type.ToString(), d.Amount, d.Reference)).ToList(),
            c.Attachments.OrderBy(a => a.Id).Select(a => new CollectionAttachmentDto(a.Id, a.FileName, a.ContentType, a.Size,
                Utc(a.UploadedAt), a.UploadedBy)).ToList(),
            c.History.OrderBy(h => h.Id).Select(h => new CollectionHistoryDto(h.Action, h.FromStatus, h.ToStatus, h.Reason,
                Utc(h.At), h.By)).ToList(),
            bms, c.MultiCustomer, bankLine,
            CanConfirmMatch: CanConfirmMatchThis(u, c, bankLine),
            Missing: editable ? Missing(c) : [],
            HomsPostings: postings,
            CanRetryHoms: u.CanManage && postings.Any(p => p.Status is "Held" or "Failed"),
            RemittanceId: c.RemittanceId, HomsDeptCode: c.HomsDeptCode, DcrNo: c.DcrNo);
    }

    /// <summary>Finance Manager, after fixing what held it (e.g. the deposit account's GL): build / send it again.</summary>
    public async Task<(CollectionDto? dto, string? error)> RetryHomsAsync(CollectionUser u, int id)
    {
        if (!u.CanManage) return (null, "Only the Finance Manager can retry a HOMS posting.");
        if (await homs.RetryAsync(id, u.Name) is { } error) return (null, error);
        return (await GetAsync(u, id), null);
    }

    /// <summary>A verified CR whose automatic match is still pending (found after verification).
    /// While it's For Verification, Verify approves the match together with the entry.</summary>
    private static bool CanConfirmMatchThis(CollectionUser u, Collection c, MatchedLineDto? line) =>
        u.CanVerify && line is { Confirmed: false } && c.VerificationStatus == VerificationStatus.Verified && c.PdcOutcome is null
        && c.PaymentMode != PaymentMode.PostDatedCheck;   // a PDC's match is the CNC Verifier's (PDC Monitor)

    // ── create / update ─────────────────────────────────────────────────────

    public async Task<(CollectionDto? dto, string? error)> CreateAsync(CollectionUser u, SaveCollectionDto dto)
    {
        if (!await CanWriteSiteAsync(u, dto.SiteId)) return (null, "You are not assigned to this branch.");
        var c = new Collection { SiteId = dto.SiteId, CreatedBy = u.Name };
        var error = await ApplyAsync(u, c, dto);
        if (error is not null) return (null, error);
        db.Collections.Add(c);
        History(c, "Created", null, c.VerificationStatus, null, u.Name);
        return await SaveAndReturnAsync(u, c);
    }

    public async Task<(CollectionDto? dto, string? error)> UpdateAsync(CollectionUser u, int id, SaveCollectionDto dto)
    {
        var c = await LoadAsync(id);
        if (c is null || !await CanReadSiteAsync(u, c.SiteId)) return (null, "Collection not found.");
        if (!await CanWriteSiteAsync(u, c.SiteId)) return (null, "You are not assigned to this branch.");
        if (c.VerificationStatus is not (VerificationStatus.Draft or VerificationStatus.Returned))
            return (null, $"A collection that is {c.VerificationStatus} can no longer be edited.");
        if (dto.SiteId != c.SiteId) return (null, "The branch of a collection can't be changed.");
        if (c.RemittanceId is { } raId) return (null, InRa(raId));

        var error = await ApplyAsync(u, c, dto);
        if (error is not null) return (null, error);
        c.UpdatedAt = DateTime.UtcNow;
        c.UpdatedBy = u.Name;
        History(c, "Edited", null, null, null, u.Name);
        return await SaveAndReturnAsync(u, c);
    }

    /// <summary>Validates and copies the form onto the entity (children replaced). Returns an error message or null.</summary>
    private async Task<string?> ApplyAsync(CollectionUser u, Collection c, SaveCollectionDto dto)
    {
        if (!Enum.TryParse<ReceiptType>(dto.ReceiptType, true, out var rt)) return "Choose the receipt type (CR, PR or AR).";
        if (!Enum.TryParse<PaymentMode>(dto.PaymentMode, true, out var mode)) return "Choose the payment mode.";
        // RAM collects by bank transfer / direct deposit or a current-dated cheque (CR), or a post-dated check (PR/AR)
        // (user 2026-10-07 / 2026-10-08); Cash stays in the enum for rows saved earlier.
        if (mode is PaymentMode.Cash)
            return "Only bank transfer / direct deposit or cheque (CR), or post-dated check (PR/AR) is accepted.";
        if (rt is ReceiptType.PR or ReceiptType.AR && mode != PaymentMode.PostDatedCheck)
            return "A Provisional / Acknowledgement Receipt is only for a post-dated check.";
        if (rt == ReceiptType.CR && mode == PaymentMode.PostDatedCheck)
            return "A post-dated check is receipted with a PR or AR, not a CR.";

        var receiptNo = (dto.ReceiptNo ?? "").Trim();
        if (receiptNo.Length is 0 or > 20) return "Enter the receipt number (up to 20 characters).";
        if (dto.ReceiptDate > Today.AddDays(1)) return "The receipt date is in the future.";
        if (await db.Collections.AnyAsync(x => x.SiteId == c.SiteId && x.ReceiptType == rt && x.ReceiptNo == receiptNo && x.Id != c.Id))
            return $"{rt} {receiptNo} is already encoded for this branch.";

        var custKey = (dto.CustKey ?? "").Trim();
        if (custKey.Length is 0 or > 7) return "Enter the customer key.";

        var amount = Math.Round(dto.Amount, 2);
        if (amount <= 0) return "The amount received must be more than zero.";

        string? bank = null, checkNo = null;
        DateOnly? checkDate = null;
        if (mode is PaymentMode.Check or PaymentMode.PostDatedCheck)
        {
            bank = dto.Bank?.Trim();
            checkNo = dto.CheckNo?.Trim();
            checkDate = dto.CheckDate;
            if (string.IsNullOrEmpty(bank) || string.IsNullOrEmpty(checkNo) || checkDate is null)
                return "Enter the check's bank, number and date.";
            if (bank.Length > 50 || checkNo.Length > 20) return "Check bank / number is too long.";
            if (mode == PaymentMode.Check && checkDate > dto.ReceiptDate)
                return "The cheque is dated after the receipt date — a post-dated check is receipted with a PR or AR.";
        }

        // Allocations + deductions against posted invoices of this branch.
        var allocs = (dto.Invoices ?? []).ToList();
        var deds = (dto.Deductions ?? []).ToList();
        if (allocs.Select(a => a.PostedInvoiceId).Distinct().Count() != allocs.Count) return "An invoice is listed twice.";
        var ids = allocs.Select(a => a.PostedInvoiceId).ToList();
        var invoices = await db.PostedInvoices.Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
        // An RA's CR (Head Office) applies a chain's invoices from every branch.
        if (invoices.Count != ids.Count || (!InRemittance(c) && invoices.Values.Any(i => i.SiteId != c.SiteId)))
            return "An invoice doesn't belong to this branch — fetch it again.";
        // One receipt (CR / PR / AR) is for one customer only, unless it's a chain account paying several (user 2026-10-07).
        if (!dto.MultiCustomer && invoices.Values.FirstOrDefault(i => !string.Equals(i.CustKey?.Trim(), custKey, StringComparison.OrdinalIgnoreCase)) is { } other)
            return $"Invoice {other.DocNo} is for customer {other.CustKey}, not {custKey} — one receipt covers one customer only.";

        var newDeds = new List<CollectionDeduction>();
        foreach (var d in deds)
        {
            if (!invoices.ContainsKey(d.PostedInvoiceId)) return "A deduction must be on one of the collection's invoices.";
            if (!Enum.TryParse<DeductionType>(d.Type, true, out var dt)) return "Choose the deduction type.";
            var amt = Math.Round(d.Amount, 2);
            if (amt <= 0) return "A deduction amount must be more than zero.";
            newDeds.Add(new CollectionDeduction
            {
                PostedInvoiceId = d.PostedInvoiceId, Type = dt, Amount = amt,
                Reference = d.Reference?.Trim() is { Length: > 0 } r ? r[..Math.Min(r.Length, 100)] : null,
            });
        }

        var paid = await PaidAsync(ids, c.Id == 0 ? null : c.Id);
        decimal appliedTotal = 0;
        foreach (var a in allocs)
        {
            var applied = Math.Round(a.AmountApplied, 2);
            var deducted = newDeds.Where(x => x.PostedInvoiceId == a.PostedInvoiceId).Sum(x => x.Amount);
            var inv = invoices[a.PostedInvoiceId];
            if (applied < 0) return $"Invoice {inv.DocNo}: the applied amount can't be negative.";
            if (applied + deducted <= 0) return $"Invoice {inv.DocNo}: enter the amount applied (or a deduction).";
            var open = Balance(inv, paid.GetValueOrDefault(inv.Id));
            if (applied + deducted > open)
                return $"Invoice {inv.DocNo}: {applied + deducted:N2} is more than its open balance of {open:N2}.";
            appliedTotal += applied;
        }
        if (appliedTotal > amount)
            return $"The amounts applied ({appliedTotal:N2}) are more than the amount received ({amount:N2}).";

        // Deposit slip: CR money is deposited before encoding; a PDC only on/after its check date.
        // An RA's CRs share the RA's slip (set by the RA, see AssignRaDepositAsync).
        if (!InRemittance(c) && await AssignDepositAsync(c, dto.Deposit, mode, checkDate, u.Name) is { } batchError) return batchError;

        c.ReceiptType = rt;
        c.ReceiptNo = receiptNo;
        c.ReceiptDate = dto.ReceiptDate;
        c.CustKey = custKey;
        c.MultiCustomer = dto.MultiCustomer && invoices.Values.Select(i => i.CustKey).Distinct().Count() > 1;
        c.CustName = invoices.Values.FirstOrDefault(i => i.CustKey == custKey)?.CustName
                     ?? await db.PostedInvoices.Where(i => i.CustKey == custKey).Select(i => i.CustName).FirstOrDefaultAsync()
                     ?? await db.Customers.Where(x => x.CustKey == custKey).Select(x => x.CusName).FirstOrDefaultAsync()
                     ?? "";
        c.PaymentMode = mode;
        c.Amount = amount;
        c.UnappliedAmount = amount - appliedTotal;
        c.Bank = bank;
        c.CheckNo = checkNo;
        c.CheckDate = checkDate;
        c.Remarks = dto.Remarks?.Trim() is { Length: > 0 } rem ? rem[..Math.Min(rem.Length, 500)] : null;

        // Replace children.
        db.CollectionInvoices.RemoveRange(c.Invoices);
        db.CollectionDeductions.RemoveRange(c.Deductions);
        c.Invoices = allocs.Select(a => new CollectionInvoice { PostedInvoiceId = a.PostedInvoiceId, AmountApplied = Math.Round(a.AmountApplied, 2) }).ToList();
        c.Deductions = newDeds;
        return null;
    }

    /// <summary>Company accounts a branch deposits into: those linked to the site, else every active account.
    /// Only accounts with a HOMS GL account (BANKS4W) — the OR / JV HOMSys posts debits it.</summary>
    public async Task<List<BankAccount>> DepositAccountsAsync(int siteId)
    {
        var usable = db.BankAccounts.Where(a => a.IsActive && a.HomsAcctNo != null);
        var linked = await usable.Where(a => a.Sites.Any(s => s.SiteId == siteId))
            .OrderBy(a => a.BankCode).ThenBy(a => a.AccountNo).ToListAsync();
        return linked.Count > 0 ? linked : await usable.OrderBy(a => a.BankCode).ThenBy(a => a.AccountNo).ToListAsync();
    }

    /// <summary>Links the collection to its deposit: with a slip / transfer ref. no., find-or-create per branch + account + ref
    /// (collections deposited together share it); without one (optional, user 2026-10-15), the deposit is the collection's own.</summary>
    private async Task<string?> AssignDepositAsync(Collection c, DepositInfoDto? dep, PaymentMode mode, DateOnly? checkDate, string user)
    {
        if (c.DepositBatch is { MatchedBankLineId: { } lineId } matched)
        {
            var same = dep is not null && dep.BankAccountId == matched.BankAccountId && (dep.SlipRefNo ?? "").Trim() == matched.SlipRefNo
                       && dep.DepositDate == matched.DepositDate;
            if (same) return null;
            if (matched.MatchConfirmedAt is not null) return "The deposit slip is already matched to the bank statement and can't be changed.";
            // Only a suggested (unapproved) match: the corrected slip gets matched afresh.
            if (await db.BankStatementLines.FindAsync(lineId) is { } line)
                await matcher.UnapplyAsync(matched, line, user, "Deposit slip corrected before the match was approved");
        }
        if (dep is null || (dep.BankAccountId is null && string.IsNullOrWhiteSpace(dep.SlipRefNo)))
        {
            c.DepositBatch = null;
            c.DepositBatchId = null;
            return null;
        }

        if (dep.BankAccountId is not { } accountId) return "Choose the company bank account the money was deposited into.";
        var account = (await DepositAccountsAsync(c.SiteId)).FirstOrDefault(a => a.Id == accountId);
        if (account is null) return "That bank account isn't available for this branch.";
        var bank = account.Label[..Math.Min(account.Label.Length, 50)];
        var slip = (dep.SlipRefNo ?? "").Trim();
        if (slip.Length > 30) return "The slip / reference number is too long (30 characters at most).";
        if (dep.DepositDate > Today.AddDays(1)) return "The deposit date is in the future.";
        if (mode == PaymentMode.PostDatedCheck && checkDate is { } cd && dep.DepositDate < cd)
            return $"A post-dated check can't be deposited before its check date ({cd:yyyy-MM-dd}).";

        var batch = slip.Length > 0
            ? await db.DepositBatches.FirstOrDefaultAsync(b => b.SiteId == c.SiteId && b.Bank == bank && b.SlipRefNo == slip)
            : c.DepositBatch is { SlipRefNo: "" } own && own.BankAccountId == account.Id ? own : null;
        if (batch is null)
        {
            batch = new DepositBatch { SiteId = c.SiteId, BankAccountId = account.Id, Bank = bank, SlipRefNo = slip, DepositDate = dep.DepositDate, CreatedBy = user };
            db.DepositBatches.Add(batch);
        }
        else if (batch.MatchConfirmedAt is not null)
            return $"Slip {(slip.Length > 0 ? slip : "(no reference)")} is already matched to the bank statement; it can't take more collections.";
        else if (batch.DepositDate != dep.DepositDate)
        {
            // Correcting the date is fine while no other live collection shares the slip.
            if (await db.Collections.AnyAsync(x => x.DepositBatchId == batch.Id && x.Id != c.Id && x.VerificationStatus != VerificationStatus.Voided))
                return $"Slip {slip} at {bank} is already encoded with deposit date {batch.DepositDate:yyyy-MM-dd}.";
            batch.DepositDate = dep.DepositDate;
        }

        c.DepositBatch = batch;
        return null;
    }

    /// <summary>Saves, then re-totals every deposit slip this collection touches (sum of its live members).</summary>
    private async Task<(CollectionDto? dto, string? error)> SaveAndReturnAsync(CollectionUser u, Collection c, int? previousBatchId = null)
    {
        var oldBatchId = previousBatchId ?? db.Entry(c).Property(x => x.DepositBatchId).OriginalValue;
        SetCompleteness(c);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_Collections_SiteId_ReceiptType_ReceiptNo") == true)
        {
            return (null, $"{c.ReceiptType} {c.ReceiptNo} is already encoded for this branch.");
        }
        foreach (var id in new[] { oldBatchId, c.DepositBatchId }.Where(x => x is not null).Distinct())
        {
            var b = await db.DepositBatches.FindAsync(id);
            if (b is null) continue;
            b.TotalAmount = await db.Collections.Where(x => x.DepositBatchId == id && x.VerificationStatus != VerificationStatus.Voided).SumAsync(x => x.Amount);
            // A voided member or a corrected amount can break (or make) the bank match.
            await matcher.SyncBatchAsync(b, u.Name);
        }
        await db.SaveChangesAsync();
        return (await ToDtoAsync(u, (await LoadAsync(c.Id))!), null);
    }

    /// <summary>What a collection still lacks before it can be submitted; empty = complete (payment status Collected).</summary>
    public static List<string> Missing(Collection c)
    {
        if (c.RemittanceId is not null) return MissingInRa(c);
        var m = new List<string>();
        if (c.Attachments.Count == 0) m.Add("Attach the soft copy of the receipt.");
        if (c.ReceiptType == ReceiptType.CR && c.DepositBatch is null) m.Add("Enter the deposit (bank account and date; the reference no. is optional).");
        return m;
    }

    /// <summary>A complete collection not yet deposited: a CR is Collected; a PDC is Pending (check on hand, not collected).</summary>
    public static PaymentStatus Undeposited(Collection c) =>
        c.PaymentMode == PaymentMode.PostDatedCheck ? PaymentStatus.Pending : PaymentStatus.Collected;

    /// <summary>Draft until the requirements are complete, then Collected / Pending (Deposited is left to the bank match).</summary>
    private static void SetCompleteness(Collection c)
    {
        if (c.VerificationStatus == VerificationStatus.Voided || c.PaymentStatus == PaymentStatus.Deposited) return;
        c.PaymentStatus = Missing(c).Count > 0 ? PaymentStatus.Draft : Undeposited(c);
    }

    private static void History(Collection c, string action, VerificationStatus? from, VerificationStatus? to, string? reason, string by) =>
        c.History.Add(new CollectionHistory { Action = action, FromStatus = from?.ToString(), ToStatus = to?.ToString(), Reason = reason, By = by });

    // ── workflow ────────────────────────────────────────────────────────────

    public async Task<(CollectionDto? dto, string? error)> SubmitAsync(CollectionUser u, int id)
    {
        var c = await LoadAsync(id);
        if (c is null || !await CanReadSiteAsync(u, c.SiteId)) return (null, "Collection not found.");
        if (!await CanWriteSiteAsync(u, c.SiteId)) return (null, "You are not assigned to this branch.");
        if (c.RemittanceId is { } raId) return (null, InRa(raId));
        if (c.VerificationStatus is not (VerificationStatus.Draft or VerificationStatus.Returned))
            return (null, $"A collection that is {c.VerificationStatus} can't be submitted.");
        if (Missing(c) is { Count: > 0 } missing) return (null, "Still a draft — " + string.Join(" ", missing));

        var from = c.VerificationStatus;
        c.VerificationStatus = VerificationStatus.ForVerification;
        c.SubmittedAt = DateTime.UtcNow;
        c.ReturnReason = null;
        History(c, "Submitted", from, c.VerificationStatus, null, u.Name);
        return await SaveAndReturnAsync(u, c);
    }

    public async Task<(CollectionDto? dto, string? error)> VoidAsync(CollectionUser u, int id, string? reason)
    {
        var c = await LoadAsync(id);
        if (c is null || !await CanReadSiteAsync(u, c.SiteId)) return (null, "Collection not found.");
        if (c.VerificationStatus == VerificationStatus.Voided) return (null, "Already voided.");
        if (c.RemittanceId is { } raId) return (null, InRa(raId));
        var editable = c.VerificationStatus is VerificationStatus.Draft or VerificationStatus.Returned;
        if (!u.CanManage && !(editable && await CanWriteSiteAsync(u, c.SiteId)))
            return (null, "Only the Finance Manager can void a collection that was already submitted.");
        reason = reason?.Trim();
        if (string.IsNullOrEmpty(reason)) return (null, "Give the reason for voiding.");
        if (await db.Collections.Where(x => x.SettledByCollectionId == c.Id).Select(x => new { x.ReceiptType, x.ReceiptNo }).FirstOrDefaultAsync() is { } settles)
            return (null, $"This receipt settles bounced PDC {settles.ReceiptType} {settles.ReceiptNo} — remove that settlement on the PDC Monitor first.");
        // Already written into HOMS → it's cancelled there first; anything still queued is cancelled here.
        if (await homs.CancelOpenAsync(c.Id, HomsPostingKind.Or, HomsPostingKind.Rpdc, HomsPostingKind.PdcJv) is { } posted)
            return (null, posted);

        var from = c.VerificationStatus;
        c.VerificationStatus = VerificationStatus.Voided;
        c.VoidReason = reason[..Math.Min(reason.Length, 500)];
        History(c, "Voided", from, c.VerificationStatus, c.VoidReason, u.Name);
        // Voided rows keep their invoice lines for the audit trail; balances ignore them.
        return await SaveAndReturnAsync(u, c, c.DepositBatchId);
    }

    /// <summary>Finance Verifier, on a submitted collection someone else encoded (four-eyes).</summary>
    private static bool CanVerifyThis(CollectionUser u, Collection c) =>
        u.CanVerify && c.VerificationStatus == VerificationStatus.ForVerification && c.RemittanceId is null
        && !string.Equals(c.CreatedBy, u.Name, StringComparison.OrdinalIgnoreCase);

    private static string? VerifyGate(CollectionUser u, Collection c) =>
        !u.CanVerify ? "Only a Finance Verifier can verify or return collections."
        : c.RemittanceId is { } raId ? InRa(raId)
        : c.VerificationStatus != VerificationStatus.ForVerification ? $"This collection is {c.VerificationStatus}, not waiting for verification."
        : !CanVerifyThis(u, c) ? "You encoded this collection — another verifier has to check it."
        : null;

    /// <summary>Entry checked against the receipt soft copy and the bank statement.</summary>
    public async Task<(CollectionDto? dto, string? error)> VerifyAsync(CollectionUser u, int id)
    {
        var c = await LoadAsync(id);
        if (c is null) return (null, "Collection not found.");
        if (VerifyGate(u, c) is { } gate) return (null, gate);
        await VerifyCoreAsync(u, c);
        return await SaveAndReturnAsync(u, c);
    }

    /// <summary>Verified = posted: marks it, queues its HOMS document and approves its suggested bank match (not saved).</summary>
    private async Task VerifyCoreAsync(CollectionUser u, Collection c)
    {
        c.VerificationStatus = VerificationStatus.Verified;
        c.VerifiedAt = DateTime.UtcNow;
        c.VerifiedBy = u.Name;
        History(c, "Verified", VerificationStatus.ForVerification, c.VerificationStatus, null, u.Name);
        // Verified = posted: the OR (CR) / RPDC (PDC) is queued for the HOMS ARCHIVES files in this same save.
        var isPdc = c.PaymentMode == PaymentMode.PostDatedCheck;
        await homs.QueueAsync(c, isPdc ? HomsPostingKind.Rpdc : HomsPostingKind.Or, u.Name);
        if (isPdc)
        {
            // A matured PDC's match is the CNC Verifier's to confirm; if they already confirmed its slip (another
            // check on it was verified first), this one's JV follows its RPDC now.
            if (c.DepositBatch?.MatchConfirmedAt is not null && c.PdcOutcome is null)
                await homs.QueueAsync(c, HomsPostingKind.PdcJv, u.Name);
        }
        // The verifier checked the CR against the bank statement: a suggested match is approved with it.
        else if (c.DepositBatch is { MatchedBankLineId: { } lineId, MatchConfirmedAt: null } b && !await PdcSlipAsync(b.Id)
                 && await db.BankStatementLines.FindAsync(lineId) is { } line)
            await matcher.ConfirmAsync(b, line, u.Name);
    }

    /// <summary>Approves the suggested bank match of an already verified collection.</summary>
    public async Task<(CollectionDto? dto, string? error)> ConfirmMatchAsync(CollectionUser u, int id)
    {
        if (!u.CanVerify) return (null, "Only a Finance Verifier can approve a bank match.");
        var c = await LoadAsync(id);
        if (c is null) return (null, "Collection not found.");
        if (c.DepositBatch is not { MatchedBankLineId: { } lineId } b || await db.BankStatementLines.FindAsync(lineId) is not { } line)
            return (null, "This collection has no bank match to approve.");
        if (b.MatchConfirmedAt is not null) return (null, "The bank match is already approved.");
        if (await PdcSlipAsync(b.Id)) return (null, CncOnly);
        if (c.VerificationStatus != VerificationStatus.Verified) return (null, "Verify the collection — that approves its bank match too.");
        await matcher.ConfirmAsync(b, line, u.Name);
        return await SaveAndReturnAsync(u, c);
    }

    private const string CncOnly = "Matured PDCs are confirmed by the CNC Verifier on the PDC Monitor.";

    /// <summary>The suggested credit isn't this deposit: unmatch it and keep that line out of automatic matching.</summary>
    public async Task<(CollectionDto? dto, string? error)> RejectMatchAsync(CollectionUser u, int id, string? reason)
    {
        if (!u.CanVerify) return (null, "Only a Finance Verifier can reject a bank match.");
        var c = await LoadAsync(id);
        if (c is null) return (null, "Collection not found.");
        if (c.DepositBatch is not { MatchedBankLineId: { } lineId } b || await db.BankStatementLines.FindAsync(lineId) is not { } line)
            return (null, "This collection has no bank match to reject.");
        if (b.MatchConfirmedAt is not null) return (null, "The bank match is already approved — finance can unmatch it on the Bank Statement page.");
        if (await PdcSlipAsync(b.Id)) return (null, CncOnly);
        reason = reason?.Trim();
        await matcher.UnapplyAsync(b, line, u.Name, string.IsNullOrEmpty(reason) ? "Suggested match rejected by the verifier" : reason[..Math.Min(reason.Length, 400)], byFinance: true);
        return await SaveAndReturnAsync(u, c);
    }

    /// <summary>Back to the custodian with the reason; they fix it and submit again.</summary>
    public async Task<(CollectionDto? dto, string? error)> ReturnAsync(CollectionUser u, int id, string? reason)
    {
        var c = await LoadAsync(id);
        if (c is null) return (null, "Collection not found.");
        if (VerifyGate(u, c) is { } gate) return (null, gate);
        reason = reason?.Trim();
        if (string.IsNullOrEmpty(reason)) return (null, "Give the reason for returning it.");

        c.VerificationStatus = VerificationStatus.Returned;
        c.ReturnReason = reason[..Math.Min(reason.Length, 500)];
        History(c, "Returned", VerificationStatus.ForVerification, c.VerificationStatus, c.ReturnReason, u.Name);
        return await SaveAndReturnAsync(u, c);
    }

    // ── PDC module ──────────────────────────────────────────────────────────

    /// <summary>Verified + deposit recorded; an existing tag can always be cleared (so tags set before this rule can be undone)
    /// until a Letter of Dishonor is issued — from then on the Bounced tag is final.</summary>
    private static bool IsTaggable(Collection c) =>
        c.PaymentMode == PaymentMode.PostDatedCheck && c.VerificationStatus != VerificationStatus.Voided && c.LodIssuedDate is null
        && ((c.VerificationStatus == VerificationStatus.Verified && c.DepositBatch is not null) || c.PdcOutcome is not null);

    /// <summary>Every live PDC (not voided). Finance sees all branches; custodians their own.</summary>
    public async Task<List<PdcItemDto>> ListPdcAsync(CollectionUser u, int? siteId, int nearDays)
    {
        var q = db.Collections.Where(c => c.PaymentMode == PaymentMode.PostDatedCheck && c.VerificationStatus != VerificationStatus.Voided);
        var allSites = u.CanVerify || u.CanManage || u.CanTagPdc || u.CanConfirmPdc;
        if (!allSites)
            q = q.Where(c => db.UserBranches.Any(b => b.UserId == u.Id && b.SiteId == c.SiteId));
        if (siteId is not null) q = q.Where(c => c.SiteId == siteId);

        var today = Today;
        var rows = await q.Include(c => c.Site).Include(c => c.DepositBatch).OrderBy(c => c.CheckDate).Take(5000).ToListAsync();
        var ids = rows.Select(c => c.Id).ToList();
        var bms = (await db.BmsPostedCollections.Where(b => b.CollectionId != null && ids.Contains(b.CollectionId.Value) && !b.Removed)
            .Select(b => new { b.CollectionId, b.DocNo, b.Bounced }).ToListAsync())
            .ToDictionary(b => b.CollectionId!.Value);
        var lineIds = rows.Where(c => c.DepositBatch?.MatchedBankLineId != null).Select(c => c.DepositBatch!.MatchedBankLineId!.Value).Distinct().ToList();
        var lines = await db.BankStatementLines.Where(l => lineIds.Contains(l.Id))
            .Select(l => new { l.Id, Account = l.BankAccount.BankCode + " " + l.BankAccount.AccountNo, l.TxnDate, l.Credit, l.Reference, l.Description, l.MatchMethod })
            .ToDictionaryAsync(l => l.Id);
        var depositBy = await DepositRecordersAsync(ids);
        var settleIds = rows.Where(c => c.SettledByCollectionId != null).Select(c => c.SettledByCollectionId!.Value).ToList();
        var settles = await db.Collections.Where(x => settleIds.Contains(x.Id))
            .Select(x => new { x.Id, x.ReceiptType, x.ReceiptNo, x.ReceiptDate }).ToDictionaryAsync(x => x.Id);
        var mySites = u.CanEncode && !u.CanManage
            ? (await db.UserBranches.Where(b => b.UserId == u.Id).Select(b => b.SiteId).ToListAsync()).ToHashSet() : [];
        return rows.Select(c =>
        {
            var checkDate = c.CheckDate ?? c.ReceiptDate;
            var days = checkDate.DayNumber - today.DayNumber;
            var state = PdcState(c.PaymentMode, c.PaymentStatus, c.CheckDate, c.PdcOutcome, today)!;
            var batch = c.DepositBatch;
            var bankLine = batch?.MatchedBankLineId is { } lid && lines.TryGetValue(lid, out var l)
                ? new MatchedLineDto(l.Id, l.Account, l.TxnDate, l.Credit, l.Reference, l.Description, l.MatchMethod,
                    batch.MatchConfirmedAt != null, batch.MatchConfirmedBy)
                : null;
            var by = depositBy.GetValueOrDefault(c.Id) ?? batch?.CreatedBy;
            var canWrite = u.CanManage || mySites.Contains(c.SiteId);
            return new PdcItemDto(c.Id, c.SiteId, c.Site.Code, c.ReceiptType.ToString(), c.ReceiptNo, c.ReceiptDate,
                c.CustKey, c.CustName, c.Bank, c.CheckNo, checkDate, days, c.Amount,
                state, state == "Immature" && days <= nearDays, c.VerificationStatus.ToString(),
                batch?.Bank, batch?.SlipRefNo, batch?.DepositDate,
                c.PdcOutcome?.ToString(), c.PdcOutcomeDate, c.PdcOutcomeReason, c.PdcOutcomeBy,
                u.CanTagPdc && IsTaggable(c),
                bms.GetValueOrDefault(c.Id)?.DocNo, bms.GetValueOrDefault(c.Id)?.Bounced ?? false,
                bankLine, by,
                CanConfirmMatch: u.CanConfirmPdc && PdcConfirmGate(u, c, by) is null,
                CanRecordDeposit: (canWrite || u.CanVerify || u.CanConfirmPdc) && c.PdcOutcome is null
                                  && checkDate <= today && batch?.MatchedBankLineId is null,
                Dishonor: Dishonor(u, c, today, c.SettledByCollectionId is { } sid && settles.TryGetValue(sid, out var cr)
                    ? ($"{cr.ReceiptType} {cr.ReceiptNo}", cr.ReceiptDate) : null));
        }).ToList();
    }

    /// <summary>Who recorded each PDC's deposit slip (its latest "Deposit recorded" history entry).</summary>
    private async Task<Dictionary<int, string>> DepositRecordersAsync(List<int> ids) =>
        (await db.CollectionHistories.Where(h => ids.Contains(h.CollectionId) && h.Action == "Deposit recorded")
            .Select(h => new { h.CollectionId, h.Id, h.By }).ToListAsync())
        .GroupBy(h => h.CollectionId).ToDictionary(g => g.Key, g => g.MaxBy(h => h.Id)!.By);

    /// <summary>CNC Verifier confirming a matured PDC's bank match: verified, matched, not yet confirmed, and not a
    /// deposit they recorded themselves (four-eyes). Null = may confirm.</summary>
    private static string? PdcConfirmGate(CollectionUser u, Collection c, string? depositBy) =>
        !u.CanConfirmPdc ? "Only a CNC Verifier can confirm a matured PDC."
        : c.PaymentMode != PaymentMode.PostDatedCheck ? "Only a PDC is confirmed here."
        : c.VerificationStatus != VerificationStatus.Verified ? "The Finance Verifier hasn't verified this PDC's receipt yet."
        : c.PdcOutcome is not null ? $"This PDC is already tagged {c.PdcOutcome}."
        : c.DepositBatch is not { MatchedBankLineId: not null } ? "No bank credit is matched to this PDC's deposit yet."
        : c.DepositBatch.MatchConfirmedAt is not null ? "This PDC's bank match is already confirmed."
        : string.Equals(depositBy, u.Name, StringComparison.OrdinalIgnoreCase) ? "You recorded this deposit — another CNC Verifier has to confirm it."
        : null;

    /// <summary>CNC Verifier: the bank credit is this check — Deposited, and its JV is queued for HOMS (only trigger).</summary>
    public async Task<(CollectionDto? dto, string? error)> ConfirmPdcMatchAsync(CollectionUser u, int id)
    {
        var c = await LoadAsync(id);
        if (c is null) return (null, "PDC not found.");
        var by = (await DepositRecordersAsync([c.Id])).GetValueOrDefault(c.Id) ?? c.DepositBatch?.CreatedBy;
        if (PdcConfirmGate(u, c, by) is { } gate) return (null, gate);
        var line = await db.BankStatementLines.FindAsync(c.DepositBatch!.MatchedBankLineId!.Value);
        await matcher.ConfirmAsync(c.DepositBatch, line!, u.Name);
        return await SaveAndReturnAsync(u, c);
    }

    /// <summary>CNC Verifier: the matched credit isn't this check — unmatch it (keeps that line out of automatic matching).</summary>
    public async Task<(CollectionDto? dto, string? error)> RejectPdcMatchAsync(CollectionUser u, int id, string? reason)
    {
        if (!u.CanConfirmPdc) return (null, "Only a CNC Verifier can reject a matured PDC's bank match.");
        var c = await LoadAsync(id);
        if (c is null || c.PaymentMode != PaymentMode.PostDatedCheck) return (null, "PDC not found.");
        if (c.DepositBatch is not { MatchedBankLineId: { } lineId } b || await db.BankStatementLines.FindAsync(lineId) is not { } line)
            return (null, "No bank credit is matched to this PDC's deposit.");
        if (b.MatchConfirmedAt is not null) return (null, "The match is already confirmed — finance can unmatch it on the Bank Statement page.");
        reason = reason?.Trim();
        if (string.IsNullOrEmpty(reason)) return (null, "Give the reason for rejecting the match.");
        await matcher.UnapplyAsync(b, line, u.Name, reason[..Math.Min(reason.Length, 400)], byFinance: true);
        return await SaveAndReturnAsync(u, c);
    }

    /// <summary>After deposit, finance records whether the check cleared or bounced. A bounce reopens the invoices.</summary>
    public async Task<(CollectionDto? dto, string? error)> TagPdcAsync(CollectionUser u, int id, PdcTagDto dto)
    {
        if (!u.CanTagPdc) return (null, "You can't tag PDC outcomes.");
        var c = await LoadAsync(id);
        if (c is null) return (null, "Collection not found.");
        if (c.PaymentMode != PaymentMode.PostDatedCheck) return (null, "Only a post-dated check has an outcome.");
        if (c.VerificationStatus == VerificationStatus.Voided) return (null, "This collection is voided.");
        if (c.DepositBatch is null) return (null, "Record the PDC's deposit first — only a deposited check can clear or bounce.");
        if (c.LodIssuedDate is not null) return (null, "A Letter of Dishonor was issued for this check — its Bounced tag is final.");

        var reason = dto.Reason?.Trim() is { Length: > 0 } r ? r[..Math.Min(r.Length, 500)] : null;
        var old = c.PdcOutcome;
        if (string.Equals(dto.Outcome, "None", StringComparison.OrdinalIgnoreCase))
        {
            if (old is null) return (null, "This PDC has no outcome to clear.");
            if (reason is null) return (null, "Give the reason for clearing the tag.");
            c.PdcOutcome = null;
            c.PdcOutcomeDate = null;
            c.PdcOutcomeReason = null;
            c.PdcOutcomeBy = null;
            History(c, $"PDC tag cleared (was {old})", null, null, reason, u.Name);
            return await SaveAndReturnAsync(u, c);
        }

        if (c.VerificationStatus != VerificationStatus.Verified) return (null, "Verify this PDC first — only a verified PDC can be tagged Succeeded or Bounced.");
        if (!Enum.TryParse<PdcOutcome>(dto.Outcome, true, out var outcome)) return (null, "Choose Succeeded or Bounced.");
        if (dto.Date is not { } date) return (null, "Enter the date the bank cleared or returned the check.");
        if (date > Today) return (null, "The outcome date is in the future.");
        if (date < c.DepositBatch.DepositDate) return (null, $"The outcome date is before the deposit ({c.DepositBatch.DepositDate:yyyy-MM-dd}).");
        if (outcome == PdcOutcome.Bounced && reason is null) return (null, "Give the bounce reason (e.g. DAIF, account closed).");

        c.PdcOutcome = outcome;
        c.PdcOutcomeDate = date;
        c.PdcOutcomeReason = reason;
        c.PdcOutcomeBy = u.Name;
        History(c, old is null ? $"PDC {outcome}" : $"PDC {old} → {outcome}", null, null, reason, u.Name);
        return await SaveAndReturnAsync(u, c);
    }

    /// <summary>A matured PDC was deposited: record its slip so the bank-statement match can find it.
    /// The branch custodian, the Finance Verifier or the CNC Verifier (any branch) records it (user 2026-10-08).</summary>
    public async Task<(CollectionDto? dto, string? error)> RecordDepositAsync(CollectionUser u, int id, DepositInfoDto dep)
    {
        var c = await LoadAsync(id);
        if (c is null || !await CanReadSiteAsync(u, c.SiteId)) return (null, "Collection not found.");
        if (!u.CanVerify && !u.CanConfirmPdc && !await CanWriteSiteAsync(u, c.SiteId)) return (null, "You are not assigned to this branch.");
        if (c.PaymentMode != PaymentMode.PostDatedCheck) return (null, "Only a PDC's deposit is recorded separately; edit the CR instead.");
        if (c.VerificationStatus == VerificationStatus.Voided || c.PdcOutcome is not null) return (null, "This PDC is closed.");
        if (c.CheckDate > Today) return (null, $"The check is not yet due ({c.CheckDate:yyyy-MM-dd}).");
        if (dep is null || dep.BankAccountId is null) return (null, "Enter the deposit: bank account and date (the slip / reference no. is optional).");

        var error = await AssignDepositAsync(c, dep, c.PaymentMode, c.CheckDate, u.Name);
        if (error is not null) return (null, error);
        var slip = (dep.SlipRefNo ?? "").Trim();
        History(c, "Deposit recorded", null, null, $"{c.DepositBatch!.Bank}{(slip.Length > 0 ? $" slip {slip}" : "")} on {dep.DepositDate:yyyy-MM-dd}", u.Name);
        return await SaveAndReturnAsync(u, c);
    }

    // ── bounced PDC follow-up ───────────────────────────────────────────────
    // A bounced PDC stays on file as the customer's history. Finance issues a Letter of Dishonor; once the customer
    // has received it they have LodSettleDays to pay. Paid = one new verified CR covering the full amount (Settled);
    // not paid by the deadline = endorsed for legal action (user 2026-10-09).

    /// <summary>Default days to settle after the LOD is received (appsettings Collections:LodSettleDays).</summary>
    public int LodSettleDaysDefault => Math.Clamp(config.GetValue("Collections:LodSettleDays", 5), 1, 90);

    /// <summary>For LOD / LOD Issued / Awaiting Settlement / Overdue / For Legal Action / Settled; null unless bounced.</summary>
    public static string? DishonorState(Collection c, DateOnly today) =>
        c.PdcOutcome != PdcOutcome.Bounced || c.VerificationStatus == VerificationStatus.Voided ? null
        : c.SettledByCollectionId is not null ? DishonorStates.Settled
        : c.LegalEndorsedDate is not null ? DishonorStates.ForLegal
        : c.LodIssuedDate is null ? DishonorStates.ForLod
        : c.LodDueDate is not { } due ? DishonorStates.LodIssued
        : today > due ? DishonorStates.Overdue : DishonorStates.AwaitingSettlement;

    private static PdcDishonorDto? Dishonor(CollectionUser u, Collection c, DateOnly today, (string Receipt, DateOnly Date)? settledBy) =>
        DishonorState(c, today) is not { } state ? null
        : new PdcDishonorDto(state, u.CanTagPdc, c.LodIssuedDate, c.LodIssuedBy, c.LodSettleDays,
            c.LodReceivedDate, c.LodCopyAttachmentId, c.LodDueDate,
            c.SettledByCollectionId, settledBy?.Receipt, settledBy?.Date, c.SettledBy,
            c.LegalEndorsedDate, c.LegalEndorsedBy, c.LegalRemarks, c.LegalEndorsedTo);

    private async Task<(Collection? c, string? error)> LoadBouncedAsync(CollectionUser u, int id)
    {
        if (!u.CanTagPdc) return (null, "You can't follow up bounced PDCs.");
        var c = await LoadAsync(id);
        if (c is null || c.PaymentMode != PaymentMode.PostDatedCheck) return (null, "PDC not found.");
        if (c.VerificationStatus == VerificationStatus.Voided) return (null, "This collection is voided.");
        if (c.PdcOutcome != PdcOutcome.Bounced) return (null, "Only a bounced PDC is followed up.");
        return (c, null);
    }

    /// <summary>Issue (or, before the customer has received it, reissue) the Letter of Dishonor. Locks the Bounced tag.</summary>
    public async Task<(CollectionDto? dto, string? error)> IssueLodAsync(CollectionUser u, int id, IssueLodDto dto)
    {
        var (c, error) = await LoadBouncedAsync(u, id);
        if (c is null) return (null, error);
        if (c.SettledByCollectionId is not null) return (null, "This bounced PDC is already settled.");
        if (c.LegalEndorsedDate is not null) return (null, "This bounced PDC is already endorsed for legal action.");
        if (c.LodReceivedDate is not null) return (null, "The customer already received the LOD — it can't be reissued.");
        if (dto.Date is not { } date) return (null, "Enter the LOD date.");
        if (date > Today) return (null, "The LOD date is in the future.");
        if (date < c.PdcOutcomeDate) return (null, $"The LOD date is before the check bounced ({c.PdcOutcomeDate:yyyy-MM-dd}).");
        if (dto.SettleDays is not { } days || days is < 1 or > 90) return (null, "Days to settle must be 1 to 90.");

        var again = c.LodIssuedDate is not null;
        c.LodIssuedDate = date;
        c.LodIssuedBy = u.Name;
        c.LodSettleDays = days;
        History(c, again ? "LOD reissued" : "LOD issued", null, null, $"{date:yyyy-MM-dd}; {days} days to settle once received", u.Name);
        return await SaveAndReturnAsync(u, c);
    }

    /// <summary>What the printed letter shows: company / branch letterhead, customer, the check and the deadline.</summary>
    public async Task<(LodLetterDto? dto, string? error)> LodLetterAsync(CollectionUser u, int id)
    {
        var (c, error) = await LoadBouncedAsync(u, id);
        if (c is null) return (null, error);
        if (c.LodIssuedDate is not { } issued) return (null, "Issue the LOD first.");
        var company = await db.Companies.FindAsync(c.Site.CompanyId);
        // CustKey isn't unique across branch copies of cust4win; any copy with an address will do for the letter.
        var cust = await db.Customers.Where(x => x.CustKey == c.CustKey)
            .OrderByDescending(x => x.AddrLn1 != "").ThenByDescending(x => x.Id)
            .Select(x => new { x.CusName, x.AddrLn1, x.AddrLn2 }).FirstOrDefaultAsync();
        return (new LodLetterDto(
            company?.Name ?? "", company?.Address ?? "", company?.Phone ?? "", c.Site.Name, c.Site.Address,
            c.CustKey, cust?.CusName is { Length: > 0 } n ? n.Trim() : c.CustName, cust?.AddrLn1?.Trim(), cust?.AddrLn2?.Trim(),
            c.ReceiptType.ToString(), c.ReceiptNo, c.Bank, c.CheckNo, c.CheckDate ?? c.ReceiptDate, c.Amount,
            c.DepositBatch?.DepositDate, c.PdcOutcomeDate!.Value, c.PdcOutcomeReason,
            issued, c.LodIssuedBy ?? u.Name, c.LodSettleDays ?? LodSettleDaysDefault), null);
    }

    /// <summary>The customer received the LOD: the date starts the settlement clock; their signed copy is proof of notice.</summary>
    public async Task<(CollectionDto? dto, string? error)> RecordLodReceivedAsync(CollectionUser u, int id, DateOnly? date,
        string? fileName, Stream? content, long length)
    {
        var (c, error) = await LoadBouncedAsync(u, id);
        if (c is null) return (null, error);
        if (c.LodIssuedDate is null) return (null, "Issue the LOD first.");
        if (c.LodReceivedDate is not null) return (null, "The customer's receipt of the LOD is already recorded.");
        if (date is not { } d) return (null, "Enter the date the customer received the LOD.");
        if (d > Today) return (null, "The received date is in the future.");
        if (d < c.LodIssuedDate) return (null, $"The received date is before the LOD date ({c.LodIssuedDate:yyyy-MM-dd}).");
        if (content is null || string.IsNullOrEmpty(fileName)) return (null, "Attach the customer's received copy of the LOD.");

        var (copy, fileError) = await StoreAttachmentAsync(c, fileName, content, length, u.Name);
        if (copy is null) return (null, fileError);
        await db.SaveChangesAsync();   // the copy's Id

        c.LodCopyAttachmentId = copy.Id;
        c.LodReceivedDate = d;
        c.LodReceivedBy = u.Name;
        c.LodDueDate = d.AddDays(c.LodSettleDays ?? LodSettleDaysDefault);
        History(c, "LOD received by customer", null, null, $"{d:yyyy-MM-dd}; settle by {c.LodDueDate:yyyy-MM-dd} ({copy.FileName})", u.Name);
        return await SaveAndReturnAsync(u, c);
    }

    /// <summary>The customer's received LOD copy, for anyone who can follow up (CNC Verifiers have no collections access).</summary>
    public async Task<(Stream stream, string contentType, string fileName)?> OpenLodCopyAsync(CollectionUser u, int id)
    {
        if (!u.CanTagPdc) return null;
        var attId = await db.Collections.Where(c => c.Id == id).Select(c => c.LodCopyAttachmentId).FirstOrDefaultAsync();
        var a = attId is null ? null : await db.CollectionAttachments.FirstOrDefaultAsync(x => x.Id == attId && x.CollectionId == id);
        if (a is null) return null;
        var stream = await store.OpenAsync(a.BlobPath);
        return stream is null ? null : (stream, a.ContentType, a.FileName);
    }

    /// <summary>A replacement CR: same customer, verified, not a PDC, dated on/after the bounce, covering the full
    /// amount, and not already settling another bounced PDC.</summary>
    private IQueryable<Collection> SettlementCandidates(Collection bounced)
    {
        var since = bounced.PdcOutcomeDate!.Value;
        return db.Collections.Where(x => x.Id != bounced.Id && x.CustKey == bounced.CustKey && x.ReceiptType == ReceiptType.CR
            && x.PaymentMode != PaymentMode.PostDatedCheck && x.VerificationStatus == VerificationStatus.Verified
            && x.Amount >= bounced.Amount && x.ReceiptDate >= since
            && !db.Collections.Any(o => o.SettledByCollectionId == x.Id));
    }

    public async Task<(List<SettlementCandidateDto>? data, string? error)> SettlementCandidatesAsync(CollectionUser u, int id)
    {
        var (c, error) = await LoadBouncedAsync(u, id);
        if (c is null) return (null, error);
        return (await SettlementCandidates(c).OrderByDescending(x => x.ReceiptDate)
            .Select(x => new SettlementCandidateDto(x.Id, x.Site.Code, x.ReceiptType.ToString(), x.ReceiptNo, x.ReceiptDate,
                x.PaymentMode.ToString(), x.Amount))
            .ToListAsync(), null);
    }

    /// <summary>The customer paid: link the one new CR that covers the bounced amount in full. Closes the follow-up.</summary>
    public async Task<(CollectionDto? dto, string? error)> SettleBouncedAsync(CollectionUser u, int id, SettleBouncedDto dto)
    {
        var (c, error) = await LoadBouncedAsync(u, id);
        if (c is null) return (null, error);
        if (c.SettledByCollectionId is not null) return (null, "This bounced PDC is already settled.");
        var cr = await SettlementCandidates(c).FirstOrDefaultAsync(x => x.Id == dto.CollectionId);
        if (cr is null)
            return (null, "That receipt can't settle this check. It must be a verified CR of the same customer (not a PDC), dated on or after the bounce, covering the full amount, and not already used for another bounced PDC.");

        c.SettledByCollectionId = cr.Id;
        c.SettledAt = DateTime.UtcNow;
        c.SettledBy = u.Name;
        History(c, "Settled", null, null, $"CR {cr.ReceiptNo} of {cr.ReceiptDate:yyyy-MM-dd}, {cr.Amount:N2}", u.Name);
        db.CollectionHistories.Add(new CollectionHistory
        {
            CollectionId = cr.Id, Action = "Settles bounced PDC", By = u.Name,
            Reason = $"{c.ReceiptType} {c.ReceiptNo} · {c.Bank} #{c.CheckNo}, {c.Amount:N2}",
        });
        return await SaveAndReturnAsync(u, c);
    }

    /// <summary>Wrong CR linked: unlink it (reason required); the follow-up resumes where it was.</summary>
    public async Task<(CollectionDto? dto, string? error)> UnsettleBouncedAsync(CollectionUser u, int id, string? reason)
    {
        var (c, error) = await LoadBouncedAsync(u, id);
        if (c is null) return (null, error);
        if (c.SettledByCollectionId is not { } crId) return (null, "This bounced PDC isn't settled.");
        reason = reason?.Trim();
        if (string.IsNullOrEmpty(reason)) return (null, "Give the reason for removing the settlement.");
        reason = reason[..Math.Min(reason.Length, 500)];

        c.SettledByCollectionId = null;
        c.SettledAt = null;
        c.SettledBy = null;
        History(c, "Settlement removed", null, null, reason, u.Name);
        db.CollectionHistories.Add(new CollectionHistory
        {
            CollectionId = crId, Action = "No longer settles bounced PDC", By = u.Name, Reason = $"{c.ReceiptType} {c.ReceiptNo}: {reason}",
        });
        return await SaveAndReturnAsync(u, c);
    }

    public static readonly string[] LegalTargets = ["In-house legal", "External counsel"];

    /// <summary>A PDC's audit trail for the PDC Monitor drawer (CNC Verifiers have no collections access).</summary>
    public async Task<(List<CollectionHistoryDto>? data, string? error)> PdcHistoryAsync(CollectionUser u, int id)
    {
        var siteId = await db.Collections.Where(x => x.Id == id && x.PaymentMode == PaymentMode.PostDatedCheck)
            .Select(x => (int?)x.SiteId).FirstOrDefaultAsync();
        var allSites = u.CanVerify || u.CanManage || u.CanTagPdc || u.CanConfirmPdc;
        if (siteId is null || (!allSites && !await db.UserBranches.AnyAsync(b => b.UserId == u.Id && b.SiteId == siteId)))
            return (null, "PDC not found.");
        var rows = await db.CollectionHistories.Where(h => h.CollectionId == id).OrderBy(h => h.Id).ToListAsync();
        return (rows.Select(h => new CollectionHistoryDto(h.Action, h.FromStatus, h.ToStatus, h.Reason, Utc(h.At), h.By)).ToList(), null);
    }

    /// <summary>Not settled by the LOD's deadline: endorse for legal action. It can still be settled afterwards.</summary>
    public async Task<(CollectionDto? dto, string? error)> EndorseLegalAsync(CollectionUser u, int id, EndorseLegalDto dto)
    {
        var (c, error) = await LoadBouncedAsync(u, id);
        if (c is null) return (null, error);
        if (c.SettledByCollectionId is not null) return (null, "This bounced PDC is already settled.");
        if (c.LegalEndorsedDate is not null) return (null, "Already endorsed for legal action.");
        if (c.LodDueDate is not { } due) return (null, "Record the customer's receipt of the LOD first — the deadline runs from it.");
        if (Today <= due) return (null, $"The customer has until {due:yyyy-MM-dd} to settle.");
        if (dto.Date is not { } date) return (null, "Enter the endorsement date.");
        if (date > Today) return (null, "The endorsement date is in the future.");
        if (date <= due) return (null, $"The endorsement date must be after the settlement deadline ({due:yyyy-MM-dd}).");

        var to = string.IsNullOrWhiteSpace(dto.EndorsedTo) ? LegalTargets[0]
            : LegalTargets.FirstOrDefault(t => t.Equals(dto.EndorsedTo.Trim(), StringComparison.OrdinalIgnoreCase));
        if (to is null) return (null, $"Endorse to {string.Join(" or ", LegalTargets)}.");

        c.LegalEndorsedDate = date;
        c.LegalEndorsedBy = u.Name;
        c.LegalEndorsedTo = to;
        c.LegalRemarks = dto.Remarks?.Trim() is { Length: > 0 } r ? r[..Math.Min(r.Length, 500)] : null;
        History(c, "Endorsed for legal action", null, null, c.LegalRemarks is null ? to : $"{to} · {c.LegalRemarks}", u.Name);
        return await SaveAndReturnAsync(u, c);
    }

    // ── attachments ─────────────────────────────────────────────────────────

    private static readonly Dictionary<string, (string Type, byte[] Magic)> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ("application/pdf", "%PDF"u8.ToArray()),
        [".png"] = ("image/png", [0x89, 0x50, 0x4E, 0x47]),
        [".jpg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]),
        [".jpeg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]),
    };

    public async Task<(CollectionDto? dto, string? error)> AddAttachmentAsync(CollectionUser u, int id, string fileName, Stream content, long length)
    {
        var c = await LoadAsync(id);
        if (c is null || !await CanReadSiteAsync(u, c.SiteId)) return (null, "Collection not found.");
        if (!await CanWriteSiteAsync(u, c.SiteId)) return (null, "You are not assigned to this branch.");
        if (c.VerificationStatus is not (VerificationStatus.Draft or VerificationStatus.Returned))
            return (null, "Attachments can only be changed before the collection is submitted.");

        var (a, error) = await StoreAttachmentAsync(c, fileName, content, length, u.Name);
        if (a is null) return (null, error);
        History(c, "Attachment added", null, null, a.FileName, u.Name);
        return await SaveAndReturnAsync(u, c);
    }

    /// <summary>Checks the file (size, type, real signature), stores it and adds it to the collection (not saved yet).</summary>
    private async Task<(CollectionAttachment? a, string? error)> StoreAttachmentAsync(Collection c, string fileName, Stream content, long length, string user,
        bool raExcel = false)
    {
        if (c.Attachments.Count >= MaxAttachments) return (null, $"At most {MaxAttachments} files per collection.");
        if (length <= 0 || length > MaxAttachmentBytes) return (null, "Files must be 10 MB or smaller.");

        var ext = Path.GetExtension(fileName);
        // The RA Excel is kept with its CR (only through the RA upload); anything else is a receipt soft copy.
        if (!(raExcel ? RaExcelTypes : Allowed).TryGetValue(ext, out var kind))
            return (null, raExcel ? "Upload the RA as an Excel file (.xls or .xlsx)." : "Only PDF, JPG and PNG files are allowed.");

        // Check the real file signature, not just the name.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer);
        var bytes = buffer.ToArray();
        if (bytes.Length < kind.Magic.Length || !bytes.AsSpan(0, kind.Magic.Length).SequenceEqual(kind.Magic))
            return (null, $"{Path.GetFileName(fileName)} is not a valid {ext.TrimStart('.').ToUpperInvariant()} file.");

        var path = $"collections/{c.CollectionKey:N}/{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        buffer.Position = 0;
        await store.SaveAsync(path, buffer, kind.Type);

        var name = Path.GetFileName(fileName);
        var a = new CollectionAttachment
        {
            BlobPath = path, FileName = name.Length > 200 ? name[..200] : name, ContentType = kind.Type, Size = bytes.Length, UploadedBy = user,
        };
        c.Attachments.Add(a);
        return (a, null);
    }

    public async Task<(CollectionDto? dto, string? error)> DeleteAttachmentAsync(CollectionUser u, int id, int attachmentId)
    {
        var c = await LoadAsync(id);
        if (c is null || !await CanReadSiteAsync(u, c.SiteId)) return (null, "Collection not found.");
        if (!await CanWriteSiteAsync(u, c.SiteId)) return (null, "You are not assigned to this branch.");
        if (c.VerificationStatus is not (VerificationStatus.Draft or VerificationStatus.Returned))
            return (null, "Attachments can only be changed before the collection is submitted.");
        var a = c.Attachments.FirstOrDefault(x => x.Id == attachmentId);
        if (a is null) return (null, "Attachment not found.");
        if (c.RemittanceId is not null && IsRaExcel(a))
            return (null, "This is the CR's RA Excel — upload a corrected file on the Remittance Advice instead.");

        db.CollectionAttachments.Remove(a);
        History(c, "Attachment removed", null, null, a.FileName, u.Name);
        var result = await SaveAndReturnAsync(u, c);
        await store.DeleteAsync(a.BlobPath);
        return result;
    }

    public async Task<(Stream stream, string contentType, string fileName)?> OpenAttachmentAsync(CollectionUser u, int id, int attachmentId)
    {
        var a = await db.CollectionAttachments.Include(x => x.Collection).FirstOrDefaultAsync(x => x.Id == attachmentId && x.CollectionId == id);
        if (a is null || !await CanReadSiteAsync(u, a.Collection.SiteId)) return null;
        var stream = await store.OpenAsync(a.BlobPath);
        return stream is null ? null : (stream, a.ContentType, a.FileName);
    }
}
