using System.Globalization;
using System.Security.Claims;
using HOMSys.Application.DTOs.Tasaf;
using HOMSys.Application.Services;
using HOMSys.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Data;

/// <summary>
/// Post Evaluation of a Completed TASAF (user request 2026-11-24; workbook › PROMO Post Eval Template). PROMO- program
/// types get the Promo Post Evaluation, the rest the simple Post Activity form. Draft → For Approval (the TASAF's own
/// approval matrix, rebuilt at submit like the TASAF's) → Approved, which closes the TASAF. Approvers Approve or Return
/// (comment required); an Admin may sign for the waiting approver. Revert (reason): the proponent while For Approval,
/// an Admin also once Approved (the TASAF goes back to Completed). Every step is logged in the TASAF's history.
/// </summary>
public class TasafPostEvalService(AppDbContext db, NotificationService notifications, IHttpContextAccessor http,
    HOMSys.Application.Interfaces.IFileStorage files)
    : HOMSys.Application.Interfaces.ITasafPostEvalStarter
{
    /// <summary>CreatedByName of post evaluations made automatically when the TASAF completed.</summary>
    public const string AutoCreator = "Automatic";

    /// <summary>
    /// The TASAF just became Completed: create its Draft post evaluation (pre-filled; actual cost = approved TPARs) and
    /// tell the proponent to fill it in. An untouched automatic Draft (e.g. after an Admin reopen) gets the new TPAR total.
    /// </summary>
    public async Task<bool> StartAsync(int tasafId, string reason)
    {
        var t = await Full().FirstOrDefaultAsync(x => x.Id == tasafId);
        if (t is null || t.Status != TasafStatus.Completed) return false;
        var p = t.PostEval;
        if (p is null)
        {
            p = NewFrom(t);
            p.Status = TasafPostEvalStatus.Draft;
            p.CreatedByName = AutoCreator;
            p.CreatedAt = DateTime.UtcNow;
            t.PostEval = p;
        }
        else if (p.Status == TasafPostEvalStatus.Draft && p.UpdatedAt is null && p.CreatedByName == AutoCreator)
        {
            p.ActualCost = TparApproved(t);
            p.PlannedCost = t.TotalCost;
        }
        else return false; // already being worked on
        await db.SaveChangesAsync();
        await notifications.SendAsync([t.ProponentUserId], "Post evaluation to fill in",
            $"{Head(t)} is Completed ({reason}). Its {KindLabel(p)} is ready as a Draft — enter the actuals and submit it "
            + "for approval; its approval closes the TASAF.", Link, EntityType, t.Id, emailRef: t.RefNo);
        return true;
    }

    public const string NotFound = "TASAF not found.";
    private const string Link = "/tasafs/post-eval";
    private const string EntityType = "postEval";
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    private ClaimsPrincipal? User => http.HttpContext?.User;
    private string CurrentUser => User?.FindFirstValue(ClaimTypes.Name) ?? "system";
    private int CurrentUserId => int.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private bool IsAdmin => User?.IsInRole("Admin") ?? false;
    private bool CanViewAll => IsAdmin || (User?.HasClaim("permission", "tasaf-view-all") ?? false);

    /// <summary>Promo Post Evaluation for PROMO- program types, else the Post Activity form.</summary>
    public static string KindOf(string? programTypeName) =>
        (programTypeName ?? "").TrimStart().StartsWith("PROMO", StringComparison.OrdinalIgnoreCase)
            ? TasafPostEvalKind.Promo : TasafPostEvalKind.Activity;

    private IQueryable<Tasaf> Full() => db.Tasafs
        .Include(t => t.SalesGroup).Include(t => t.ProgramType).Include(t => t.Objective).Include(t => t.TargetMarket)
        .Include(t => t.Areas).ThenInclude(a => a.Area)
        .Include(t => t.Chains).ThenInclude(c => c.Chain)
        .Include(t => t.Channels).ThenInclude(c => c.Channel)
        .Include(t => t.ProductCategories).ThenInclude(c => c.ProductGroup)
        .Include(t => t.SubCategories)
        .Include(t => t.Customers)
        .Include(t => t.Products)
        .Include(t => t.Tpars)
        .Include(t => t.Steps)
        .Include(t => t.Actions)
        .Include(t => t.PostEval).ThenInclude(p => p!.Lines)
        .Include(t => t.PostEval).ThenInclude(p => p!.Steps)
        .Include(t => t.PostEval).ThenInclude(p => p!.Attachments)
        .AsSplitQuery();

    private bool CanView(Tasaf t) =>
        CanViewAll || t.ProponentUserId == CurrentUserId || t.Steps.Any(s => s.UserId == CurrentUserId)
        || (t.PostEval?.Steps.Any(s => s.UserId == CurrentUserId) ?? false);

    private bool IsOwner(Tasaf t) => t.ProponentUserId == CurrentUserId || IsAdmin;
    private static bool Open(TasafPostEval? p) => p is null || p.Status is TasafPostEvalStatus.Draft or TasafPostEvalStatus.Returned;
    private bool CanEdit(Tasaf t) => t.Status == TasafStatus.Completed && Open(t.PostEval) && IsOwner(t);
    private bool AwaitingMe(TasafPostEval? p) =>
        p is { Status: TasafPostEvalStatus.ForApproval } && p.CurrentApproverUserId == CurrentUserId && CurrentUserId != 0;
    private bool CanAdminDecide(TasafPostEval? p) =>
        IsAdmin && p is { Status: TasafPostEvalStatus.ForApproval } && Current(p) is not null && !AwaitingMe(p);
    private bool CanRevert(Tasaf t) => t.PostEval is { } p && (
        (p.Status == TasafPostEvalStatus.ForApproval && IsOwner(t))
        || (p.Status == TasafPostEvalStatus.Approved && IsAdmin && t.Status == TasafStatus.Closed));
    private static TasafPostEvalStep? Current(TasafPostEval p) =>
        p.CurrentStepSeq is int seq ? p.Steps.FirstOrDefault(s => s.Seq == seq) : null;

    // ── Read ──────────────────────────────────────────────────
    public async Task<List<TasafPostEvalListItemDto>> GetListAsync()
    {
        var me = CurrentUserId;
        var q = db.Tasafs.AsNoTracking()
            .Where(t => t.Status == TasafStatus.Completed || t.Status == TasafStatus.Closed);
        if (!CanViewAll)
            q = q.Where(t => t.ProponentUserId == me || t.Steps.Any(s => s.UserId == me)
                             || (t.PostEval != null && t.PostEval.Steps.Any(s => s.UserId == me)));
        var list = await q.Include(t => t.SalesGroup).Include(t => t.ProgramType)
            .Include(t => t.PostEval).ThenInclude(p => p!.Steps)
            .AsSplitQuery()
            .OrderByDescending(t => t.UpdatedAt ?? t.DecidedAt).ThenByDescending(t => t.Id)
            .ToListAsync();
        return list.Select(t =>
        {
            var p = t.PostEval;
            var cur = p is null ? null : Current(p);
            return new TasafPostEvalListItemDto
            {
                TasafId = t.Id, RefNo = t.RefNo, Title = t.Title, TasafStatus = t.Status,
                SalesGroupName = t.SalesGroup?.Name, ProgramTypeName = t.ProgramType?.Name, ProponentName = t.ProponentName,
                StartDate = t.StartDate, EndDate = t.EndDate,
                Kind = p?.Kind ?? KindOf(t.ProgramType?.Name),
                PostEvalId = p?.Id, Status = p?.Status,
                PlannedCost = p?.PlannedCost ?? t.TotalCost, ActualCost = p?.ActualCost,
                OverBudget = p?.ActualCost is decimal a ? a > (p.PlannedCost) : null,
                CurrentRoleCode = cur?.RoleCode, CurrentApproverName = cur?.UserName,
                AwaitingMe = AwaitingMe(p), IsMine = t.ProponentUserId == me,
                SubmittedAt = p?.SubmittedAt, DecidedAt = p?.DecidedAt
            };
        }).ToList();
    }

    /// <summary>The TASAF's post evaluation, or a new one (Id 0) pre-filled from it.</summary>
    public async Task<(TasafPostEvalDto? dto, string? error)> GetAsync(int tasafId)
    {
        var t = await Full().AsNoTracking().FirstOrDefaultAsync(x => x.Id == tasafId);
        if (t is null) return (null, NotFound);
        if (!CanView(t)) return (null, "You can only open post evaluations of your own TASAFs or ones you approve.");
        if (t.PostEval is null && t.Status != TasafStatus.Completed)
            return (null, $"TASAF {t.RefNo} is {t.Status} — a post evaluation is made once it is Completed (its TPARs are done).");
        return (Map(t, t.PostEval ?? NewFrom(t)), null);
    }

    private TasafPostEval NewFrom(Tasaf t) => new()
    {
        TasafId = t.Id,
        Kind = KindOf(t.ProgramType?.Name),
        PlannedCost = t.TotalCost,
        PlannedDoors = t.Customers.Count,
        ActualCost = TparApproved(t),
        Lines = t.Products.OrderBy(p => p.LineNo).Where(p => p.CProdNo.Length > 0).Select((p, i) => new TasafPostEvalLine
        {
            LineNo = i + 1, CProdNo = p.CProdNo, Sku = p.Sku, LspPerCase = p.LspPerCase,
            PrePromoCs = p.P3MoAvgCs ?? p.SplyCs, PlannedCs = p.PromotedVolCs
        }).ToList()
    };

    private static decimal TparApproved(Tasaf t) =>
        t.Tpars.Where(p => p.Status == TasafTparStatus.Approved).Sum(p => p.Amount);

    // ── Save ──────────────────────────────────────────────────
    public async Task<(TasafPostEvalDto? dto, string? error)> SaveAsync(int tasafId, SaveTasafPostEvalDto dto)
    {
        var t = await Full().FirstOrDefaultAsync(x => x.Id == tasafId);
        if (t is null) return (null, NotFound);
        if (!CanEdit(t)) return (null, EditError(t));
        if (Apply(t, dto, await MyNameAsync()) is { } err) return (null, err);
        await db.SaveChangesAsync();
        return await GetAsync(tasafId);
    }

    private string EditError(Tasaf t) =>
        t.Status != TasafStatus.Completed ? $"TASAF {t.RefNo} is {t.Status} — the post evaluation can be changed only while it is Completed."
        : !Open(t.PostEval) ? $"The post evaluation is {t.PostEval!.Status} — only a Draft or Returned one can be changed."
        : "Only the proponent (or an Admin) can fill in the post evaluation.";

    private string? Apply(Tasaf t, SaveTasafPostEvalDto dto, string myName)
    {
        var explanation = (dto.Explanation ?? "").Trim();
        var conclusion = (dto.Conclusion ?? "").Trim();
        if (explanation.Length > 4000 || conclusion.Length > 4000) return "Explanation and Conclusion can have at most 4000 characters.";
        if (dto.ActualCost is < 0) return "Actual cost can't be negative.";
        if (dto.ActualCost is decimal c && decimal.Round(c, 2) != c) return "Actual cost can have at most 2 decimals.";
        if (dto.ActualDoors is < 0) return "Number of doors can't be negative.";
        if (dto.ActualStartDate is DateOnly s && dto.ActualEndDate is DateOnly e && e < s) return "The actual end date is before the start date.";
        if (dto.Lines.Any(l => l.ActualCs is < 0 || l.PostPromoCs is < 0)) return "Volumes can't be negative.";

        var p = t.PostEval;
        if (p is null)
        {
            p = NewFrom(t);
            p.CreatedByUserId = CurrentUserId;
            p.CreatedByName = myName;
            p.CreatedAt = DateTime.UtcNow;
            t.PostEval = p;
        }
        else
        {
            p.UpdatedAt = DateTime.UtcNow;
            p.UpdatedBy = CurrentUser;
        }
        p.PlannedCost = t.TotalCost;
        p.ActualStartDate = dto.ActualStartDate;
        p.ActualEndDate = dto.ActualEndDate;
        p.ActualDoors = p.Kind == TasafPostEvalKind.Promo ? dto.ActualDoors : null;
        p.Explanation = explanation;
        p.Conclusion = conclusion;
        p.ActualCost = dto.ActualCost;
        foreach (var l in p.Lines)
        {
            var d = dto.Lines.FirstOrDefault(x => x.LineNo == l.LineNo);
            l.ActualCs = d?.ActualCs;
            l.PostPromoCs = d?.PostPromoCs;
        }
        return null;
    }

    // ── Workflow ──────────────────────────────────────────────
    public async Task<(TasafPostEvalDto? dto, string? error)> SubmitAsync(int tasafId, SaveTasafPostEvalDto dto)
    {
        var t = await Full().FirstOrDefaultAsync(x => x.Id == tasafId);
        if (t is null) return (null, NotFound);
        if (!CanEdit(t)) return (null, EditError(t));
        if (Apply(t, dto, await MyNameAsync()) is { } err) return (null, err);
        var p = t.PostEval!;

        var missing = new List<string>();
        if (p.ActualStartDate is null) missing.Add("Actual start date");
        if (p.ActualEndDate is null) missing.Add("Actual end date");
        if (p.ActualCost is null) missing.Add("Actual cost");
        if (p.Kind == TasafPostEvalKind.Promo)
        {
            if (p.ActualDoors is null) missing.Add("Actual number of doors");
            var bad = p.Lines.Where(l => l.ActualCs is null).Select(l => l.LineNo).ToList();
            if (bad.Count > 0) missing.Add($"Actual Cs on SKU row {string.Join(", ", bad)}");
            if (p.Explanation.Length == 0) missing.Add("Explanation (what worked / what didn't)");
            if (p.Conclusion.Length == 0) missing.Add("Conclusion / Recommendation");
        }
        else if (p.Explanation.Length == 0) missing.Add("Remarks");
        if (missing.Count > 0) return (null, "Complete these before submitting: " + string.Join(", ", missing) + ".");

        // Same approval matrix as the TASAF (program type's approval type, rules on the approved total, the sales
        // group's approvers) — rebuilt now, so a changed approver signs.
        var (steps, typeName, chainError) = await BuildChainAsync(t);
        if (chainError is not null) return (null, chainError);
        var first = steps.First(s => s.Status == TasafStepStatus.Pending);

        var resubmit = p.Status == TasafPostEvalStatus.Returned;
        var me = await MyNameAsync();
        p.Steps.Clear();
        p.Steps.AddRange(steps);
        p.Status = TasafPostEvalStatus.ForApproval;
        p.ApprovalTypeName = typeName;
        p.SubmittedAt = DateTime.UtcNow;
        p.DecidedAt = null;
        p.CurrentStepSeq = first.Seq;
        p.CurrentApproverUserId = first.UserId;
        var byAdmin = t.ProponentUserId != CurrentUserId;
        t.Actions.Add(NewAction(resubmit ? "Post-eval resubmit" : "Post-eval submitted", t.Status, t.Status,
            byAdmin ? "Admin" : "KAM", Flag(p), byAdmin ? $"{me} (Admin, for {t.ProponentName})" : t.ProponentName));
        await db.SaveChangesAsync();

        await notifications.SendAsync([first.UserId], "Post evaluation for your approval",
            $"{Head(t)} — {KindLabel(p)} from {t.ProponentName}{(resubmit ? ", resubmitted" : "")}. {Flag(p)}. You sign as {first.RoleCode}.",
            Link, EntityType, t.Id, emailRef: t.RefNo);
        if (byAdmin)
            await notifications.SendAsync([t.ProponentUserId], "Post evaluation submitted by Admin",
                $"{Head(t)}: the {KindLabel(p).ToLowerInvariant()} was submitted by {me} (Admin) on your behalf.", Link, EntityType, t.Id, emailRef: t.RefNo);
        return await GetAsync(tasafId);
    }

    private async Task<(List<TasafPostEvalStep> steps, string typeName, string? error)> BuildChainAsync(Tasaf t)
    {
        if (t.ProgramType is not TasafProgramType { ApprovalTypeId: int approvalTypeId } pt)
            return ([], "", $"Program type {t.ProgramType?.Name} has no Approval Type. Ask an admin to set it in TASAF Setup.");
        var typeName = await db.TasafApprovalTypes.Where(x => x.Id == approvalTypeId).Select(x => x.Name).FirstOrDefaultAsync() ?? "";
        var rules = await db.TasafApprovalRules.Where(r => r.ApprovalTypeId == approvalTypeId).ToDictionaryAsync(r => r.RoleId, r => r.OverAmount);
        var sgId = t.SalesGroupId ?? 0;
        var matrix = await db.TasafApprovers.Where(a => a.SalesGroupId == sgId).ToDictionaryAsync(a => a.RoleId, a => a.UserId);
        var roles = (await db.TasafApprovalRoles.Where(r => r.IsActive).ToListAsync())
            .Where(r => rules.TryGetValue(r.Id, out var over) && (over is null || t.TotalCost > over))
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Id).ToList();
        if (roles.Count == 0) return ([], typeName, $"No approval roles apply to {typeName}. Ask an admin to check Approval Rules in TASAF Setup.");

        var steps = new List<TasafPostEvalStep>();
        foreach (var r in roles)
        {
            var user = matrix.GetValueOrDefault(r.Id) is int uid ? await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid) : null;
            var vacant = user is null || !user.IsActive;
            steps.Add(new TasafPostEvalStep
            {
                Seq = steps.Count + 1, RoleId = r.Id, RoleCode = r.Code.Length > 0 ? r.Code : r.Name, RoleName = r.Name,
                UserId = vacant ? null : user!.Id, UserName = vacant ? "" : FullName(user!),
                Status = vacant ? TasafStepStatus.Skipped : TasafStepStatus.Pending,
                Comment = vacant ? "Vacant — skipped" : ""
            });
        }
        if (steps.All(s => s.Status != TasafStepStatus.Pending))
            return ([], typeName, $"No approvers are assigned for {t.SalesGroup?.Name} ({string.Join(", ", steps.Select(s => s.RoleCode))}). Ask an admin to fill in the Approval Matrix in TASAF Setup.");
        return (steps, typeName, null);
    }

    /// <summary>Approve / return the current step (comment required). The last approval closes the TASAF.</summary>
    public async Task<(TasafPostEvalDto? dto, string? error)> DecideAsync(int tasafId, TasafDecisionDto dto)
    {
        var t = await Full().FirstOrDefaultAsync(x => x.Id == tasafId);
        if (t is null) return (null, NotFound);
        var p = t.PostEval;
        var step = p is null ? null : Current(p);
        if (p is null || step is null || (!AwaitingMe(p) && !CanAdminDecide(p)))
            return (null, "This post evaluation is not waiting for your action.");
        if (t.Status != TasafStatus.Completed) return (null, $"TASAF {t.RefNo} is {t.Status}, not Completed.");
        var decision = dto.Decision.Trim().ToLowerInvariant();
        var comment = dto.Comment.Trim();
        if (decision is not ("approve" or "return")) return (null, "Choose Approve or Return.");
        if (comment.Length == 0) return (null, "Please enter a comment.");
        if (comment.Length > 1000) return (null, "Comment must be 1000 characters or fewer.");

        var byAdmin = !AwaitingMe(p);
        var me = await MyNameAsync();
        var behalf = step.UserName.Length > 0 ? step.UserName : step.RoleCode;
        var myName = byAdmin ? $"{me} (Admin, for {behalf})" : (step.UserName.Length > 0 ? step.UserName : me);
        var how = byAdmin ? " by Admin" : "";
        var signed = p.Steps.Where(s => s.Status == TasafStepStatus.Approved).Select(s => s.UserId).ToList();
        step.ActedAt = DateTime.UtcNow;
        step.Comment = comment;
        var head = Head(t);

        if (decision == "approve")
        {
            step.Status = TasafStepStatus.Approved;
            var next = p.Steps.Where(s => s.Seq > step.Seq && s.Status == TasafStepStatus.Pending).OrderBy(s => s.Seq).FirstOrDefault();
            if (next is not null)
            {
                p.CurrentStepSeq = next.Seq;
                p.CurrentApproverUserId = next.UserId;
                t.Actions.Add(NewAction("Post-eval approved", t.Status, t.Status, step.RoleCode, comment, myName));
                await db.SaveChangesAsync();
                await notifications.SendAsync([next.UserId], "Post evaluation for your approval",
                    $"{head} — {KindLabel(p)} from {t.ProponentName}, approved by {myName} ({step.RoleCode}). {Flag(p)}. You sign as {next.RoleCode}.",
                    Link, EntityType, t.Id, emailRef: t.RefNo);
                await notifications.SendAsync([t.ProponentUserId], "Post evaluation progress",
                    $"{head}: the {KindLabel(p).ToLowerInvariant()} was approved by {myName} ({step.RoleCode}): \"{comment}\". Now with {next.UserName} ({next.RoleCode}).",
                    Link, EntityType, t.Id, emailRef: t.RefNo);
            }
            else
            {
                p.Status = TasafPostEvalStatus.Approved;
                p.CurrentStepSeq = null;
                p.CurrentApproverUserId = null;
                p.DecidedAt = DateTime.UtcNow;
                var from = t.Status;
                t.Status = TasafStatus.Closed;
                t.UpdatedAt = DateTime.UtcNow;
                t.UpdatedBy = CurrentUser;
                t.Actions.Add(NewAction("Post-eval approved", from, t.Status, step.RoleCode, comment, myName));
                t.Actions.Add(NewAction("Closed", from, t.Status, step.RoleCode, Flag(p), myName));
                await db.SaveChangesAsync();
                await notifications.SendAsync([t.ProponentUserId, .. signed], "TASAF closed",
                    $"{head}: the {KindLabel(p).ToLowerInvariant()} is fully approved (final sign-off by {myName}, {step.RoleCode}) and the TASAF is now Closed. {Flag(p)}.",
                    Link, EntityType, t.Id, emailRef: t.RefNo);
            }
        }
        else
        {
            step.Status = TasafStepStatus.Returned;
            p.Status = TasafPostEvalStatus.Returned;
            p.CurrentStepSeq = null;
            p.CurrentApproverUserId = null;
            t.Actions.Add(NewAction("Post-eval returned", t.Status, t.Status, step.RoleCode, comment, myName));
            await db.SaveChangesAsync();
            await notifications.SendAsync([t.ProponentUserId, .. signed], "Post evaluation returned",
                $"{head}: the {KindLabel(p).ToLowerInvariant()} was returned by {myName} ({step.RoleCode}): {comment}", Link, EntityType, t.Id, emailRef: t.RefNo);
        }
        if (byAdmin && step.UserId is int uid && uid != CurrentUserId)
            await notifications.SendAsync([uid], "Post evaluation signed by Admin",
                $"{head}: your {step.RoleCode} step on the post evaluation was {step.Status.ToLowerInvariant()} by {me} (Admin) on your behalf: \"{comment}\".",
                Link, EntityType, t.Id, emailRef: t.RefNo);
        return await GetAsync(tasafId);
    }

    /// <summary>Back to Returned (reason): the proponent / Admin while For Approval; an Admin once Approved (TASAF → Completed).</summary>
    public async Task<(TasafPostEvalDto? dto, string? error)> RevertAsync(int tasafId, TasafCommentDto dto)
    {
        var t = await Full().FirstOrDefaultAsync(x => x.Id == tasafId);
        if (t is null) return (null, NotFound);
        var p = t.PostEval;
        if (p is null || !CanRevert(t))
            return (null, p?.Status == TasafPostEvalStatus.Approved ? "Only an Admin can revert an approved post evaluation."
                : "Only the proponent or an Admin can revert the post evaluation, and only while it is For Approval or Approved.");
        var comment = (dto.Comment ?? "").Trim();
        if (comment.Length == 0) return (null, "Please give a reason for reverting.");
        if (comment.Length > 1000) return (null, "Reason must be 1000 characters or fewer.");

        var me = await MyNameAsync();
        var role = t.ProponentUserId == CurrentUserId ? "Proponent" : "Admin";
        var waiting = p.CurrentApproverUserId;
        var signed = p.Steps.Where(s => s.Status == TasafStepStatus.Approved).Select(s => s.UserId).ToList();
        if (Current(p) is { Status: TasafStepStatus.Pending } cur)
        {
            cur.Status = TasafStepStatus.Skipped;
            cur.Comment = $"Not reached — reverted by {role.ToLowerInvariant()}";
        }
        var from = t.Status;
        p.Status = TasafPostEvalStatus.Returned;
        p.CurrentStepSeq = null;
        p.CurrentApproverUserId = null;
        p.DecidedAt = null;
        p.UpdatedAt = DateTime.UtcNow;
        p.UpdatedBy = CurrentUser;
        if (t.Status == TasafStatus.Closed)
        {
            t.Status = TasafStatus.Completed;
            t.UpdatedAt = DateTime.UtcNow;
            t.UpdatedBy = CurrentUser;
        }
        t.Actions.Add(NewAction("Post-eval reverted", from, t.Status, role, comment, me));
        await db.SaveChangesAsync();
        await notifications.SendAsync(new List<int?> { t.ProponentUserId == CurrentUserId ? null : t.ProponentUserId, waiting }.Concat(signed).Distinct(),
            "Post evaluation reverted",
            $"{Head(t)}: the {KindLabel(p).ToLowerInvariant()} was reverted by {me} ({role}) and returned to {t.ProponentName}{(from == TasafStatus.Closed ? "; the TASAF is Completed again" : "")}: {comment}",
            Link, EntityType, t.Id, emailRef: t.RefNo);
        return await GetAsync(tasafId);
    }

    // ── Helpers ───────────────────────────────────────────────
    private static string Head(Tasaf t) => $"{t.RefNo} — {t.Title}";
    private static string KindLabel(TasafPostEval p) => p.Kind == TasafPostEvalKind.Promo ? "Promo Post Evaluation" : "Post Activity form";
    private static string N2(decimal v) => v.ToString("N2", En);

    // ── Attachments (supporting files: photos, proof of execution, receipts) ──
    /// <summary>Private blob container (account sthomsyssea via IFileStorage, Storage:ConnectionString + ContainerSuffix); blob = "{tasafId}/{guid}.ext".</summary>
    private const string FilesContainer = "tasaf-post-eval-files";
    public const long MaxAttachmentBytes = 10 * 1024 * 1024;
    private const int MaxAttachments = 20;
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04];
    private static readonly Dictionary<string, (string Type, byte[] Magic)> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ("application/pdf", "%PDF"u8.ToArray()),
        [".png"] = ("image/png", [0x89, 0x50, 0x4E, 0x47]),
        [".jpg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]),
        [".jpeg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]),
        [".xlsx"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Zip),
        [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", Zip),
        [".pptx"] = ("application/vnd.openxmlformats-officedocument.presentationml.presentation", Zip),
    };

    /// <summary>Adds a file (proponent / Admin, while Draft or Returned). Creates the post evaluation if it isn't saved yet.</summary>
    public async Task<(TasafPostEvalDto? dto, string? error)> AddAttachmentAsync(int tasafId, string fileName, Stream content, long length)
    {
        var t = await Full().FirstOrDefaultAsync(x => x.Id == tasafId);
        if (t is null) return (null, NotFound);
        if (!CanEdit(t)) return (null, EditError(t));
        if (!files.IsConfigured) return (null, "File storage is not set up on this server (Storage:ConnectionString) — ask IT.");
        if (t.PostEval is { } cur && cur.Attachments.Count >= MaxAttachments) return (null, $"At most {MaxAttachments} files per post evaluation.");
        if (length <= 0 || length > MaxAttachmentBytes) return (null, "Files must be 10 MB or smaller.");
        var ext = Path.GetExtension(fileName);
        if (!Allowed.TryGetValue(ext, out var kind)) return (null, "Only PDF, JPG, PNG, Excel (.xlsx), Word (.docx) and PowerPoint (.pptx) files are allowed.");

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer);
        var bytes = buffer.ToArray();
        if (bytes.Length < kind.Magic.Length || !bytes.AsSpan(0, kind.Magic.Length).SequenceEqual(kind.Magic))
            return (null, $"{Path.GetFileName(fileName)} is not a valid {ext.TrimStart('.').ToUpperInvariant()} file.");

        var myName = await MyNameAsync();
        var p = t.PostEval;
        if (p is null)
        {
            p = NewFrom(t);
            p.Status = TasafPostEvalStatus.Draft;
            p.CreatedByUserId = CurrentUserId;
            p.CreatedByName = myName;
            p.CreatedAt = DateTime.UtcNow;
            t.PostEval = p;
        }

        var path = $"{t.Id}/{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        await files.SaveAsync(FilesContainer, path, bytes, kind.Type);
        var name = Path.GetFileName(fileName);
        name = name.Length > 200 ? name[..200] : name;
        p.Attachments.Add(new TasafPostEvalAttachment
        {
            BlobPath = path, FileName = name, ContentType = kind.Type, Size = bytes.Length,
            UploadedByUserId = CurrentUserId, UploadedBy = myName
        });
        t.Actions.Add(NewAction("Post-eval file added", t.Status, t.Status, IsAdmin && t.ProponentUserId != CurrentUserId ? "Admin" : "Proponent", name, myName));
        try { await db.SaveChangesAsync(); }
        catch { await files.DeleteAsync(FilesContainer, path); throw; }
        return await GetAsync(tasafId);
    }

    public async Task<(TasafPostEvalDto? dto, string? error)> DeleteAttachmentAsync(int tasafId, int attachmentId)
    {
        var t = await Full().FirstOrDefaultAsync(x => x.Id == tasafId);
        if (t is null) return (null, NotFound);
        if (!CanEdit(t)) return (null, EditError(t));
        var a = t.PostEval?.Attachments.FirstOrDefault(x => x.Id == attachmentId);
        if (a is null) return (null, "File not found.");
        db.TasafPostEvalAttachments.Remove(a);
        t.Actions.Add(NewAction("Post-eval file del.", t.Status, t.Status, IsAdmin && t.ProponentUserId != CurrentUserId ? "Admin" : "Proponent", a.FileName, await MyNameAsync()));
        await db.SaveChangesAsync();
        try { await files.DeleteAsync(FilesContainer, a.BlobPath); } catch { /* the row is gone; an orphan blob is harmless */ }
        return await GetAsync(tasafId);
    }

    /// <summary>The file, for anyone who may open the post evaluation; null = not found / not allowed.</summary>
    public async Task<(Stream stream, string contentType, string fileName)?> OpenAttachmentAsync(int tasafId, int attachmentId)
    {
        var t = await db.Tasafs.AsNoTracking().Include(x => x.Steps)
            .Include(x => x.PostEval).ThenInclude(p => p!.Steps)
            .Include(x => x.PostEval).ThenInclude(p => p!.Attachments)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == tasafId);
        var a = t?.PostEval?.Attachments.FirstOrDefault(x => x.Id == attachmentId);
        if (t is null || a is null || !CanView(t)) return null;
        var file = await files.ReadAsync(FilesContainer, a.BlobPath);
        return file is null ? null : (new MemoryStream(file.Value.Data), a.ContentType, a.FileName);
    }

    /// <summary>"Within budget: actual 18,000.00 of 20,000.00" / "Over budget: …".</summary>
    private static string Flag(TasafPostEval p) => p.ActualCost is decimal a
        ? $"{(a > p.PlannedCost ? "OVER budget" : "Within budget")}: actual {N2(a)} vs approved {N2(p.PlannedCost)}"
        : $"Approved budget {N2(p.PlannedCost)}";

    private async Task<string> MyNameAsync() =>
        await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == CurrentUserId) is { } u ? FullName(u) : CurrentUser;
    private static string FullName(User u) => $"{u.FirstName} {u.LastName}".Trim();

    private TasafAction NewAction(string action, string from, string to, string role, string comment, string byName) => new()
    {
        Action = action, FromStatus = from, ToStatus = to, ByUserId = CurrentUserId, ByName = byName,
        Role = role, Comment = comment.Length > 1000 ? comment[..1000] : comment
    };

    private TasafPostEvalDto Map(Tasaf t, TasafPostEval p)
    {
        var cur = Current(p);
        return new TasafPostEvalDto
        {
            Id = p.Id, TasafId = t.Id, Kind = p.Kind, Status = p.Id == 0 ? "New" : p.Status,
            RefNo = t.RefNo, Title = t.Title, TasafStatus = t.Status,
            SalesGroupName = t.SalesGroup?.Name,
            AreaNames = Join(t.Areas.OrderBy(a => a.LineNo).Select(a => a.Area?.Name)),
            ChainNames = Join(t.Chains.OrderBy(c => c.LineNo).Select(c => c.Chain?.Name)),
            CustKeys = Join(t.Customers.OrderBy(c => c.LineNo).Take(20).Select(c => $"{c.CustKey} {c.CusName}".Trim()))
                       + (t.Customers.Count > 20 ? $" … +{t.Customers.Count - 20} more" : ""),
            CustKeyCount = t.Customers.Count,
            ProgramTypeName = Other(t.ProgramType?.Name, t.ProgramTypeOther),
            ObjectiveName = Other(t.Objective?.Name, t.ObjectiveOther),
            TargetMarketName = Other(t.TargetMarket?.Name, t.TargetMarketOther),
            ChannelNames = Join(t.Channels.OrderBy(c => c.LineNo).Select(c => c.Channel?.Name)),
            ProductCategoryNames = Join(t.ProductCategories.OrderBy(c => c.LineNo).Select(c => c.ProductGroup?.Name)),
            SegmentNames = Join(t.SubCategories.OrderBy(c => c.LineNo).Select(c => $"{c.Code} — {(c.Segment.Length > 0 ? c.Segment : c.Name)}")),
            Mechanics = t.Mechanics,
            PlannedStartDate = t.StartDate, PlannedEndDate = t.EndDate,
            ProponentName = t.ProponentName,
            TparApprovedTotal = TparApproved(t),
            ActualStartDate = p.ActualStartDate, ActualEndDate = p.ActualEndDate,
            PlannedDoors = p.PlannedDoors, ActualDoors = p.ActualDoors,
            Explanation = p.Explanation, Conclusion = p.Conclusion,
            PlannedCost = p.Id == 0 || Open(p) ? t.TotalCost : p.PlannedCost, ActualCost = p.ActualCost,
            OverBudget = p.ActualCost is decimal a ? a > (Open(p) ? t.TotalCost : p.PlannedCost) : null,
            Lines = p.Lines.OrderBy(l => l.LineNo).Select(l => new TasafPostEvalLineDto
            {
                LineNo = l.LineNo, CProdNo = l.CProdNo, Sku = l.Sku, LspPerCase = l.LspPerCase,
                PrePromoCs = l.PrePromoCs, PlannedCs = l.PlannedCs, ActualCs = l.ActualCs, PostPromoCs = l.PostPromoCs
            }).ToList(),
            ApprovalTypeName = p.ApprovalTypeName,
            Steps = p.Steps.OrderBy(s => s.Seq).Select(s => new TasafStepDto
            {
                Seq = s.Seq, RoleCode = s.RoleCode, RoleName = s.RoleName, UserName = s.UserName,
                Status = s.Status, ActedAt = s.ActedAt, Comment = s.Comment
            }).ToList(),
            CurrentRoleCode = cur?.RoleCode, CurrentApproverName = cur?.UserName,
            SubmittedAt = p.SubmittedAt, DecidedAt = p.DecidedAt,
            CreatedByName = p.CreatedByName, CreatedAt = p.Id == 0 ? null : p.CreatedAt,
            UpdatedAt = p.UpdatedAt, UpdatedBy = p.UpdatedBy,
            Attachments = p.Attachments.OrderBy(a => a.UploadedAt).ThenBy(a => a.Id).Select(a => new TasafPostEvalAttachmentDto
            {
                Id = a.Id, FileName = a.FileName, ContentType = a.ContentType, Size = a.Size, UploadedAt = a.UploadedAt, UploadedBy = a.UploadedBy
            }).ToList(),
            CanAttach = CanEdit(t),
            CanEdit = CanEdit(t), CanSubmit = CanEdit(t),
            AwaitingMe = AwaitingMe(p), CanAdminDecide = CanAdminDecide(p), CanRevert = CanRevert(t)
        };
    }

    private static string? Join(IEnumerable<string?> names)
    {
        var s = string.Join(", ", names.Where(n => !string.IsNullOrWhiteSpace(n)));
        return s.Length > 0 ? s : null;
    }

    private static string? Other(string? name, string other) =>
        name is null ? null : other.Length > 0 && name.StartsWith("Others", StringComparison.OrdinalIgnoreCase) ? $"{name}: {other}" : name;
}
