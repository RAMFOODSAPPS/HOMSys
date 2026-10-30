using System.Linq.Expressions;
using System.Text.RegularExpressions;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Collections;

/// <summary>
/// Matches deposit slips (DepositBatch) to bank-statement credits on the same company account:
/// exact amount (slip total = credit) and the credit dated on the deposit date up to <see cref="WindowDays"/> later.
/// The branch's slip number never appears on the bank line as such, so it only breaks ties when it shows up in
/// the line's text (reference / sequence no. / narrative). Ambiguous cases stay unmatched for finance to pick.
/// An automatic match is a suggestion until a Finance Verifier approves it; only then does every live collection on the
/// slip become Deposited. A no-match is never final (re-run on every upload).
/// </summary>
public partial class BankMatchService(AppDbContext db, HomsPostingService homs)
{
    public const int WindowDays = 4;
    public const string System = "system";
    /// <summary>MatchMethod of a line finance unmatched by hand: only a manual match may use it again.</summary>
    public const string Unmatched = "Unmatched";

    private static bool InWindow(BankStatementLine l, DateOnly deposit) =>
        Within(l.TxnDate, deposit) || (l.ValueDate is { } v && Within(v, deposit));

    private static bool Within(DateOnly d, DateOnly deposit) => d >= deposit && d <= deposit.AddDays(WindowDays);

    private static string Alnum(string? s) => NonAlnum().Replace(s ?? "", "").ToUpperInvariant();

    [GeneratedRegex("[^A-Za-z0-9]")]
    private static partial Regex NonAlnum();

    /// <summary>The slip ref appears in the bank line's own text (leading zeros ignored; at least 4 characters).</summary>
    public static bool SlipInLine(string slip, BankStatementLine l)
    {
        var s = Alnum(slip).TrimStart('0');
        if (s.Length < 4) return false;
        var text = Alnum($"{l.Description} {l.Reference} {l.CheckNo} {l.BankBranch}");
        return text.Contains(s);
    }

    /// <summary>Every live collection on the slip is submitted (For Verification or Verified): a slip holding a Draft or
    /// Returned collection is never matched to the bank statement.</summary>
    public static readonly Expression<Func<DepositBatch, bool>> Submitted = b =>
        b.Collections.All(c => c.VerificationStatus == VerificationStatus.Voided
                               || c.VerificationStatus == VerificationStatus.ForVerification
                               || c.VerificationStatus == VerificationStatus.Verified);

    /// <summary>Tries every open, submitted slip of the account against its open credits. Returns how many were matched.</summary>
    public async Task<int> MatchAccountAsync(int bankAccountId)
    {
        var batches = await db.DepositBatches
            .Where(b => b.BankAccountId == bankAccountId && b.MatchedBankLineId == null && b.TotalAmount > 0)
            .Where(Submitted)
            .ToListAsync();
        if (batches.Count == 0) return 0;
        var from = batches.Min(b => b.DepositDate);
        var lines = await db.BankStatementLines
            .Where(l => l.BankAccountId == bankAccountId && l.MatchedDepositBatchId == null && l.Credit > 0 && l.TxnDate >= from
                        && (l.MatchMethod == null || l.MatchMethod != Unmatched))
            .ToListAsync();

        var cands = batches.ToDictionary(b => b, b => lines.Where(l => l.Credit == b.TotalAmount && InWindow(l, b.DepositDate)).ToList());
        var matched = 0;

        // 1) The slip ref shows up in exactly one candidate line.
        foreach (var (b, ls) in cands)
        {
            var byRef = ls.Where(l => l.MatchedDepositBatchId == null && SlipInLine(b.SlipRefNo, l)).ToList();
            if (byRef.Count == 1) { await ApplyAsync(b, byRef[0], "Reference", System); matched++; }
        }

        // 2) One slip, one credit: the only candidate on both sides.
        var open = cands.Where(kv => kv.Key.MatchedBankLineId == null)
            .ToDictionary(kv => kv.Key, kv => kv.Value.Where(l => l.MatchedDepositBatchId == null).ToList());
        foreach (var (b, ls) in open)
        {
            if (ls.Count != 1) continue;
            var line = ls[0];
            if (open.Count(kv => kv.Value.Contains(line)) != 1) continue;
            await ApplyAsync(b, line, "Amount", System);
            matched++;
        }
        return matched;
    }

