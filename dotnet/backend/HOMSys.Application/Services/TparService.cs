using System.Globalization;
using System.Security.Claims;
using HOMSys.Application.DTOs.Tasaf;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace HOMSys.Application.Services;

/// <summary>
/// TPAR page (permission `tpar`; user decision 2026-11-24). Encoders enter TPARs (Trade Promo Analysis Reports) against
/// Approved TASAFs — several per TASAF, by different BAs. Each TPAR goes Draft → Submitted → Approved, or Cancelled;
/// for now the encoder who entered it (or an Admin) does every step. Every TPAR but a Cancelled one holds its amount:
/// their total can't exceed the TASAF's approved TotalCost, and a TPAR No. is used only once. When the Approved TPARs
/// reach the approved amount the TASAF becomes Completed. Every change is logged in the TASAF's history, and the
/// TASAF form lists them read-only.
/// </summary>
public class TparService(
    ITparRepository repo,
    IUserRepository users,
    NotificationService notifications,
    ITasafPostEvalStarter postEvals,
    IHttpContextAccessor http)
{
    public const string NotFound = "TPAR not found.";
    public const string TasafNotFound = "TASAF not found.";
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    private ClaimsPrincipal? Principal => http.HttpContext?.User;
    private string CurrentUser => Principal?.FindFirstValue(ClaimTypes.Name) ?? "system";
    private int CurrentUserId => int.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private bool IsAdmin => Principal?.IsInRole("Admin") ?? false;

    public async Task<List<TparTasafDto>> GetTasafsAsync(bool includeCompleted)
    {
        var sites = await repo.GetActiveSitesAsync();
        var mine = await MyServicingAsync();
        return (await repo.GetTasafsAsync(includeCompleted)).Select(t => new TparTasafDto
        {
            Id = t.Id, RefNo = t.RefNo, Title = t.Title, Status = t.Status,
            SalesGroupName = t.SalesGroup?.Name, ProgramTypeName = t.ProgramType?.Name, ProponentName = t.ProponentName,
            StartDate = t.StartDate, EndDate = t.EndDate, ApprovedAt = t.DecidedAt,
            ApprovedAmount = t.TotalCost, TparTotal = Held(t), ApprovedTotal = ApprovedSum(t), Balance = Balance(t),
            TparCount = Live(t).Count(),
            Branches = Mine(BranchOptions(t, sites), mine)
        }).ToList();
    }

    /// <summary>One TPAR (for the form).</summary>
    public async Task<TparDto?> GetAsync(int id) =>
        await repo.GetTasafIdAsync(id) is { } tasafId && await repo.GetTasafAsync(tasafId) is { } t
            && t.Tpars.FirstOrDefault(p => p.Id == id) is { } p
            ? ToDto(p, t, CurrentUserId, IsAdmin) : null;

    public async Task<List<TparDto>> GetListAsync(int? tasafId) =>
        (await repo.GetListAsync(tasafId)).Select(p => ToDto(p, p.Tasaf!, CurrentUserId, IsAdmin)).ToList();

    /// <summary>New TPAR, saved as Draft (it holds its amount from now on).</summary>
    public Task<(TparDto? dto, string? error)> CreateAsync(SaveTparDto dto)
    {
        var (no, remarks, error) = Clean(dto);
        if (error is not null) return Task.FromResult<(TparDto?, string?)>((null, error));

        return repo.InTasafLockAsync(dto.TasafId, async () =>
        {
            var t = await repo.GetTasafAsync(dto.TasafId);
            if (t is null) return (null, TasafNotFound);
            if (t.Status != TasafStatus.Approved)
                return (null, $"TPARs can be added only to an Approved TASAF ({t.RefNo} is {t.Status}).");
            if (await repo.TparNoUsedOnAsync(no, null) is { } on) return (null, $"TPAR No. {no} is already used on TASAF {on}.");
            var (branch, branchError) = await PickBranchAsync(t, dto.SiteId);
            if (branchError is not null) return (null, branchError);
            if (OverBalance(t, dto.Amount, null) is { } over) return (null, over);

            var me = await MyNameAsync();
            var p = new TasafTpar
            {
                TparNo = no, Amount = dto.Amount, Remarks = remarks, Status = TasafTparStatus.Draft,
                SiteId = branch!.SiteId, BranchName = branch.Name,
                CreatedByUserId = CurrentUserId, CreatedByName = me, CreatedAt = DateTime.UtcNow
            };
            t.Tpars.Add(p);
            Log(t, "TPAR added", $"TPAR No. {no}: {Money(dto.Amount)} (Draft, {branch.Name}){(remarks.Length > 0 ? " — " + remarks : "")}", me);
            if (!await repo.SaveAsync()) return (null, $"TPAR No. {no} is already used.");
            return ((TparDto?)ToDto(p, t, CurrentUserId, IsAdmin), (string?)null);
        });
    }

    /// <summary>Changes a Draft TPAR.</summary>
    public async Task<(TparDto? dto, string? error)> UpdateAsync(int id, SaveTparDto dto)
    {
        var (no, remarks, error) = Clean(dto);
        if (error is not null) return (null, error);

        return await OnTparAsync(id, TasafTparStatus.Draft, "changed", async (p, t, me) =>
        {
            if (no != p.TparNo && await repo.TparNoUsedOnAsync(no, id) is { } on) return $"TPAR No. {no} is already used on TASAF {on}.";
            if (OverBalance(t, dto.Amount, id) is { } over) return over;
            var (branch, branchError) = await PickBranchAsync(t, dto.SiteId, p.SiteId);
            if (branchError is not null) return branchError;

            var changes = new List<string>();
            if (no != p.TparNo) changes.Add($"TPAR No. {p.TparNo} → {no}");
            if (dto.Amount != p.Amount) changes.Add($"amount {Money(p.Amount)} → {Money(dto.Amount)}");
            if (remarks != p.Remarks) changes.Add("remarks");
            if (branch!.SiteId != p.SiteId) changes.Add($"branch {(p.BranchName.Length > 0 ? p.BranchName : "(none)")} → {branch.Name}");
            if (changes.Count == 0) return null;

            p.TparNo = no; p.Amount = dto.Amount; p.Remarks = remarks; p.SiteId = branch.SiteId; p.BranchName = branch.Name;
            p.UpdatedAt = DateTime.UtcNow; p.UpdatedBy = me;
            Log(t, "TPAR changed", $"TPAR No. {no}: {string.Join(", ", changes)}", me);
            return null;
        });
    }

    /// <summary>Draft → Submitted.</summary>
    public Task<(TparDto? dto, string? error)> SubmitAsync(int id) =>
        OnTparAsync(id, TasafTparStatus.Draft, "submitted", (p, t, me) =>
        {
            p.Status = TasafTparStatus.Submitted;
            p.SubmittedAt = DateTime.UtcNow; p.UpdatedAt = p.SubmittedAt; p.UpdatedBy = me;
            Log(t, "TPAR submitted", $"TPAR No. {p.TparNo}: {Money(p.Amount)}", me);
            return Task.FromResult<string?>(null);
        });

    /// <summary>Submitted → Approved; completes the TASAF when the Approved TPARs reach the approved amount.</summary>
    public async Task<(TparDto? dto, string? error)> ApproveAsync(int id)
    {
        var completed = false;
        Tasaf? tasaf = null;
        string? approver = null;
        var result = await OnTparAsync(id, TasafTparStatus.Submitted, "approved", (p, t, me) =>
        {
            p.Status = TasafTparStatus.Approved;
            p.ApprovedAt = DateTime.UtcNow;
            p.ApprovedBy = me; p.UpdatedAt = DateTime.UtcNow; p.UpdatedBy = me;
            Log(t, "TPAR approved", $"TPAR No. {p.TparNo}: {Money(p.Amount)}", me);
            completed = AutoComplete(t, me);
            (tasaf, approver) = (t, me);
            return Task.FromResult<string?>(null);
        });
        // Completed: the Draft post evaluation is created now and its message tells the proponent (fallback below).
        var started = false;
        if (result.error is null && tasaf is not null && completed)
            try { started = await postEvals.StartAsync(tasaf.Id, $"its approved TPARs reach the approved amount; last TPAR approved by {approver}"); }
            catch (Exception) { /* the TPAR approval is saved; fall back to the plain notice */ }
        if (result.error is null && tasaf is not null && !started)
            await notifications.SendAsync([tasaf.ProponentUserId], completed ? "TASAF completed" : "TPAR approved",
                $"{tasaf.RefNo} — {tasaf.Title}: TPAR No. {result.dto!.TparNo} ({Money(result.dto.Amount)}) was approved by {approver}."
                + (completed ? " The approved TPARs now add up to the approved amount, so the TASAF is Completed."
                             : $" Balance: {Money(Balance(tasaf))}."),
                "/tasafs", "tasaf", tasaf.Id, emailRef: tasaf.RefNo);
        return result;
    }

    /// <summary>Draft / Submitted / Approved → Cancelled: frees its amount and its TPAR No.</summary>
    public Task<(TparDto? dto, string? error)> CancelAsync(int id) =>
        OnTparAsync(id, null, "cancelled", (p, t, me) =>
        {
            var from = p.Status;
            p.Status = TasafTparStatus.Cancelled;
            p.CancelledAt = DateTime.UtcNow;
            p.CancelledBy = me; p.UpdatedAt = DateTime.UtcNow; p.UpdatedBy = me;
            Log(t, "TPAR cancelled", $"TPAR No. {p.TparNo}: {Money(p.Amount)} (was {from})", me);
            return Task.FromResult<string?>(null);
        });

    /// <summary>Submitted / Approved → Draft (reason required) so it can be changed, then submitted and approved again.</summary>
    public Task<(TparDto? dto, string? error)> RevertAsync(int id, string? reason)
    {
        var why = (reason ?? "").Trim();
        if (why.Length == 0) return Task.FromResult<(TparDto?, string?)>((null, "Give the reason for reverting."));
        if (why.Length > 500) return Task.FromResult<(TparDto?, string?)>((null, "The reason is at most 500 characters."));
        return OnTparAsync(id, null, "reverted", (p, t, me) =>
        {
            if (p.Status is not (TasafTparStatus.Submitted or TasafTparStatus.Approved))
                return Task.FromResult<string?>($"TPAR No. {p.TparNo} is {p.Status} — only a Submitted or Approved TPAR can be reverted.");
            var from = p.Status;
            p.Status = TasafTparStatus.Draft;
            p.SubmittedAt = null; p.ApprovedAt = null; p.ApprovedBy = null;
            p.UpdatedAt = DateTime.UtcNow; p.UpdatedBy = me;
            Log(t, "TPAR reverted", $"TPAR No. {p.TparNo}: {Money(p.Amount)} ({from} → Draft) — {why}", me);
            return Task.FromResult<string?>(null);
        });
    }

    /// <summary>
    /// Loads the TPAR with its TASAF under the TASAF lock, checks who may act and that it is in <paramref name="from"/>
    /// (null = anything but Cancelled), runs the change and saves.
    /// </summary>
    private async Task<(TparDto? dto, string? error)> OnTparAsync(int id, string? from, string verb,
        Func<TasafTpar, Tasaf, string, Task<string?>> change)
    {
        var tasafId = await repo.GetTasafIdAsync(id);
        if (tasafId is null) return (null, NotFound);

        return await repo.InTasafLockAsync(tasafId.Value, async () =>
        {
            var t = await repo.GetTasafAsync(tasafId.Value);
            var p = t?.Tpars.FirstOrDefault(x => x.Id == id);
            if (t is null || p is null) return (null, NotFound);
            if (t.Status != TasafStatus.Approved)
                return (null, $"TASAF {t.RefNo} is {t.Status} — its TPARs can't be {verb}{(t.Status == TasafStatus.Completed ? " unless an Admin reopens it" : "")}.");
            if (!(IsAdmin || p.CreatedByUserId == CurrentUserId))
                return (null, $"Only {p.CreatedByName} (who entered it) or an Admin can do this.");
            if (from is not null ? p.Status != from : p.Status == TasafTparStatus.Cancelled)
                return (null, $"TPAR No. {p.TparNo} is {p.Status} — it can't be {verb}.");

            if (await change(p, t, await MyNameAsync()) is { } err) return (null, err);
            if (!await repo.SaveAsync()) return (null, $"TPAR No. {p.TparNo} is already used.");
            return ((TparDto?)ToDto(p, t, CurrentUserId, IsAdmin), (string?)null);
        });
    }

    // ── Rules ─────────────────────────────────────────────────
    /// <summary>
    /// Branches a TPAR on this TASAF can be for: the TASAF's own branches (Sites), or every active Site when it covers an
    /// ALL … scope (or lists none).
    /// </summary>
    private static List<TparBranchDto> BranchOptions(Tasaf t, List<Site> sites)
    {
        var byId = sites.ToDictionary(s => s.Id);
        if (t.Branches.Count == 0 || t.Branches.Any(b => b.SiteId is null))
            return sites.Select(s => new TparBranchDto { SiteId = s.Id, Code = s.Code, Name = s.Name }).ToList();
        return t.Branches.Where(b => b.SiteId is not null).OrderBy(b => b.LineNo)
            .Select(b => byId.TryGetValue(b.SiteId!.Value, out var s)
                ? new TparBranchDto { SiteId = s.Id, Code = s.Code, Name = s.Name }
                : new TparBranchDto { SiteId = b.SiteId!.Value, Name = b.Name })
            .DistinctBy(b => b.SiteId).ToList();
    }

    private async Task<(TparBranchDto? branch, string? error)> PickBranchAsync(Tasaf t, int? siteId, int? keep = null)
    {
        if (siteId is null) return (null, "Pick the branch.");
        var pick = BranchOptions(t, await repo.GetActiveSitesAsync()).FirstOrDefault(b => b.SiteId == siteId);
        if (pick is null) return (null, $"That branch is not one of TASAF {t.RefNo}'s branches.");
        if (siteId != keep && await MyServicingAsync() is { } mine && !mine.Contains(pick.SiteId))
            return (null, $"{pick.Name} is not one of your servicing branches (Users page).");
        return (pick, null);
    }

    /// <summary>
    /// The current user's servicing branches, or null when they may use any of the TASAF's branches (an Admin, or no
    /// servicing branches set).
    /// </summary>
    private async Task<HashSet<int>?> MyServicingAsync()
    {
        if (IsAdmin) return null;
        var ids = await repo.GetServicingSiteIdsAsync(CurrentUserId);
        return ids.Count == 0 ? null : ids.ToHashSet();
    }

    private static List<TparBranchDto> Mine(List<TparBranchDto> options, HashSet<int>? mine) =>
        mine is null ? options : options.Where(o => mine.Contains(o.SiteId)).ToList();

    private static IEnumerable<TasafTpar> Live(Tasaf t) => t.Tpars.Where(p => p.Status != TasafTparStatus.Cancelled);
    /// <summary>What the TPARs hold: every one but Cancelled.</summary>
    public static decimal Held(Tasaf t) => Live(t).Sum(p => p.Amount);
    public static decimal ApprovedSum(Tasaf t) => t.Tpars.Where(p => p.Status == TasafTparStatus.Approved).Sum(p => p.Amount);
    public static decimal Balance(Tasaf t) => t.TotalCost - Held(t);

    /// <summary>The TPARs (with this one at the new amount) may not add up to more than the approved TotalCost.</summary>
    private static string? OverBalance(Tasaf t, decimal amount, int? exceptId)
    {
        var others = Live(t).Where(p => exceptId is null || p.Id != exceptId).Sum(p => p.Amount);
        return others + amount > t.TotalCost
            ? $"The TPAR total would be {Money(others + amount)}, more than TASAF {t.RefNo}'s approved amount of {Money(t.TotalCost)} (balance {Money(t.TotalCost - others)})."
            : null;
    }

    /// <summary>Approved → Completed once the Approved TPARs add up to the approved amount. True when it just completed.</summary>
    private bool AutoComplete(Tasaf t, string me)
    {
        if (t.Status != TasafStatus.Approved || ApprovedSum(t) < t.TotalCost) return false;
        t.Status = TasafStatus.Completed;
        t.Actions.Add(new TasafAction
        {
            Action = "Completed", FromStatus = TasafStatus.Approved, ToStatus = TasafStatus.Completed,
            ByUserId = CurrentUserId, ByName = me, Role = "TPAR",
            Comment = $"Approved TPARs total {Money(t.TotalCost)} = the approved amount", At = DateTime.UtcNow
        });
        return true;
    }

    private void Log(Tasaf t, string action, string comment, string me)
    {
        t.UpdatedAt = DateTime.UtcNow;
        t.UpdatedBy = CurrentUser;
        t.Actions.Add(new TasafAction
        {
            Action = action, FromStatus = t.Status, ToStatus = t.Status, ByUserId = CurrentUserId, ByName = me,
            Role = IsAdmin ? "Admin" : "TPAR Encoder", Comment = comment, At = DateTime.UtcNow
        });
    }

    private static (string no, string remarks, string? error) Clean(SaveTparDto dto)
    {
        var no = (dto.TparNo ?? "").Trim();
        var remarks = (dto.Remarks ?? "").Trim();
        if (dto.TasafId <= 0) return (no, remarks, "Pick the TASAF.");
        if (no.Length == 0) return (no, remarks, "TPAR No. is required.");
        if (no.Length > 50) return (no, remarks, "TPAR No. is at most 50 characters.");
        if (dto.Amount <= 0) return (no, remarks, "Amount must be more than zero.");
        if (decimal.Round(dto.Amount, 2) != dto.Amount) return (no, remarks, "Amount can have at most 2 decimals.");
        if (remarks.Length > 500) return (no, remarks, "Remarks are at most 500 characters.");
        return (no, remarks, null);
    }

    private static string Money(decimal v) => "₱" + v.ToString("N2", En);

    private async Task<string> MyNameAsync() =>
        await users.GetByIdAsync(CurrentUserId) is { } u ? $"{u.FirstName} {u.LastName}".Trim() : CurrentUser;

    public static TparDto ToDto(TasafTpar p, Tasaf t, int userId, bool isAdmin)
    {
        var may = t.Status == TasafStatus.Approved && (isAdmin || p.CreatedByUserId == userId);
        return new TparDto
        {
            Id = p.Id, TasafId = t.Id, TasafRefNo = t.RefNo, TasafTitle = t.Title, TasafStatus = t.Status,
            TparNo = p.TparNo, Amount = p.Amount, Remarks = p.Remarks, SiteId = p.SiteId, BranchName = p.BranchName, Status = p.Status, CreatedByName = p.CreatedByName,
            CreatedAt = p.CreatedAt, UpdatedAt = p.UpdatedAt, UpdatedBy = p.UpdatedBy,
            SubmittedAt = p.SubmittedAt, ApprovedAt = p.ApprovedAt, ApprovedBy = p.ApprovedBy,
            CancelledAt = p.CancelledAt, CancelledBy = p.CancelledBy,
            CanEdit    = may && p.Status == TasafTparStatus.Draft,
            CanSubmit  = may && p.Status == TasafTparStatus.Draft,
            CanApprove = may && p.Status == TasafTparStatus.Submitted,
            CanCancel  = may && p.Status != TasafTparStatus.Cancelled,
            CanRevert  = may && p.Status is TasafTparStatus.Submitted or TasafTparStatus.Approved
        };
    }
}
