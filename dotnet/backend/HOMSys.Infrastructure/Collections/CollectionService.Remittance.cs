using System.Text.Json;
using System.Text.Json.Serialization;
using HOMSys.Application.DTOs.Collections;
using HOMSys.Application.Services;
using HOMSys.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Collections;

/// <summary>
/// Remittance Advice (NKA, user 2026-10-15): the Custodian Verifier encodes one RA per customer payment, adds a CR
/// per RA Excel (OR no. + DCR no. pre-filled from it, editable), and each Excel's invoice blocks are applied to that CR.
/// Every line is counter-checked against the system invoice; a mismatch blocks submit until the Excel is corrected and
/// uploaded again (no accepting differences). The Finance Verifier verifies / returns the RA as a whole; its CRs follow it.
/// Applied the way HOMS records NKA ORs (checked on SVI OR 21312 / 21764): only the NET goes against each invoice —
/// the chain's deductions (CEWT, RFC/BO/RTV, rebates, portal fees, unidentified…) stay open on A/R in HOMS and BMS, so
/// they're kept on the RA lines for the checks and the verifier but never become CollectionDeductions.
/// </summary>
public partial class CollectionService
{
    private const string HeadOfficeCode = "HO";

    private static readonly JsonSerializerOptions DedJson = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static readonly Dictionary<string, (string Type, byte[] Magic)> RaExcelTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".xls"] = ("application/vnd.ms-excel", [0xD0, 0xCF, 0x11, 0xE0]),
        [".xlsx"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [0x50, 0x4B, 0x03, 0x04]),
    };

    private sealed record RaDed(DeductionType Type, decimal Amount, string? Remark);

    private static string InRa(int raId) => $"This CR is part of Remittance Advice #{raId} — submit, verify, return or void it there.";

    private static bool InRemittance(Collection c) => c.RemittanceId is not null || c.Remittance is not null;

    private static bool IsRaExcel(CollectionAttachment a) => RaExcelTypes.ContainsKey(Path.GetExtension(a.FileName));

    private static bool Editable(Remittance r) => r.VerificationStatus is VerificationStatus.Draft or VerificationStatus.Returned;

    private static bool Live(Collection c) => c.VerificationStatus != VerificationStatus.Voided;

    private static string[] Split(string? flags) => string.IsNullOrEmpty(flags) ? [] : flags.Split(',');

    /// <summary>No flags: the line matches its invoice, so it's applied to the CR.</summary>
    private static bool IsClear(string flags) => string.IsNullOrEmpty(flags);

    private static bool IsClear(RemittanceLine l) => IsClear(l.Flags);

    private static List<RaDed> Deds(RemittanceLine l) => JsonSerializer.Deserialize<List<RaDed>>(l.DeductionsJson, DedJson) ?? [];

    /// <summary>What a CR of an RA still lacks before the RA can be submitted.</summary>
    private static List<string> MissingInRa(Collection c)
    {
        var m = new List<string>();
        if (!c.Attachments.Any(IsRaExcel)) m.Add("Upload the RA Excel.");
        if (c.DcrNo is null) m.Add("Enter the DCR no.");
        var open = c.RemittanceLines.Count(l => !IsClear(l));
        if (open > 0) m.Add($"{open} line(s) don't match their invoice — correct the RA Excel and upload it again.");
        else if (c.UnappliedAmount != 0) m.Add($"{c.UnappliedAmount:N2} of the CR amount isn't applied to any invoice — check the RA Excel totals.");
        if (c.DepositBatch is null && c.DepositBatchId is null) m.Add("Enter the deposit on the Remittance Advice.");
        return m;
    }

    private static List<string> MissingRa(Remittance ra)
    {
        var m = new List<string>();
        var crs = ra.Collections.Where(Live).ToList();
        if (string.IsNullOrWhiteSpace(ra.ChainName)) m.Add("Enter the chain name.");
        if (ra.DepositBatch is null) m.Add("Enter the deposit (bank account and date; the reference no. is optional).");
        if (crs.Count == 0) m.Add("Add a CR: type its OR no. and DCR no., then upload its RA Excel.");
        else if (crs.Sum(c => c.Amount) is var total && total != ra.CheckAmount)
            m.Add($"The CRs total {total:N2} but the {(ra.PaymentMode == PaymentMode.Check ? "check" : "payment")} is {ra.CheckAmount:N2}.");
        foreach (var c in crs)
            m.AddRange(MissingInRa(c).Where(x => !x.Contains("Enter the deposit")).Select(x => $"CR {c.ReceiptNo}: {x}"));
        return m;
    }

    private static void RaHistory(Remittance ra, string action, VerificationStatus? from, VerificationStatus? to, string? reason, string by) =>
        ra.History.Add(new RemittanceHistory { Action = action, FromStatus = from?.ToString(), ToStatus = to?.ToString(), Reason = reason, By = by });

    private Task<Remittance?> LoadRaAsync(int id) => db.Remittances
        .Include(r => r.Site).Include(r => r.DepositBatch).ThenInclude(b => b!.BankAccount).Include(r => r.History)
        .Include(r => r.Collections).ThenInclude(c => c.Site)
        .Include(r => r.Collections).ThenInclude(c => c.DepositBatch)
        .Include(r => r.Collections).ThenInclude(c => c.RemittanceLines).ThenInclude(l => l.PostedInvoice)
        .Include(r => r.Collections).ThenInclude(c => c.Attachments)
        .Include(r => r.Collections).ThenInclude(c => c.History)
        .Include(r => r.Collections).ThenInclude(c => c.Invoices).ThenInclude(i => i.PostedInvoice)
        .Include(r => r.Collections).ThenInclude(c => c.Deductions).ThenInclude(d => d.PostedInvoice)
        .AsSplitQuery().FirstOrDefaultAsync(r => r.Id == id);

    private static bool CanReadRa(CollectionUser u) => u.CanEncodeRa || u.CanVerify || u.CanManage;

    // ── list / get ──────────────────────────────────────────────────────────

    public async Task<List<RemittanceListItemDto>> ListRemittancesAsync(CollectionUser u, string? status)
    {
        if (!CanReadRa(u)) return [];
        var q = db.Remittances.AsQueryable();
        if (Enum.TryParse<VerificationStatus>(status, true, out var vs)) q = q.Where(r => r.VerificationStatus == vs);
        var rows = await q.OrderByDescending(r => r.Id).Take(500).Select(r => new
        {
            r.Id, r.ChainName, r.PaymentMode, r.CheckNo, r.CheckDate, r.CheckAmount, r.VerificationStatus, r.CreatedBy, r.CreatedAt, r.SubmittedAt,
            Slip = r.DepositBatch != null ? r.DepositBatch.SlipRefNo : null,
            BankMatch = r.DepositBatch == null || r.DepositBatch.MatchedBankLineId == null ? null
                : r.DepositBatch.MatchConfirmedAt != null ? "Approved" : "Found",
            Crs = r.Collections.Where(c => c.VerificationStatus != VerificationStatus.Voided)
                .Select(c => new { c.ReceiptNo, c.Amount, Postings = c.HomsPostings.Select(p => p.Status).ToList() }).ToList(),
            Lines = r.Collections.Where(c => c.VerificationStatus != VerificationStatus.Voided)
                .SelectMany(c => c.RemittanceLines).Select(l => l.Flags).ToList(),
        }).AsSplitQuery().ToListAsync();

        return rows.Select(r =>
        {
            var postings = r.Crs.SelectMany(c => c.Postings).ToList();
            var homsState = postings.Contains(HomsPostingStatus.Failed) ? "Failed"
                : postings.Contains(HomsPostingStatus.Held) ? "Held"
                : postings.Any(p => p is HomsPostingStatus.Pending or HomsPostingStatus.Processing) ? "Queued"
                : postings.Contains(HomsPostingStatus.Done) ? "Posted" : null;
            return new RemittanceListItemDto(r.Id, r.ChainName, r.PaymentMode.ToString(), r.CheckNo, r.CheckDate, r.CheckAmount,
                r.Crs.Count, r.Crs.Sum(c => c.Amount), string.Join(", ", r.Crs.Select(c => c.ReceiptNo)),
                r.Lines.Count(l => !IsClear(l)),
                r.VerificationStatus.ToString(), r.Slip, r.BankMatch, homsState, r.CreatedBy, Utc(r.CreatedAt),
                r.SubmittedAt is { } s ? Utc(s) : null);
        }).ToList();
    }

    public async Task<RemittanceDto?> GetRemittanceAsync(CollectionUser u, int id)
    {
        if (!CanReadRa(u)) return null;
        var ra = await LoadRaAsync(id);
        return ra is null ? null : await RaToDtoAsync(u, ra);
    }

    private async Task<RemittanceDto> RaToDtoAsync(CollectionUser u, Remittance ra)
    {
        var canWrite = u.CanEncodeRa && Editable(ra);
        var crs = new List<RemittanceCrDto>();
        foreach (var c in ra.Collections.Where(c => Live(c) || ra.VerificationStatus == VerificationStatus.Voided).OrderBy(c => c.Id))
        {
            var applied = c.Invoices.Select(i => i.PostedInvoiceId).ToHashSet();
            var invIds = c.RemittanceLines.Where(l => l.PostedInvoiceId is not null).Select(l => l.PostedInvoiceId!.Value).Distinct().ToList();
            var paidElsewhere = await PaidAsync(invIds, c.Id);
            var elsewhere = await OtherReceiptsAsync(invIds, c.Id);
            var docNos = c.RemittanceLines.Select(l => l.DocNo).Distinct().ToList();
            var rfcs = (await db.PostedRfcs.Where(x => docNos.Contains(x.InvoiceNo)).ToListAsync())
                .GroupBy(x => (x.WhseNo, x.InvoiceNo))
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.RfcDate).ThenBy(x => x.RfcNo)
                    .Select(x => new RemittanceRfcDto(x.RfcNo, x.RfcDate, -x.Amt)).ToList());
            var lines = c.RemittanceLines.OrderBy(l => l.RowNo).Select(l => new RemittanceLineDto(
                l.Id, l.RowNo, l.DocNo, l.WhseNo, BranchWarehouseMap.SiteCode(l.WhseNo), l.CustKey, l.DocDate, l.BirSeries,
                l.Gross, l.Cewt, l.RfcBoRtv, l.Other, l.Net,
                Deds(l).Select(d => new RemittanceDeductionDto(d.Type.ToString(), d.Amount, d.Remark)).ToList(),
                l.PostedInvoiceId, l.SystemTotal, l.SystemRfc, l.PostedInvoice?.CustKey.Trim(), l.PostedInvoice?.CustName, l.PostedInvoice?.DocDate,
                Split(l.Flags).ToList(),
                l.PostedInvoiceId is { } pid && applied.Contains(pid) && IsClear(l),
                l.PostedInvoice is { } inv ? Balance(inv, paidElsewhere.GetValueOrDefault(inv.Id)) : null,
                l.PostedInvoiceId is { } id && elsewhere.TryGetValue(id, out var who) ? who : null,
                rfcs.GetValueOrDefault((l.WhseNo, l.DocNo)))).ToList();
            var att = c.Attachments.OrderBy(a => a.Id).Select(a => new CollectionAttachmentDto(a.Id, a.FileName, a.ContentType, a.Size,
                Utc(a.UploadedAt), a.UploadedBy)).ToList();
            var excel = c.Attachments.Where(IsRaExcel).MaxBy(a => a.Id);
            crs.Add(new RemittanceCrDto(c.Id, c.ReceiptNo, c.DcrNo, c.ReceiptDate, c.HomsDeptCode,
                c.HomsDeptCode switch { 32 => "Luzon", 31 => "VisMin", _ => null }, c.Amount,
                c.Invoices.Sum(i => i.AmountApplied), c.RemittanceLines.Sum(l => l.Cewt + l.RfcBoRtv + l.Other), c.UnappliedAmount,
                c.VerificationStatus.ToString(), c.PaymentStatus.ToString(),
                excel is null ? null : att.First(a => a.Id == excel.Id), att.Where(a => a.Id != excel?.Id).ToList(), lines,
                Editable(ra) ? MissingInRa(c) : [], await homs.ForCollectionAsync(c.Id)));
        }

        var batch = ra.DepositBatch;
        var bankLine = batch?.MatchedBankLineId is { } lineId
            ? await db.BankStatementLines.Where(l => l.Id == lineId)
                .Select(l => new MatchedLineDto(l.Id, l.BankAccount.BankCode + " " + l.BankAccount.AccountNo, l.TxnDate, l.Credit,
                    l.Reference, l.Description, l.MatchMethod, batch.MatchConfirmedAt != null, batch.MatchConfirmedBy))
                .FirstOrDefaultAsync()
            : null;
        var encodedByMe = string.Equals(ra.CreatedBy, u.Name, StringComparison.OrdinalIgnoreCase)
                          || ra.Collections.Any(c => string.Equals(c.CreatedBy, u.Name, StringComparison.OrdinalIgnoreCase));
        return new RemittanceDto(ra.Id, ra.SiteId, ra.Site.Code, ra.ChainName, ra.PaymentMode.ToString(), ra.Bank, ra.CheckNo, ra.CheckDate,
            ra.CheckAmount, batch is null ? null : new DepositInfoDto(batch.BankAccountId, batch.Bank, batch.SlipRefNo, batch.DepositDate),
            bankLine, ra.VerificationStatus.ToString(), ra.ReturnReason, ra.VoidReason, ra.Remarks,
            ra.CreatedBy, Utc(ra.CreatedAt), ra.SubmittedAt is { } s ? Utc(s) : null, ra.VerifiedAt is { } v ? Utc(v) : null, ra.VerifiedBy,
            ra.Collections.Where(Live).Sum(c => c.Amount), crs,
            ra.History.OrderBy(h => h.Id).Select(h => new CollectionHistoryDto(h.Action, h.FromStatus, h.ToStatus, h.Reason, Utc(h.At), h.By)).ToList(),
            Editable(ra) ? MissingRa(ra) : [],
            CanEdit: canWrite, CanSubmit: canWrite,
            CanVerify: u.CanVerify && ra.VerificationStatus == VerificationStatus.ForVerification && !encodedByMe,
            CanVoid: ra.VerificationStatus != VerificationStatus.Voided && (u.CanManage || canWrite));
    }

    /// <summary>The site RAs belong to (its deposit accounts are the RA's picklist).</summary>
    public async Task<CollectionSiteDto?> HeadOfficeAsync(CollectionUser u) =>
        !CanReadRa(u) ? null : await db.Sites.Where(s => s.Code == HeadOfficeCode && s.IsActive)
            .Select(s => new CollectionSiteDto(s.Id, s.Code, s.Name)).FirstOrDefaultAsync();

    /// <summary>Per invoice: the other live receipts on it (what makes its open balance smaller than its total).</summary>
    private async Task<Dictionary<int, string>> OtherReceiptsAsync(List<int> invoiceIds, int collectionId)
    {
        if (invoiceIds.Count == 0) return [];
        var rows = await db.CollectionInvoices
            .Where(x => invoiceIds.Contains(x.PostedInvoiceId) && x.CollectionId != collectionId
                        && x.Collection.VerificationStatus != VerificationStatus.Voided && x.Collection.PdcOutcome != PdcOutcome.Bounced)
            .Select(x => new
            {
                x.PostedInvoiceId, x.Collection.ReceiptType, x.Collection.ReceiptNo, Site = x.Collection.Site.Code,
                x.Collection.VerificationStatus, x.Collection.RemittanceId,
                Amount = x.AmountApplied + (db.CollectionDeductions.Where(d => d.CollectionId == x.CollectionId && d.PostedInvoiceId == x.PostedInvoiceId)
                    .Sum(d => (decimal?)d.Amount) ?? 0),
            }).ToListAsync();
        return rows.GroupBy(r => r.PostedInvoiceId).ToDictionary(g => g.Key, g => string.Join("; ", g.Select(r =>
            $"{r.ReceiptType} {r.ReceiptNo} {r.Site}{(r.RemittanceId is { } raId ? $" RA #{raId}" : "")} "
            + $"({StatusWords().Replace(r.VerificationStatus.ToString(), "$1 $2")}) {r.Amount:N2}")));
    }

    [System.Text.RegularExpressions.GeneratedRegex("([a-z])([A-Z])")]
    private static partial System.Text.RegularExpressions.Regex StatusWords();

    // ── header ──────────────────────────────────────────────────────────────

    public async Task<(RemittanceDto? dto, string? error)> CreateRemittanceAsync(CollectionUser u, SaveRemittanceDto dto)
    {
        if (!u.CanEncodeRa) return (null, "Only a Custodian Verifier can encode a Remittance Advice.");
        var ho = await db.Sites.FirstOrDefaultAsync(s => s.Code == HeadOfficeCode && s.IsActive);
        if (ho is null) return (null, "There's no active Head Office (HO) site for Remittance Advices.");
        var ra = new Remittance { SiteId = ho.Id, Site = ho, CreatedBy = u.Name };
        if (await ApplyRaAsync(u, ra, dto) is { } error) return (null, error);
        db.Remittances.Add(ra);
        RaHistory(ra, "Created", null, ra.VerificationStatus, null, u.Name);
        return await SaveRaAndReturnAsync(u, ra, null);
    }

    public async Task<(RemittanceDto? dto, string? error)> UpdateRemittanceAsync(CollectionUser u, int id, SaveRemittanceDto dto)
    {
        var (ra, gate) = await LoadEditableRaAsync(u, id);
        if (ra is null) return (null, gate);
        var oldBatch = ra.DepositBatchId;
        if (await ApplyRaAsync(u, ra, dto) is { } error) return (null, error);
        // The CRs carry the payment's check / slip: re-apply each one.
        foreach (var c in ra.Collections.Where(Live))
            if (await RebuildCrAsync(u, ra, c, c.Amount) is { } crError) return (null, crError);
        ra.UpdatedAt = DateTime.UtcNow;
        ra.UpdatedBy = u.Name;
        RaHistory(ra, "Edited", null, null, null, u.Name);
        return await SaveRaAndReturnAsync(u, ra, oldBatch);
    }

    private async Task<(Remittance? ra, string? error)> LoadEditableRaAsync(CollectionUser u, int id)
    {
        if (!u.CanEncodeRa) return (null, "Only a Custodian Verifier can change a Remittance Advice.");
        var ra = await LoadRaAsync(id);
        if (ra is null) return (null, "Remittance Advice not found.");
        if (!Editable(ra)) return (null, $"This Remittance Advice is {ra.VerificationStatus} and can no longer be changed.");
        return (ra, null);
    }

    private async Task<string?> ApplyRaAsync(CollectionUser u, Remittance ra, SaveRemittanceDto dto)
    {
        if (!Enum.TryParse<PaymentMode>(dto.PaymentMode, true, out var mode) || mode is not (PaymentMode.Check or PaymentMode.BankTransfer))
            return "A Remittance Advice is paid by cheque or by bank transfer / direct deposit.";
        var amount = Math.Round(dto.CheckAmount, 2);
        if (amount <= 0) return "Enter the amount of the payment.";
        string? bank = null, checkNo = null;
        DateOnly? checkDate = null;
        if (mode == PaymentMode.Check)
        {
            bank = dto.Bank?.Trim();
            checkNo = dto.CheckNo?.Trim();
            checkDate = dto.CheckDate;
            if (string.IsNullOrEmpty(bank) || string.IsNullOrEmpty(checkNo) || checkDate is null) return "Enter the check's bank, number and date.";
            if (bank.Length > 50 || checkNo.Length > 20) return "Check bank / number is too long.";
            if (checkDate > Today) return "The cheque is post-dated — a post-dated check is receipted with a PR or AR.";
        }
        var chain = dto.ChainName?.Trim() ?? "";
        if (chain.Length > 50) return "The chain name is too long (50 characters at most).";

        ra.PaymentMode = mode;
        ra.Bank = bank;
        ra.CheckNo = checkNo;
        ra.CheckDate = checkDate;
        ra.CheckAmount = amount;
        if (chain.Length > 0) ra.ChainName = chain;
        ra.Remarks = dto.Remarks?.Trim() is { Length: > 0 } rem ? rem[..Math.Min(rem.Length, 500)] : null;
        return await AssignRaDepositAsync(ra, dto.Deposit, u.Name);
    }

    /// <summary>One deposit for the whole payment, shared by its CRs: with a slip / ref. no., find-or-create per Head Office +
    /// account + ref; without one (optional, user 2026-10-15), the RA's own deposit.</summary>
    private async Task<string?> AssignRaDepositAsync(Remittance ra, DepositInfoDto? dep, string user)
    {
        if (ra.DepositBatch is { MatchedBankLineId: { } lineId } matched)
        {
            var same = dep is not null && dep.BankAccountId == matched.BankAccountId && (dep.SlipRefNo ?? "").Trim() == matched.SlipRefNo
                       && dep.DepositDate == matched.DepositDate;
            if (same) return null;
            if (matched.MatchConfirmedAt is not null) return "The deposit slip is already matched to the bank statement and can't be changed.";
            if (await db.BankStatementLines.FindAsync(lineId) is { } line)
                await matcher.UnapplyAsync(matched, line, user, "Deposit slip corrected before the match was approved");
        }
        DepositBatch? batch = null;
        if (dep is not null && (dep.BankAccountId is not null || !string.IsNullOrWhiteSpace(dep.SlipRefNo)))
        {
            if (dep.BankAccountId is not { } accountId) return "Choose the company bank account the payment was deposited into.";
            var account = (await DepositAccountsAsync(ra.SiteId)).FirstOrDefault(a => a.Id == accountId);
            if (account is null) return "That bank account isn't available for Head Office.";
            var bank = account.Label[..Math.Min(account.Label.Length, 50)];
            var slip = (dep.SlipRefNo ?? "").Trim();
            if (slip.Length > 30) return "The slip / reference number is too long (30 characters at most).";
            if (dep.DepositDate > Today.AddDays(1)) return "The deposit date is in the future.";

            batch = slip.Length > 0
                ? await db.DepositBatches.FirstOrDefaultAsync(b => b.SiteId == ra.SiteId && b.Bank == bank && b.SlipRefNo == slip)
                : ra.DepositBatch is { SlipRefNo: "" } own && own.BankAccountId == account.Id ? own : null;
            if (batch is null)
            {
                batch = new DepositBatch { SiteId = ra.SiteId, BankAccountId = account.Id, Bank = bank, SlipRefNo = slip, DepositDate = dep.DepositDate, CreatedBy = user };
                db.DepositBatches.Add(batch);
            }
            else if (batch.MatchConfirmedAt is not null) return $"Slip {(slip.Length > 0 ? slip : "(no reference)")} is already matched to the bank statement.";
            else if (await db.Collections.AnyAsync(x => x.DepositBatchId == batch.Id && x.RemittanceId != ra.Id && x.VerificationStatus != VerificationStatus.Voided))
                return $"Slip {slip} at {bank} is already used by another collection.";
            else batch.DepositDate = dep.DepositDate;
        }
        ra.DepositBatch = batch;
        foreach (var c in ra.Collections.Where(Live)) c.DepositBatch = batch;
        return null;
    }

    // ── CRs ─────────────────────────────────────────────────────────────────

    /// <summary>Adds a CR from its RA Excel (crId null) or replaces an existing CR's Excel.</summary>
    public async Task<(RemittanceDto? dto, string? error)> UploadRemittanceCrAsync(
        CollectionUser u, int id, int? crId, SaveRemittanceCrDto form, string fileName, Stream content, long length)
    {
        var (ra, gate) = await LoadEditableRaAsync(u, id);
        if (ra is null) return (null, gate);
        if (!RemittanceAdviceParser.Extensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase))
            return (null, "Upload the RA as an Excel file (.xls or .xlsx).");
        if (length <= 0 || length > MaxAttachmentBytes) return (null, "Files must be 10 MB or smaller.");

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer);
        RemittanceAdviceParser.Result file;
        try
        {
            // The Excel reader closes what it reads: parse a copy, keep the buffer for storing the file.
            file = RemittanceAdviceParser.Parse(new MemoryStream(buffer.ToArray()));
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or ExcelDataReader.Exceptions.ExcelReaderException or NotSupportedException)
        {
            return (null, $"{Path.GetFileName(fileName)}: {ex.Message}");
        }
        if (file.Desk is null) return (null, "The RA Excel doesn't say FOR LUZON or FOR VISMIN PROCESSING, so the HOMS desk is unknown.");

        // The Excel has to be for this payment.
        if (file.ChainName is { } chain)
        {
            if (string.IsNullOrWhiteSpace(ra.ChainName)) ra.ChainName = chain;
            else if (!string.Equals(ra.ChainName.Trim(), chain, StringComparison.OrdinalIgnoreCase))
                return (null, $"This RA Excel is for {chain}, but the Remittance Advice is for {ra.ChainName}.");
        }
        if (ra.PaymentMode == PaymentMode.Check && file.CheckNo is { } fileCheck && !string.IsNullOrEmpty(ra.CheckNo)
            && fileCheck.TrimStart('0') != ra.CheckNo.TrimStart('0'))
            return (null, $"This RA Excel is for check {fileCheck}, but the Remittance Advice is check {ra.CheckNo}.");
        // HOMS debits the deposit account's GL (BANKS4W) — the Excel names it (DEPOSIT BANK ACCTNO).
        if (file.DepositGl is { } excelGl && ra.DepositBatch?.BankAccount?.HomsAcctNo is { } raGl && excelGl != raGl)
            return (null, $"This RA Excel says the payment was deposited to GL {excelGl}, but the Remittance Advice's deposit account "
                          + $"({ra.DepositBatch.Bank}) is GL {raGl}.");

        var c = crId is { } cid ? ra.Collections.FirstOrDefault(x => x.Id == cid && Live(x)) : null;
        if (crId is not null && c is null) return (null, "CR not found on this Remittance Advice.");
        var isNew = c is null;
        c ??= new Collection { SiteId = ra.SiteId, Site = ra.Site, Remittance = ra, RemittanceId = ra.Id, ReceiptType = ReceiptType.CR, CreatedBy = u.Name };
        if (SetCrFields(c, form, file.OrDate, file.OrNo, file.DcrNo) is { } formError) return (null, formError);
        c.HomsDeptCode = file.Desk;

        // Lines replaced by the new file (acceptances go with them: it's a different file).
        db.RemittanceLines.RemoveRange(c.RemittanceLines);
        c.RemittanceLines = file.Blocks.Select(b => new RemittanceLine
        {
            RowNo = b.RowNo, DocNo = b.DocNo, WhseNo = b.DocNo / 1_000_000, CustKey = b.CustKey, DocDate = b.DocDate, BirSeries = b.BirSeries,
            Gross = b.Gross, Cewt = b.Cewt, RfcBoRtv = b.RfcBoRtv, Other = b.Other, Net = b.Net,
            DeductionsJson = JsonSerializer.Serialize(b.Deductions.Select(d => new RaDed(d.Type, d.Amount, d.Remark)).ToList(), DedJson),
        }).ToList();

        await ResolveLinesAsync(c);
        var amount = file.CheckAmount ?? file.Total ?? file.Blocks.Sum(b => b.Net);
        if (await RebuildCrAsync(u, ra, c, amount) is { } error) return (null, error);

        var oldExcel = c.Attachments.Where(IsRaExcel).ToList();
        foreach (var a in oldExcel) { c.Attachments.Remove(a); db.CollectionAttachments.Remove(a); }
        buffer.Position = 0;
        var (stored, storeError) = await StoreAttachmentAsync(c, fileName, buffer, buffer.Length, u.Name, raExcel: true);
        if (stored is null) return (null, storeError);

        var what = $"CR {c.ReceiptNo} · {Path.GetFileName(fileName)} · {file.Blocks.Count} invoice(s), {amount:N2}";
        if (isNew)
        {
            db.Collections.Add(c);
            History(c, "Created", null, c.VerificationStatus, $"From Remittance Advice #{ra.Id}", u.Name);
            RaHistory(ra, "CR added", null, null, what, u.Name);
        }
        else
        {
            History(c, "RA Excel uploaded", null, null, Path.GetFileName(fileName), u.Name);
            RaHistory(ra, "CR re-uploaded", null, null, what, u.Name);
        }
        var result = await SaveRaAndReturnAsync(u, ra, ra.DepositBatchId);
        if (result.error is null)
            foreach (var a in oldExcel) await store.DeleteAsync(a.BlobPath);
        return result;
    }

    public async Task<(RemittanceDto? dto, string? error)> UpdateRemittanceCrAsync(CollectionUser u, int id, int crId, SaveRemittanceCrDto form)
    {
        var (ra, gate) = await LoadEditableRaAsync(u, id);
        if (ra is null) return (null, gate);
        var c = ra.Collections.FirstOrDefault(x => x.Id == crId && Live(x));
        if (c is null) return (null, "CR not found on this Remittance Advice.");
        if (SetCrFields(c, form, null, null, null) is { } formError) return (null, formError);
        if (await RebuildCrAsync(u, ra, c, c.Amount) is { } error) return (null, error);
        c.UpdatedAt = DateTime.UtcNow;
        c.UpdatedBy = u.Name;
        History(c, "Edited", null, null, null, u.Name);
        return await SaveRaAndReturnAsync(u, ra, ra.DepositBatchId);
    }

    /// <summary>OR no. / DCR no. / OR date: what the Custodian Verifier typed, else the RA Excel's (they stay editable).</summary>
    private static string? SetCrFields(Collection c, SaveRemittanceCrDto form, DateOnly? excelOrDate, string? excelOrNo, int? excelDcrNo)
    {
        var receiptNo = (string.IsNullOrWhiteSpace(form.ReceiptNo) ? excelOrNo ?? "" : form.ReceiptNo).Trim();
        if (receiptNo.Length is 0 or > 20) return "Enter the OR no. (up to 20 characters).";
        int? dcr = excelDcrNo;
        if (!string.IsNullOrWhiteSpace(form.DcrNo))
        {
            if (!int.TryParse(form.DcrNo.Trim(), out var d) || d <= 0) return "The DCR no. must be a number.";
            dcr = d;
        }
        c.ReceiptNo = receiptNo;
        c.DcrNo = dcr;
        c.ReceiptDate = form.ReceiptDate ?? excelOrDate ?? (c.ReceiptDate == default ? Today : c.ReceiptDate);
        return null;
    }

    public async Task<(RemittanceDto? dto, string? error)> RemoveRemittanceCrAsync(CollectionUser u, int id, int crId)
    {
        var (ra, gate) = await LoadEditableRaAsync(u, id);
        if (ra is null) return (null, gate);
        var c = ra.Collections.FirstOrDefault(x => x.Id == crId && Live(x));
        if (c is null) return (null, "CR not found on this Remittance Advice.");
        var blobs = new List<string>();
        if (c.SubmittedAt is null)
        {
            // Never submitted: nothing to keep.
            blobs.AddRange(c.Attachments.Select(a => a.BlobPath));
            ra.Collections.Remove(c);
            db.Collections.Remove(c);
        }
        else
        {
            // It went to finance once: keep it as a voided receipt for the audit trail.
            c.VerificationStatus = VerificationStatus.Voided;
            c.VoidReason = $"Removed from Remittance Advice #{ra.Id}";
            History(c, "Voided", VerificationStatus.Returned, c.VerificationStatus, c.VoidReason, u.Name);
            c.DepositBatch = null;
        }
        RaHistory(ra, "CR removed", null, null, $"CR {c.ReceiptNo}", u.Name);
        var result = await SaveRaAndReturnAsync(u, ra, ra.DepositBatchId);
        if (result.error is null) foreach (var b in blobs) await store.DeleteAsync(b);
        return result;
    }

    /// <summary>Re-checks every line (e.g. once HO has fetched the missing invoices) and re-applies the CRs.</summary>
    public async Task<(RemittanceDto? dto, string? error)> RematchRemittanceAsync(CollectionUser u, int id)
    {
        var (ra, gate) = await LoadEditableRaAsync(u, id);
        if (ra is null) return (null, gate);
        foreach (var c in ra.Collections.Where(Live))
        {
            await ResolveLinesAsync(c);
            if (await RebuildCrAsync(u, ra, c, c.Amount) is { } error) return (null, error);
        }
        return await SaveRaAndReturnAsync(u, ra, ra.DepositBatchId);
    }

    /// <summary>The RA shows the invoice without its 12% VAT: gross = total ÷ 1.12, give or take the per-line VAT rounding.</summary>
    private static bool IsVatExcluded(decimal gross, decimal total) =>
        gross > 0 && gross < total && Math.Abs(gross - total / 1.12m) <= Math.Max(0.05m, total * 0.0001m);

    /// <summary>Finds each line's invoice (warehouse = the invoice no.'s first two digits) and flags the differences.</summary>
    private async Task ResolveLinesAsync(Collection c)
    {
        var lines = c.RemittanceLines.OrderBy(l => l.RowNo).ToList();
        var docNos = lines.Select(l => l.DocNo).Distinct().ToList();
        var invoices = await db.PostedInvoices.Where(i => docNos.Contains(i.DocNo)).ToListAsync();
        var paid = await PaidAsync(invoices.Select(i => i.Id).ToList(), c.Id == 0 ? null : c.Id);
        var reqIds = lines.Where(l => l.FetchRequestId is not null).Select(l => l.FetchRequestId!.Value).Distinct().ToList();
        var answered = await db.InvoiceSyncRequests
            .Where(r => reqIds.Contains(r.Id) && (r.Status == InvoiceSyncStatus.Done || r.Status == InvoiceSyncStatus.Failed))
            .Select(r => r.Id).ToListAsync();

        var seen = new HashSet<int>();
        foreach (var l in lines)
        {
            var f = new List<string>();
            var inv = invoices.FirstOrDefault(i => i.DocNo == l.DocNo && i.WhseNo == l.WhseNo);
            l.PostedInvoice = inv;
            l.PostedInvoiceId = inv?.Id;
            if (inv is null)
            {
                l.SystemTotal = l.SystemRfc = null;
                var gone = l.DocDate is null || BranchWarehouseMap.SiteCode(l.WhseNo) is null
                           || (l.FetchRequestId is { } rid && answered.Contains(rid));
                f.Add(gone ? RemittanceFlags.NotFound : RemittanceFlags.Fetching);
            }
            else
            {
                var sysRfc = -inv.RfcTotal;
                l.SystemTotal = inv.Total;
                l.SystemRfc = sysRfc;
                if (Math.Abs(l.Gross - inv.Total) > 0.01m)
                    f.Add(IsVatExcluded(l.Gross, inv.Total) ? RemittanceFlags.VatExcluded : RemittanceFlags.GrossDiff);
                if (!string.Equals(inv.CustKey.Trim(), l.CustKey.Trim(), StringComparison.OrdinalIgnoreCase)) f.Add(RemittanceFlags.CustDiff);
                if (l.DocDate is { } d && d != inv.DocDate) f.Add(RemittanceFlags.DateDiff);
                // The chain deducts returns BMS hasn't posted as an RFC (or the amounts differ): the records aren't aligned.
                if (Math.Abs(l.RfcBoRtv - sysRfc) > 0.01m)
                {
                    f.Add(RemittanceFlags.RfcDiff);
                    // HO is re-reading the invoice's RFCs (QueueFetchesAsync): wait for its answer.
                    if (l.FetchRequestId is { } rq && !answered.Contains(rq)) f.Add(RemittanceFlags.Fetching);
                }
                // Only the net is taken off the invoice (HOMS keeps the deductions open on A/R).
                if (!seen.Add(inv.Id)) f.Add(RemittanceFlags.Duplicate);
                else if (l.Net > Balance(inv, paid.GetValueOrDefault(inv.Id)) + 0.005m) f.Add(RemittanceFlags.OverBalance);
            }
            if (Math.Abs(l.Gross - l.Cewt - l.RfcBoRtv - l.Other - l.Net) > 0.01m) f.Add(RemittanceFlags.NetMismatch);
            l.Flags = string.Join(",", f);
        }
    }

    /// <summary>Re-applies the CR from its lines: lines that match their invoice are applied at their NET, nothing else —
    /// like the HOMS OR (DR bank / CR A/R net per invoice). The RA's deductions stay on its lines only.</summary>
    private async Task<string?> RebuildCrAsync(CollectionUser u, Remittance ra, Collection c, decimal amount)
    {
        var allocs = c.RemittanceLines.OrderBy(l => l.RowNo)
            .Where(l => l.PostedInvoiceId is not null && IsClear(l) && l.Net > 0)
            .Select(l => new SaveAllocationDto(l.PostedInvoiceId!.Value, l.Net)).ToList();
        var custKey = (c.RemittanceLines.Where(l => l.PostedInvoice is not null).OrderBy(l => l.RowNo).FirstOrDefault()?.PostedInvoice!.CustKey
                       ?? c.RemittanceLines.OrderBy(l => l.RowNo).FirstOrDefault()?.CustKey ?? "").Trim();
        var dto = new SaveCollectionDto(c.SiteId, nameof(ReceiptType.CR), c.ReceiptNo, c.ReceiptDate, custKey, ra.PaymentMode.ToString(), amount,
            ra.Bank, ra.CheckNo, ra.CheckDate, null, c.Remarks, allocs, [], MultiCustomer: true);
        if (await ApplyAsync(u, c, dto) is { } error) return $"CR {c.ReceiptNo}: {error}";
        c.DepositBatch = ra.DepositBatch;
        return null;
    }

    /// <summary>How often an "RFC differs" line asks HO again for its invoice's RFCs.</summary>
    private static readonly TimeSpan RfcRecheckEvery = TimeSpan.FromMinutes(30);

    /// <summary>Asks HO for the invoices not in HOMSys yet, and again for the invoices of "RFC differs" lines (BMS may have
    /// posted the RFC since; at most every 30 minutes). One request per branch warehouse, 50 invoices at most each.</summary>
    private async Task QueueFetchesAsync(CollectionUser u, Remittance ra)
    {
        var lines = ra.Collections.Where(Live).SelectMany(c => c.RemittanceLines).ToList();
        var need = lines
            .Where(l => l.PostedInvoiceId is null && l.FetchRequestId is null && l.DocDate is not null && Split(l.Flags).Contains(RemittanceFlags.Fetching))
            .Select(l => (Line: l, Date: l.DocDate!.Value)).ToList();

        var rfc = lines.Where(l => l.PostedInvoiceId is not null && Split(l.Flags) is var f && f.Contains(RemittanceFlags.RfcDiff) && !f.Contains(RemittanceFlags.Fetching)).ToList();
        if (rfc.Count > 0)
        {
            var reqIds = rfc.Where(l => l.FetchRequestId is not null).Select(l => l.FetchRequestId!.Value).Distinct().ToList();
            var since = DateTime.UtcNow - RfcRecheckEvery;
            var recent = await db.InvoiceSyncRequests.Where(r => reqIds.Contains(r.Id) && (r.CompletedAt == null || r.CompletedAt > since))
                .Select(r => r.Id).ToListAsync();
            var invIds = rfc.Select(l => l.PostedInvoiceId!.Value).Distinct().ToList();
            var dates = await db.PostedInvoices.Where(i => invIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.DocDate);
            need.AddRange(rfc.Where(l => l.FetchRequestId is not { } rq || !recent.Contains(rq))
                .Select(l => (Line: l, Date: dates[l.PostedInvoiceId!.Value])));
        }

        foreach (var wh in need.GroupBy(x => x.Line.WhseNo))
        {
            var code = BranchWarehouseMap.SiteCode(wh.Key);
            var site = code is null ? null : await db.Sites.FirstOrDefaultAsync(s => s.Code == code && s.IsActive);
            if (site is null) continue;
            foreach (var chunk in wh.DistinctBy(x => x.Line.DocNo).Chunk(50))
            {
                var (status, _) = await invoiceRequests.CreateAsync(u.Id, u.Name, true,
                    new CreateInvoiceSyncRequestDto(site.Id, chunk.Select(x => new InvoiceSyncItemDto(x.Line.DocNo, x.Date)).ToList()));
                if (status is null) continue;
                var nos = chunk.Select(x => x.Line.DocNo).ToHashSet();
                foreach (var (l, _) in wh.Where(x => nos.Contains(x.Line.DocNo)))
                {
                    l.FetchRequestId = status.Id;
                    // An RFC re-check: the line waits for HO's answer like a missing invoice.
                    if (!Split(l.Flags).Contains(RemittanceFlags.Fetching)) l.Flags = string.Join(",", Split(l.Flags).Append(RemittanceFlags.Fetching));
                }
            }
        }
    }

    /// <summary>Saves, asks HO for missing invoices, re-totals the payment's slip and returns the fresh RA.</summary>
    private async Task<(RemittanceDto? dto, string? error)> SaveRaAndReturnAsync(CollectionUser u, Remittance ra, int? oldBatchId)
    {
        foreach (var c in ra.Collections.Where(Live)) SetCompleteness(c);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_Collections_SiteId_ReceiptType_ReceiptNo") == true)
        {
            return (null, "That OR no. is already encoded at Head Office.");
        }
        await QueueFetchesAsync(u, ra);
        foreach (var id in new[] { oldBatchId, ra.DepositBatchId }.Where(x => x is not null).Distinct())
        {
            var b = await db.DepositBatches.FindAsync(id);
            if (b is null) continue;
            b.TotalAmount = await db.Collections.Where(x => x.DepositBatchId == id && x.VerificationStatus != VerificationStatus.Voided).SumAsync(x => x.Amount);
            await matcher.SyncBatchAsync(b, u.Name);
        }
        await db.SaveChangesAsync();
        return (await RaToDtoAsync(u, (await LoadRaAsync(ra.Id))!), null);
    }

    // ── workflow ────────────────────────────────────────────────────────────

    public async Task<(RemittanceDto? dto, string? error)> SubmitRemittanceAsync(CollectionUser u, int id)
    {
        var (ra, gate) = await LoadEditableRaAsync(u, id);
        if (ra is null) return (null, gate);
        // Re-check against today's balances and posted RFCs: another receipt may have paid an invoice since the upload.
        foreach (var c in ra.Collections.Where(Live))
        {
            await ResolveLinesAsync(c);
            if (await RebuildCrAsync(u, ra, c, c.Amount) is { } error) return (null, error);
        }
        if (MissingRa(ra) is { Count: > 0 } missing)
        {
            await SaveRaAndReturnAsync(u, ra, ra.DepositBatchId);   // keep the refreshed flags so the page shows why
            return (null, "Not ready — " + string.Join(" ", missing));
        }

        var from = ra.VerificationStatus;
        ra.VerificationStatus = VerificationStatus.ForVerification;
        ra.SubmittedAt = DateTime.UtcNow;
        ra.ReturnReason = null;
        foreach (var c in ra.Collections.Where(Live))
        {
            var was = c.VerificationStatus;
            c.VerificationStatus = VerificationStatus.ForVerification;
            c.SubmittedAt = ra.SubmittedAt;
            c.ReturnReason = null;
            History(c, "Submitted", was, c.VerificationStatus, $"With Remittance Advice #{ra.Id}", u.Name);
        }
        RaHistory(ra, "Submitted", from, ra.VerificationStatus, null, u.Name);
        return await SaveRaAndReturnAsync(u, ra, ra.DepositBatchId);
    }

    private static string? RaVerifyGate(CollectionUser u, Remittance ra) =>
        !u.CanVerify ? "Only a Finance Verifier can verify or return a Remittance Advice."
        : ra.VerificationStatus != VerificationStatus.ForVerification ? $"This Remittance Advice is {ra.VerificationStatus}, not waiting for verification."
        : string.Equals(ra.CreatedBy, u.Name, StringComparison.OrdinalIgnoreCase)
          || ra.Collections.Any(c => string.Equals(c.CreatedBy, u.Name, StringComparison.OrdinalIgnoreCase))
            ? "You encoded this Remittance Advice — another verifier has to check it."
        : null;

    /// <summary>The whole RA at once: every CR is verified (its OR queued for HOMS) and the payment's bank match approved.</summary>
    public async Task<(RemittanceDto? dto, string? error)> VerifyRemittanceAsync(CollectionUser u, int id)
    {
        var ra = await LoadRaAsync(id);
        if (ra is null) return (null, "Remittance Advice not found.");
        if (RaVerifyGate(u, ra) is { } gate) return (null, gate);

        foreach (var c in ra.Collections.Where(c => c.VerificationStatus == VerificationStatus.ForVerification).OrderBy(c => c.Id))
            await VerifyCoreAsync(u, c);
        ra.VerificationStatus = VerificationStatus.Verified;
        ra.VerifiedAt = DateTime.UtcNow;
        ra.VerifiedBy = u.Name;
        RaHistory(ra, "Verified", VerificationStatus.ForVerification, ra.VerificationStatus, null, u.Name);
        return await SaveRaAndReturnAsync(u, ra, ra.DepositBatchId);
    }

    /// <summary>Back to the Custodian Verifier with the reason, all CRs together.</summary>
    public async Task<(RemittanceDto? dto, string? error)> ReturnRemittanceAsync(CollectionUser u, int id, string? reason)
    {
        var ra = await LoadRaAsync(id);
        if (ra is null) return (null, "Remittance Advice not found.");
        if (RaVerifyGate(u, ra) is { } gate) return (null, gate);
        reason = reason?.Trim();
        if (string.IsNullOrEmpty(reason)) return (null, "Give the reason for returning it.");
        reason = reason[..Math.Min(reason.Length, 500)];

        ra.VerificationStatus = VerificationStatus.Returned;
        ra.ReturnReason = reason;
        foreach (var c in ra.Collections.Where(c => c.VerificationStatus == VerificationStatus.ForVerification))
        {
            c.VerificationStatus = VerificationStatus.Returned;
            c.ReturnReason = reason;
            History(c, "Returned", VerificationStatus.ForVerification, c.VerificationStatus, reason, u.Name);
        }
        RaHistory(ra, "Returned", VerificationStatus.ForVerification, ra.VerificationStatus, reason, u.Name);
        return await SaveRaAndReturnAsync(u, ra, ra.DepositBatchId);
    }

    /// <summary>Custodian Verifier while Draft / Returned; Finance Manager at any time (cancels its open HOMS postings).</summary>
    public async Task<(RemittanceDto? dto, string? error)> VoidRemittanceAsync(CollectionUser u, int id, string? reason)
    {
        var ra = await LoadRaAsync(id);
        if (ra is null) return (null, "Remittance Advice not found.");
        if (ra.VerificationStatus == VerificationStatus.Voided) return (null, "Already voided.");
        if (!u.CanManage && !(u.CanEncodeRa && Editable(ra)))
            return (null, "Only the Finance Manager can void a Remittance Advice that was already submitted.");
        reason = reason?.Trim();
        if (string.IsNullOrEmpty(reason)) return (null, "Give the reason for voiding.");
        reason = reason[..Math.Min(reason.Length, 500)];

        foreach (var c in ra.Collections.Where(Live))
        {
            if (await homs.CancelOpenAsync(c.Id, HomsPostingKind.Or) is { } posted) return (null, $"CR {c.ReceiptNo}: {posted}");
            var was = c.VerificationStatus;
            c.VerificationStatus = VerificationStatus.Voided;
            c.VoidReason = reason;
            History(c, "Voided", was, c.VerificationStatus, reason, u.Name);
        }
        var from = ra.VerificationStatus;
        ra.VerificationStatus = VerificationStatus.Voided;
        ra.VoidReason = reason;
        RaHistory(ra, "Voided", from, ra.VerificationStatus, reason, u.Name);
        return await SaveRaAndReturnAsync(u, ra, ra.DepositBatchId);
    }
}