    /// <summary>After a slip changes: drop a match its new total no longer fits (or a suggested match whose slip is no
    /// longer fully submitted, e.g. a collection was returned), then try to match it.</summary>
    public async Task SyncBatchAsync(DepositBatch b, string user)
    {
        if (b.MatchedBankLineId is { } lineId)
        {
            var line = await db.BankStatementLines.FindAsync(lineId);
            var submitted = await db.DepositBatches.Where(x => x.Id == b.Id).AnyAsync(Submitted);
            if (line is not null && line.Credit == b.TotalAmount && (submitted || b.MatchConfirmedAt is not null)) return;
            if (line is not null)
                await UnapplyAsync(b, line, user, line.Credit != b.TotalAmount
                    ? "Slip total changed — no longer equals the bank credit"
                    : "A collection on the slip is back in draft — matched again once it's resubmitted");
            else b.MatchedBankLineId = null;
        }
        if (b.BankAccountId is { } accountId && b.TotalAmount > 0)
        {
            await db.SaveChangesAsync();
            await MatchAccountAsync(accountId);
        }
    }

    /// <summary>confirm: finance made the match by hand. An automatic match waits for the verifier (<see cref="ConfirmAsync"/>).</summary>
    public async Task ApplyAsync(DepositBatch b, BankStatementLine line, string method, string user, bool confirm = false)
    {
        line.MatchedDepositBatchId = b.Id;
        line.MatchMethod = method;
        line.MatchedAt = DateTime.UtcNow;
        line.MatchedBy = user;
        b.MatchedBankLineId = line.Id;
        b.MatchedAt = line.MatchedAt;
        if (confirm)
        {
            await ConfirmAsync(b, line, user);
            return;
        }
        foreach (var c in await LiveMembersAsync(b.Id))
            c.History.Add(new CollectionHistory
            {
                Action = "Bank match found", By = user,
                Reason = $"{line.TxnDate:yyyy-MM-dd} credit {line.Credit:N2} ({method}) — awaiting the verifier",
            });
    }

    /// <summary>The verifier approves the match: every live collection on the slip becomes Deposited, and each verified
    /// PDC on it is queued for its HOMS JV (DR cash in bank / CR 10552). A CR's OR was already queued at Verify.</summary>
    public async Task ConfirmAsync(DepositBatch b, BankStatementLine line, string user)
    {
        b.MatchConfirmedAt = DateTime.UtcNow;
        b.MatchConfirmedBy = user;
        foreach (var c in await LiveMembersAsync(b.Id))
        {
            c.PaymentStatus = PaymentStatus.Deposited;
            c.History.Add(new CollectionHistory
            {
                Action = "Bank matched", ToStatus = nameof(PaymentStatus.Deposited), By = user,
                Reason = $"{line.TxnDate:yyyy-MM-dd} credit {line.Credit:N2} ({line.MatchMethod})",
            });
            if (c.PaymentMode == PaymentMode.PostDatedCheck && c.VerificationStatus == VerificationStatus.Verified && c.PdcOutcome is null)
                await homs.QueueAsync(c, HomsPostingKind.PdcJv, user);
        }
    }

    /// <summary>byFinance: the line is kept out of automatic matching (finance said that pair was wrong).</summary>
    public async Task UnapplyAsync(DepositBatch b, BankStatementLine line, string user, string reason, bool byFinance = false)
    {
        line.MatchedDepositBatchId = null;
        line.MatchMethod = byFinance ? Unmatched : null;
        line.MatchedAt = byFinance ? DateTime.UtcNow : null;
        line.MatchedBy = byFinance ? user : null;
        b.MatchedBankLineId = null;
        b.MatchedAt = null;
        b.MatchConfirmedAt = null;
        b.MatchConfirmedBy = null;
        foreach (var c in await LiveMembersAsync(b.Id))
        {
            // Only an approved match made it Deposited; a suggestion never changed the status.
            var was = c.PaymentStatus;
            if (was == PaymentStatus.Deposited) c.PaymentStatus = CollectionService.Undeposited(c);
            c.History.Add(new CollectionHistory
            {
                Action = "Bank match removed", ToStatus = was == PaymentStatus.Deposited ? c.PaymentStatus.ToString() : null, Reason = reason, By = user,
            });
            await homs.CancelOpenAsync(c.Id, HomsPostingKind.PdcJv);   // a queued JV no longer applies (a written one is refused earlier)
        }
    }

    private Task<List<Collection>> LiveMembersAsync(int batchId) =>
        db.Collections.Include(c => c.History)
            .Where(c => c.DepositBatchId == batchId && c.VerificationStatus != VerificationStatus.Voided).ToListAsync();
}
