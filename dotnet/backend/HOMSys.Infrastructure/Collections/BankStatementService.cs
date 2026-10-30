using System.Security.Cryptography;
using HOMSys.Application.DTOs.Collections;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Collections;

/// <summary>Finance Manager: company bank accounts, daily statement uploads, and manual bank matching.</summary>
public class BankStatementService(AppDbContext db, IAttachmentStore store, BankMatchService matcher, CollectionService collections)
{
    public const long MaxFileBytes = 5 * 1024 * 1024;
    public static readonly string[] BankCodes = ["BDO", "BPI", "MBTC", "PBCOM", "CBC", "SBC", "LBP"];

    private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);
    private static string Digits(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());

    // ── accounts ────────────────────────────────────────────────────────────

    public async Task<List<BankAccountDto>> ListAccountsAsync()
    {
        var accounts = await db.BankAccounts.Include(a => a.Sites).Where(a => a.DeletedAt == null)
            .OrderBy(a => a.BankCode).ThenBy(a => a.AccountNo).ToListAsync();
        var withDeposits = (await db.DepositBatches.Where(b => b.BankAccountId != null).Select(b => b.BankAccountId!.Value).Distinct().ToListAsync()).ToHashSet();
        var latest = await db.BankStatementUploads.GroupBy(u => u.BankAccountId)
            .Select(g => new { g.Key, AsOf = g.Max(u => u.AsOfDate), At = g.Max(u => u.UploadedAt) }).ToDictionaryAsync(x => x.Key);
        var stats = await db.BankStatementLines.GroupBy(l => l.BankAccountId)
            .Select(g => new { g.Key, Lines = g.Count(), Unmatched = g.Count(l => l.Credit > 0 && l.MatchedDepositBatchId == null),
                               // Latest day that carries a running balance (a line without one must not blank the card).
                               Last = g.Max(l => l.Balance != null ? l.TxnDate : (DateOnly?)null) })
            .ToDictionaryAsync(x => x.Key);
        var balances = new Dictionary<int, decimal?>();
        foreach (var s in stats.Values.Where(s => s.Last is not null))
            balances[s.Key] = Closing(await db.BankStatementLines.Where(l => l.BankAccountId == s.Key && l.TxnDate == s.Last && l.Balance != null)
                .OrderBy(l => l.Id).ToListAsync());
        return accounts.Select(a => new BankAccountDto(a.Id, a.BankCode, a.AccountNo, a.AccountName, a.Label, a.IsActive,
            a.Sites.Select(s => s.SiteId).OrderBy(x => x).ToList(),
            latest.GetValueOrDefault(a.Id)?.AsOf, latest.GetValueOrDefault(a.Id)?.At is { } at ? Utc(at) : null,
            balances.GetValueOrDefault(a.Id), stats.GetValueOrDefault(a.Id)?.Lines ?? 0, stats.GetValueOrDefault(a.Id)?.Unmatched ?? 0,
            a.HomsAcctNo, a.BankAbbr, a.Synced, a.HomsBankAc,
            CanDelete: !withDeposits.Contains(a.Id) && !latest.ContainsKey(a.Id) && !stats.ContainsKey(a.Id))).ToList();
    }

    /// <summary>
    /// Running balance after the last transaction of the latest day. Some portals export newest-first, so the file
    /// order (Id) is read whichever way the balances chain (prev − debit + credit = next); ascending by default.
    /// </summary>
    private static decimal? Closing(List<BankStatementLine> day)
    {
        if (day.Count == 0) return null;
        bool Chains(BankStatementLine prev, BankStatementLine next) => prev.Balance - next.Debit + next.Credit == next.Balance;
        var ascending = Enumerable.Range(1, day.Count - 1).All(i => Chains(day[i - 1], day[i]));
        var descending = Enumerable.Range(1, day.Count - 1).All(i => Chains(day[i], day[i - 1]));
        return descending && !ascending ? day[0].Balance : day[^1].Balance;
    }

    public async Task<List<BankAccountOptionDto>> DepositOptionsAsync(int siteId) =>
        (await collections.DepositAccountsAsync(siteId)).Select(a => new BankAccountOptionDto(a.Id, a.Label, a.AccountName)).ToList();

    private static string? BadAccountNo(string no) =>
        Digits(no).Length < 6 || no.Length > 30 ? "Enter the account number as printed on the statement." : null;

    /// <summary>
    /// Adds or edits a company account (the BANKS4W sync was removed 2026-10-16, so HOMSys keeps the list). Slips keep the
    /// label they were made with (DepositBatch.Bank), so a number in use can still be corrected. An account that came from
    /// BANKS4W keeps BANKS4W's number in HomsBankAc for HOMS DEPBANK.
    /// </summary>
    public async Task<(BankAccountDto? dto, string? error)> SaveAccountAsync(int? id, SaveBankAccountDto dto, string user)
    {
        var code = (dto.BankCode ?? "").Trim().ToUpperInvariant();
        if (code.Length is 0 or > 10) return (null, "Enter the bank code (10 characters at most, e.g. BDO).");
        var no = (dto.AccountNo ?? "").Trim();
        if (BadAccountNo(no) is { } badNo) return (null, badNo);
        if (dto.HomsAcctNo is not > 0) return (null, "Enter the HOMS GL account (cash in bank, e.g. 10212).");
        var name = (dto.AccountName ?? "").Trim();
        if (name.Length == 0) return (null, "Enter the account name.");
        var abbr = (dto.BankAbbr ?? "").Trim().ToUpperInvariant() is { Length: > 0 } ab ? ab : code;
        if (abbr.Length > 20) return (null, "The HOMS bank abbreviation is 20 characters at most.");

        // Deleted accounts still hold their number and GL (unique indexes).
        var clash = await db.BankAccounts.Where(a => a.Id != id && ((a.BankCode == code && a.AccountNo == no) || a.HomsAcctNo == dto.HomsAcctNo))
            .Select(a => new { a.BankCode, a.AccountNo, a.HomsAcctNo, a.DeletedAt }).FirstOrDefaultAsync();
        if (clash is not null)
            return (null, (clash.HomsAcctNo == dto.HomsAcctNo ? $"GL {dto.HomsAcctNo} is already {clash.BankCode} {clash.AccountNo}" : $"{code} {no} already exists")
                          + (clash.DeletedAt is null ? "." : " (a deleted account)."));

        BankAccount? a;
        if (id is null) { a = new BankAccount { CreatedBy = user }; db.BankAccounts.Add(a); }
        else
        {
            a = await db.BankAccounts.Include(x => x.Sites).FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
            if (a is null) return (null, "Bank account not found.");
        }
        a.BankCode = code;
        a.AccountNo = no;
        a.AccountName = name.Length > 100 ? name[..100] : name;
        a.HomsAcctNo = dto.HomsAcctNo;
        a.BankAbbr = abbr;
        a.IsActive = dto.IsActive;
        var siteIds = (dto.SiteIds ?? []).Distinct().ToList();
        db.BankAccountSites.RemoveRange(a.Sites.Where(s => !siteIds.Contains(s.SiteId)));
        foreach (var sid in siteIds.Where(s => a.Sites.All(x => x.SiteId != s)))
            a.Sites.Add(new BankAccountSite { SiteId = sid });
        await db.SaveChangesAsync();
        return ((await ListAccountsAsync()).First(x => x.Id == a.Id), null);
    }

    /// <summary>
    /// Deletes an account HOMSys shouldn't use. One with deposits or statement uploads can't be deleted (they point at it).
    /// One that came from BANKS4W stays as a hidden deleted row (it still holds its GL and number).
    /// </summary>
    public async Task<(int? id, string? error)> DeleteAccountAsync(int id, string user)
    {
        var a = await db.BankAccounts.Include(x => x.Sites).FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (a is null) return (null, "Bank account not found.");
        var deposits = await db.DepositBatches.CountAsync(b => b.BankAccountId == id);
        var uploads = await db.BankStatementUploads.CountAsync(u => u.BankAccountId == id);
        if (deposits > 0 || uploads > 0)
        {
            var what = string.Join(" and ", new[] { (deposits, "deposit"), (uploads, "statement upload") }
                .Where(x => x.Item1 > 0).Select(x => $"{x.Item1} {x.Item2}{(x.Item1 == 1 ? "" : "s")}"));
            return (null, $"{a.Label} can't be deleted: it has {what}.");
        }
        db.BankAccountSites.RemoveRange(a.Sites);
        if (a.Synced)
        {
            a.DeletedAt = DateTime.UtcNow;
            a.DeletedBy = user;
            a.IsActive = false;
        }
        else db.BankAccounts.Remove(a);
        await db.SaveChangesAsync();
        return (id, null);
    }

    // ── upload ──────────────────────────────────────────────────────────────

    /// <summary>A statement file that passed every check, parsed and ready to import or preview.</summary>
    private sealed record Prepared(BankAccount Account, string Ext, string Hash, byte[] Bytes, BankStatementParser.Result Parsed, HashSet<string> Existing);

    private async Task<(Prepared? file, string? error)> PrepareAsync(int accountId, string fileName, Stream content, long length)
    {
        var account = await db.BankAccounts.FindAsync(accountId);
        if (account is null || !account.IsActive) return (null, "Choose an active bank account.");
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!BankStatementParser.Extensions.Contains(ext)) return (null, "Upload the bank portal's .xls, .xlsx or .csv export.");
        if (length <= 0 || length > MaxFileBytes) return (null, "Statement files must be 5 MB or smaller.");

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer);
        var bytes = buffer.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes));

        BankStatementParser.Result parsed;
        try
        {
            // The readers close what they read; keep the bytes for storing the original file.
            parsed = BankStatementParser.Parse(new MemoryStream(bytes, writable: false), fileName);
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or ExcelDataReader.Exceptions.ExcelReaderException or NotSupportedException)
        {
            return (null, $"Can't read {Path.GetFileName(fileName)}: {ex.Message}");
        }
        // BDO's export has no account number; the others must be this account's. Leading zeros don't count:
        // PBCOM's statement prints 224101003715 for HOMS's 0224101003715.
        if (parsed.AccountNo is { } fileAccount && Digits(fileAccount).TrimStart('0') != Digits(account.AccountNo).TrimStart('0'))
            return (null, $"This statement is for account {fileAccount}, not {account.Label}.");
        if (parsed.Lines.Count == 0) return (null, "The file has no transactions.");

        // Same-day re-uploads are fine (user 2026-10-16): only lines the account doesn't have yet come in. A line is matched
        // by date + debit + credit + running balance, counted (two equal deposits stay two), not by its text: portals
        // reword or re-time lines between downloads. A line whose exact fingerprint is already in is skipped as well.
        var dates = parsed.Lines.Select(l => l.TxnDate).Distinct().ToList();
        var have = await db.BankStatementLines.Where(l => l.BankAccountId == accountId && dates.Contains(l.TxnDate))
            .Select(l => new { l.TxnDate, l.Debit, l.Credit, l.Balance, l.Fingerprint }).ToListAsync();
        var left = have.GroupBy(l => (l.TxnDate, l.Debit, l.Credit, l.Balance)).ToDictionary(g => g.Key, g => g.Count());
        var prints = have.Select(l => l.Fingerprint).ToHashSet();
        var existing = new HashSet<string>();   // fingerprints of this file's lines that are already in
        foreach (var l in parsed.Lines)
        {
            var key = (l.TxnDate, l.Debit, l.Credit, l.Balance);
            if (left.GetValueOrDefault(key) > 0) { left[key]--; existing.Add(l.Fingerprint); }
            else if (prints.Contains(l.Fingerprint)) existing.Add(l.Fingerprint);
        }
        return (new Prepared(account, ext, hash, bytes, parsed, existing), null);
    }

    /// <summary>
    /// Opening → closing from the bank's own running balances, in file order. Portals list either way (BDO newest
    /// first, the rest oldest first), so the order is the one in which every balance chains (prev − debit + credit).
    /// </summary>
    private static (string? order, bool addsUp, decimal? opening, decimal? closing) FileBalances(List<BankStatementParser.Line> lines)
    {
        var b = lines.Where(l => l.Balance is not null).ToList();
        if (b.Count == 0) return (null, false, null, null);
        bool Chains(BankStatementParser.Line prev, BankStatementParser.Line next) => prev.Balance - next.Debit + next.Credit == next.Balance;
        var asc = Enumerable.Range(1, b.Count - 1).All(i => Chains(b[i - 1], b[i]));
        var desc = Enumerable.Range(1, b.Count - 1).All(i => Chains(b[i], b[i - 1]));
        if (!asc && !desc)
        {
            // Doesn't add up either way: show the last line of the latest day, no opening.
            var lastDay = b.Max(l => l.TxnDate);
            return (null, false, null, b.Last(l => l.TxnDate == lastDay).Balance);
        }
        var newestFirst = desc && !asc;
        var (first, last) = newestFirst ? (b[^1], b[0]) : (b[0], b[^1]);
        return (newestFirst ? "newest first" : "oldest first", true, first.Balance - first.Credit + first.Debit, last.Balance);
    }

    /// <summary>Reads the file and reports what importing it would bring in — nothing is saved.</summary>
    public async Task<(BankUploadPreviewDto? dto, string? error)> PreviewAsync(int accountId, string fileName, Stream content, long length)
    {
        var (file, error) = await PrepareAsync(accountId, fileName, content, length);
        if (file is null) return (null, error);
        var lines = file.Parsed.Lines;
        var (order, addsUp, opening, closing) = FileBalances(lines);
        var current = (await ListAccountsAsync()).FirstOrDefault(a => a.Id == accountId)?.Balance;
        return (new BankUploadPreviewDto(file.Account.Label, Path.GetFileName(fileName), lines.Min(l => l.TxnDate), lines.Max(l => l.TxnDate),
            lines.Count, lines.Count(l => !file.Existing.Contains(l.Fingerprint)),
            lines.Count(l => l.Credit > 0), lines.Sum(l => l.Credit), lines.Count(l => l.Debit > 0), lines.Sum(l => l.Debit),
            lines.Any(l => l.Balance is not null), addsUp, order, opening, closing, current), null);
    }

    /// <summary>
    /// Imports a statement the user has previewed and confirmed. When the file carries running balances the
    /// confirmed figure must be the file's closing balance, so nothing is imported unchecked.
    /// </summary>
    public async Task<(BankUploadResultDto? dto, string? error)> UploadAsync(int accountId, string fileName, Stream content, long length,
        bool confirmed, decimal? confirmedBalance, string user)
    {
        var (file, error) = await PrepareAsync(accountId, fileName, content, length);
        if (file is null) return (null, error);
        if (!confirmed) return (null, "Review the statement and confirm its balance before importing.");
        var closing = FileBalances(file.Parsed.Lines).closing;
        if (closing is not null && confirmedBalance != closing)
            return (null, $"The confirmed balance doesn't match this file's closing balance ({closing:N2}) — preview it again.");

        var (account, ext, hash, parsed, existing) = (file.Account, file.Ext, file.Hash, file.Parsed, file.Existing);
        if (parsed.Lines.All(l => existing.Contains(l.Fingerprint)))
            return (null, $"Nothing new to import: all {parsed.Lines.Count} line(s) of this file are already in {account.Label}.");
        var path = $"bank-statements/{account.BankCode}/{Guid.NewGuid():N}{ext}";
        await store.SaveAsync(path, new MemoryStream(file.Bytes, writable: false), ext == ".csv" ? "text/csv" : "application/vnd.ms-excel");

        var upload = new BankStatementUpload
        {
            BankAccountId = accountId, FileName = Path.GetFileName(fileName) is var fn && fn.Length > 200 ? fn[..200] : fn,
            FileHash = hash, BlobPath = path, AsOfDate = parsed.Lines.Max(l => l.TxnDate), LineCount = parsed.Lines.Count, UploadedBy = user,
        };
        var fresh = parsed.Lines.Where(l => !existing.Contains(l.Fingerprint)).Select(l => new BankStatementLine
        {
            BankAccountId = accountId, Upload = upload, TxnDate = l.TxnDate, ValueDate = l.ValueDate, TxnTime = l.Time,
            Description = l.Description, Reference = l.Reference, BankBranch = l.Branch, CheckNo = l.CheckNo,
            Debit = l.Debit, Credit = l.Credit, Balance = l.Balance, Fingerprint = l.Fingerprint,
        }).ToList();
        upload.NewLineCount = fresh.Count;
        db.BankStatementUploads.Add(upload);
        db.BankStatementLines.AddRange(fresh);
        await db.SaveChangesAsync();

        // Re-match every open slip of the account — an earlier "no match" is never final.
        var matched = await matcher.MatchAccountAsync(accountId);
        await db.SaveChangesAsync();
        return (new BankUploadResultDto(ToDto(upload, account), fresh.Count(l => l.Credit > 0), matched), null);
    }

    private static BankStatementUploadDto ToDto(BankStatementUpload u, BankAccount a) =>
        new(u.Id, u.BankAccountId, a.Label, u.FileName, u.AsOfDate, u.LineCount, u.NewLineCount, Utc(u.UploadedAt), u.UploadedBy);

    public async Task<List<BankStatementUploadDto>> ListUploadsAsync(int? accountId) =>
        (await db.BankStatementUploads.Include(u => u.BankAccount)
            .Where(u => accountId == null || u.BankAccountId == accountId)
            .OrderByDescending(u => u.Id).Take(200).ToListAsync())
        .Select(u => ToDto(u, u.BankAccount)).ToList();

    public async Task<BankAsOfDto> AsOfAsync()
    {
        var accounts = await ListAccountsAsync();
        return new BankAsOfDto(accounts.Max(a => a.AsOfDate), accounts.Max(a => a.LastUploadAt), accounts.Where(a => a.IsActive).ToList());
    }

    // ── lines / manual matching ─────────────────────────────────────────────

    /// <summary>filter: all | unmatched (open credits) | matched.</summary>
    public async Task<List<BankStatementLineDto>> ListLinesAsync(int? accountId, DateOnly? from, DateOnly? to, string? filter)
    {
        var q = db.BankStatementLines.AsQueryable();
        if (accountId is not null) q = q.Where(l => l.BankAccountId == accountId);
        if (from is not null) q = q.Where(l => l.TxnDate >= from);
        if (to is not null) q = q.Where(l => l.TxnDate <= to);
        if (filter == "unmatched") q = q.Where(l => l.Credit > 0 && l.MatchedDepositBatchId == null);
        if (filter == "matched") q = q.Where(l => l.MatchedDepositBatchId != null);
        var rows = await q.OrderByDescending(l => l.TxnDate).ThenBy(l => l.Id).Take(3000).Select(l => new
        {
            l, Label = l.BankAccount.BankCode + " " + l.BankAccount.AccountNo,
            Slip = l.MatchedDepositBatch != null ? l.MatchedDepositBatch.SlipRefNo : null,
            Site = l.MatchedDepositBatch != null ? l.MatchedDepositBatch.Site.Code : null,
            Confirmed = l.MatchedDepositBatch != null && l.MatchedDepositBatch.MatchConfirmedAt != null,
        }).ToListAsync();
        return rows.Select(r => new BankStatementLineDto(r.l.Id, r.l.BankAccountId, r.Label, r.l.TxnDate, r.l.ValueDate, r.l.TxnTime,
            r.l.Description, r.l.Reference, r.l.BankBranch, r.l.CheckNo, r.l.Debit, r.l.Credit, r.l.Balance,
            r.l.MatchedDepositBatchId, r.Slip, r.Site, r.l.MatchMethod, r.l.MatchedAt is { } m ? Utc(m) : null, r.l.MatchedBy,
            r.Confirmed)).ToList();
    }

    /// <summary>Open slips on the line's account within the date window; exact-amount ones first.</summary>
    public async Task<(List<DepositCandidateDto>? dto, string? error)> CandidatesAsync(int lineId)
    {
        var line = await db.BankStatementLines.FindAsync(lineId);
        if (line is null) return (null, "Bank line not found.");
        var from = line.TxnDate.AddDays(-BankMatchService.WindowDays - 3);
        var to = (line.ValueDate ?? line.TxnDate).AddDays(1);
        var rows = await db.DepositBatches
            .Where(b => b.BankAccountId == line.BankAccountId && b.MatchedBankLineId == null && b.TotalAmount > 0
                        && b.DepositDate >= from && b.DepositDate <= to)
            .Where(BankMatchService.Submitted)
            .Select(b => new { b.Id, Site = b.Site.Code, b.Bank, b.SlipRefNo, b.DepositDate, b.TotalAmount,
                Count = b.Collections.Count(c => c.VerificationStatus != VerificationStatus.Voided) })
            .ToListAsync();
        return (rows.Select(b => new DepositCandidateDto(b.Id, b.Site, b.Bank, b.SlipRefNo, b.DepositDate, b.TotalAmount, b.Count, b.TotalAmount == line.Credit))
            .OrderByDescending(b => b.AmountMatches).ThenBy(b => Math.Abs(b.TotalAmount - line.Credit)).ToList(), null);
    }

    public async Task<(BankStatementLineDto? dto, string? error)> MatchAsync(int lineId, int batchId, string user)
    {
        var line = await db.BankStatementLines.FindAsync(lineId);
        var batch = await db.DepositBatches.FindAsync(batchId);
        if (line is null || batch is null) return (null, "Bank line or deposit slip not found.");
        if (line.Credit <= 0) return (null, "Only a credit can settle a deposit.");
        if (line.MatchedDepositBatchId is not null || batch.MatchedBankLineId is not null) return (null, "One of them is already matched — unmatch it first.");
        if (batch.BankAccountId != line.BankAccountId) return (null, "The slip was deposited into a different account.");
        if (!await db.DepositBatches.Where(b => b.Id == batch.Id).AnyAsync(BankMatchService.Submitted))
            return (null, "A collection on this slip isn't submitted for verification yet — drafts aren't matched to the bank statement.");
        // Amounts must agree: a short or over deposit is fixed on the collections first (finance returns them).
        if (batch.TotalAmount != line.Credit)
            return (null, $"The slip totals {batch.TotalAmount:N2} but the bank credit is {line.Credit:N2}.");
        // A manual match is approved at once, except for a PDC slip: only the CNC Verifier confirms those (posts the JV).
        var pdcSlip = await db.Collections.AnyAsync(c => c.DepositBatchId == batch.Id && c.PaymentMode == PaymentMode.PostDatedCheck
                                                         && c.VerificationStatus != VerificationStatus.Voided);
        await matcher.ApplyAsync(batch, line, "Manual", user, confirm: !pdcSlip);
        await db.SaveChangesAsync();
        return ((await ListLinesAsync(line.BankAccountId, line.TxnDate, line.TxnDate, null)).First(x => x.Id == line.Id), null);
    }

    public async Task<(BankStatementLineDto? dto, string? error)> UnmatchAsync(int lineId, string user)
    {
        var line = await db.BankStatementLines.FindAsync(lineId);
        if (line?.MatchedDepositBatchId is not { } batchId) return (null, "This line isn't matched.");
        var batch = (await db.DepositBatches.FindAsync(batchId))!;
        if (await db.HomsPostings.FirstOrDefaultAsync(p => p.Collection.DepositBatchId == batchId && p.Kind == HomsPostingKind.PdcJv
                && (p.Status == HomsPostingStatus.Done || p.Status == HomsPostingStatus.Processing)) is { } jv)
            return (null, $"The matured PDC's JV {jv.DocNo} is already posted in HOMS from this match — reverse it there first.");
        await matcher.UnapplyAsync(batch, line, user, "Unmatched by finance", byFinance: true);
        await db.SaveChangesAsync();
        return ((await ListLinesAsync(line.BankAccountId, line.TxnDate, line.TxnDate, null)).First(x => x.Id == line.Id), null);
    }
}
