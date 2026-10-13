using System.Security.Claims;
using HOMSys.Application.DTOs.Tasaf;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace HOMSys.Application.Services;

public class TasafService(
    ITasafRepository repo,
    ITasafLookupRepository lookups,
    ISiteRepository sites,
    ICustomerRepository customers,
    IProductRepository products,
    IUserRepository users,
    NotificationService notifications,
    IHttpContextAccessor http)
{
    public const string NotFound = "TASAF not found.";
    public const string Forbidden = "You can only open your own TASAFs.";
    /// <summary>Most participating stores (Cust Keys) one TASAF can list.</summary>
    private const int MaxCustomers = 8000;

    /// <summary>Branch scopes offered after the Sites (TASAF Template DATA sheet).</summary>
    public static readonly string[] BranchScopes = ["ALL BRANCHES", "ALL LUZON", "ALL VISAYAS", "ALL MINDANAO"];

    private ClaimsPrincipal? Principal => http.HttpContext?.User;
    private string CurrentUser => Principal?.FindFirstValue(ClaimTypes.Name) ?? "system";
    private int CurrentUserId => int.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private bool CanViewAll => Principal?.HasClaim("permission", "tasaf-view-all") ?? false;
    private bool IsAdmin => Principal?.IsInRole("Admin") ?? false;

    // ── Read ──────────────────────────────────────────────────
    public async Task<TasafFormOptionsDto> GetOptionsAsync()
    {
        var dto = new TasafFormOptionsDto();
        foreach (var m in TasafMasterService.Masters)
        {
            dto.Lists[m.Key] = (await lookups.GetAllAsync(m.EntityType))
                .Where(x => x.IsActive)
                .Select(x => new TasafOptionDto
                {
                    Id = x.Id, Code = x.Code, Name = x.Name,
                    ParentId = (x as ITasafParentLookup)?.ParentId,
                    Amount   = (x as ITasafAmountLookup)?.Amount
                })
                .ToList();
        }
        dto.Branches = (await sites.GetAllAsync())
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new TasafBranchDto { SiteId = s.Id, Name = s.Name })
            .Concat(BranchScopes.Select(b => new TasafBranchDto { Name = b }))
            .ToList();
        return dto;
    }

    public async Task<List<TasafListItemDto>> GetListAsync() =>
        (await repo.GetListAsync(CanViewAll ? null : CurrentUserId))
            .Select(t => new TasafListItemDto
            {
                CurrentRoleCode     = CurrentStep(t)?.RoleCode,
                CurrentApproverName = CurrentStep(t)?.UserName,
                StepCount           = t.Steps.Count(s => s.Status != TasafStepStatus.Skipped),
                StepsDone           = t.Steps.Count(s => s.Status == TasafStepStatus.Approved),
                IsMine            = t.ProponentUserId == CurrentUserId,
                AwaitingMe        = MyAction(t) is not null,
                SubmittedAt       = t.SubmittedAt,
                Id              = t.Id,
                RefNo           = t.RefNo,
                Status          = t.Status,
                Title           = t.Title,
                SalesGroupName  = t.SalesGroup?.Name,
                ChainName       = t.Chain?.Name,
                CustomerName    = t.Customers.Count switch
                {
                    0 => null,
                    1 => t.Customers[0].CusName,
                    var n => $"{t.Customers.OrderBy(c => c.LineNo).First().CusName} +{n - 1} more"
                },
                ProgramTypeName = t.ProgramType?.Name,
                StartDate       = t.StartDate,
                EndDate         = t.EndDate,
                TotalCost       = t.TotalCost,
                ProponentName   = t.ProponentName,
                CreatedAt       = t.CreatedAt,
                UpdatedAt       = t.UpdatedAt
            })
            .ToList();

    public async Task<(TasafDto? dto, string? error)> GetByIdAsync(int id)
    {
        var t = await repo.GetByIdAsync(id);
        if (t is null) return (null, NotFound);
        if (!CanView(t)) return (null, Forbidden);
        return (Map(t), null);
    }

    /// <summary>
    /// Cust Key dropdown: active stores of the picked chain (its customer-name keywords, TASAF
    /// Setup › Customers / Chains) AND of the picked areas (their branch codes, TASAF Setup ›
    /// Areas). filtered = false when neither narrows anything (the form searches everything).
    /// </summary>
    public async Task<(List<CustomerMatchInfo> customers, bool filtered)> GetCustomerOptionsAsync(
        int? chainId, IReadOnlyList<int> areaIds)
    {
        var fragments = new List<string>();
        if (chainId is int cid && await lookups.GetByIdAsync(typeof(TasafChain), cid) is TasafChain chain)
            fragments = SplitList(chain.CustomerMatch).Where(f => f.Length >= 2).ToList();

        var scope = await AreaScopeAsync(areaIds);
        if (fragments.Count == 0 && scope is null) return ([], false);
        return (await customers.MatchAsync(fragments, scope, take: MaxCustomers), true);
    }

    /// <summary>
    /// The picked areas' branches as a customer filter; null = no filter (no area picked, or one
    /// with no branch codes, e.g. Nationwide). A customer belongs to a Site when its WhseNo is in
    /// the Site's Cuwhsenos; a Site with none (HON-B) owns its folder's unclaimed WhseNos.
    /// </summary>
    private async Task<CustomerBranchScope?> AreaScopeAsync(IReadOnlyList<int> areaIds)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in areaIds.Distinct())
        {
            if (await lookups.GetByIdAsync(typeof(TasafArea), id) is not TasafArea area) continue;
            var list = SplitList(area.BranchCodes);
            if (list.Count == 0) return null;
            codes.UnionWith(list);
        }
        if (codes.Count == 0) return null;

        var allSites = (await sites.GetAllAsync()).ToList();
        var claimed = allSites.SelectMany(s => ParseWhseNos(s.Cuwhsenos)).Distinct().ToList();
        var picked = allSites.Where(s => codes.Contains(s.Code) || codes.Contains(s.Name)).ToList();
        var whse = picked.SelectMany(s => ParseWhseNos(s.Cuwhsenos)).Distinct().ToList();
        var folders = picked.Where(s => ParseWhseNos(s.Cuwhsenos).Count == 0)
            .Select(s => s.Code.Split('-')[0].ToLowerInvariant()).Distinct().ToList();
        return new CustomerBranchScope(whse, folders, claimed);
    }

    private static List<string> SplitList(string s) =>
        s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static List<int> ParseWhseNos(string cuwhsenos) =>
        SplitList(cuwhsenos).Select(x => int.TryParse(x, out var n) ? n : -1).Where(n => n >= 0).ToList();

    /// <summary>
    /// Participating SKU dropdown: active products (not Delisted / Phased Out) of the TASAF's
    /// product scope. Each (group, category) pair contributes its category's SKUs when a
    /// category is picked, else the whole group's. TASAF codes map to the product master:
    /// category "1A" = ProductCategory "1a", group / whole-group category "07" = GroupNo 7.
    /// A non-numeric group code (CORP — Total RAM) or no scope at all = every active product.
    /// </summary>
    public async Task<List<TasafProductOptionDto>> GetProductOptionsAsync(
        int? productGroupId, int? categoryId, int? productGroup2Id, int? category2Id)
    {
        var catalog = (await products.GetCatalogAsync())
            .Where(p => !string.Equals(p.GroupDesc?.Trim(), ProductCatalogService.Delisted, StringComparison.OrdinalIgnoreCase)
                        && !p.PhOut)
            .ToList();

        var codes = new List<string>();
        foreach (var (groupId, catId) in new[] { (productGroupId, categoryId), (productGroup2Id, category2Id) })
        {
            var cat = catId is int c ? await lookups.GetByIdAsync(typeof(TasafCategory), c) : null;
            var grp = groupId is int g ? await lookups.GetByIdAsync(typeof(TasafProductGroup), g) : null;
            var code = (cat ?? grp)?.Code.Trim();
            if (!string.IsNullOrEmpty(code)) codes.Add(code);
        }

        IEnumerable<HOMSys.Application.DTOs.Products.ProductCatalogDto> picked = catalog;
        if (codes.Count > 0 && !codes.Any(c => !char.IsDigit(c[0])))  // any CORP-like code = all products
        {
            var categoryCodes = codes.Where(c => c.Any(char.IsLetter)).Select(c => c.ToLowerInvariant()).ToHashSet();
            var groupNos = codes.Where(c => c.All(char.IsDigit)).Select(int.Parse).ToHashSet();
            picked = catalog.Where(p => categoryCodes.Contains(p.Category.Trim().ToLowerInvariant()) || groupNos.Contains(p.GroupNo));
        }

        return picked
            .OrderBy(p => p.GroupNo).ThenBy(p => p.SeqNo).ThenBy(p => p.CProdNo)
            .Select(p => new TasafProductOptionDto
            {
                CProdNo      = p.CProdNo.Trim(),
                ProdDesc     = p.ProdDesc.Trim(),
                PackSize     = p.PackSize.Trim(),
                Category     = p.Category.Trim(),
                CategoryName = p.GroupDesc?.Trim(),
                LspPerCase   = p.NewPrice
            })
            .ToList();
    }

    public async Task<TasafProductLookupDto?> LookupProductAsync(string cProdNo)
    {
        var p = await products.GetByCProdNoAsync(cProdNo.Trim());
        return p is null ? null : new TasafProductLookupDto
        {
            CProdNo = p.CProdNo, ProdDesc = p.ProdDesc, PackSize = p.PackSize, Pieces = p.Pieces, LspPerCase = p.NewPrice
        };
    }

    // ── Write ─────────────────────────────────────────────────
    public async Task<(TasafDto? dto, string? error)> CreateAsync(SaveTasafDto dto)
    {
        var user = await users.GetByIdAsync(CurrentUserId);
        if (user is null) return (null, "Your user account was not found.");

        var t = new Tasaf
        {
            Status          = TasafStatus.Draft,
            ProponentUserId = user.Id,
            ProponentName   = $"{user.FirstName} {user.LastName}".Trim(),
            CreatedBy       = CurrentUser
        };
        var error = await ApplyAsync(t, dto);
        if (error is not null) return (null, error);

        await repo.CreateAsync(t);
        return (Map((await repo.GetByIdAsync(t.Id))!), null);
    }

    public async Task<(TasafDto? dto, string? error)> UpdateAsync(int id, SaveTasafDto dto)
    {
        var t = await repo.GetByIdAsync(id);
        if (t is null) return (null, NotFound);
        if (!CanEdit(t)) return (null, "Only the proponent can edit this TASAF, and only while it is a Draft or Returned.");

        var error = await ApplyAsync(t, dto);
        if (error is not null) return (null, error);

        t.UpdatedAt = DateTime.UtcNow;
        t.UpdatedBy = CurrentUser;
        await repo.UpdateAsync(t);
        return (Map((await repo.GetByIdAsync(id))!), null);
    }

    public async Task<string?> DeleteAsync(int id)
    {
        var t = await repo.GetByIdAsync(id);
        if (t is null) return NotFound;
        if (!CanDelete(t)) return t.Status is TasafStatus.Approved or TasafStatus.Cancelled
            ? $"{t.Status} TASAFs are kept for the record and cannot be deleted."
            : "Only an Admin can delete a TASAF.";

        await repo.DeleteAsync(t);
        return null;
    }

    private bool CanView(Tasaf t) =>
        CanViewAll || t.ProponentUserId == CurrentUserId || t.Steps.Any(s => s.UserId == CurrentUserId);
    private bool CanEdit(Tasaf t) =>
        t.Status is TasafStatus.Draft or TasafStatus.Returned && t.ProponentUserId == CurrentUserId;
    private bool CanDelete(Tasaf t) =>
        IsAdmin && t.Status is not (TasafStatus.Approved or TasafStatus.Cancelled); // kept for the record
    private bool CanRevert(Tasaf t) =>
        IsAdmin && t.Status is (TasafStatus.Approved or TasafStatus.Rejected or TasafStatus.ForApproval);
    private bool CanCancel(Tasaf t) =>
        (IsAdmin || t.ProponentUserId == CurrentUserId)
        && t.Status is not (TasafStatus.Approved or TasafStatus.Cancelled);

    /// <summary>"approve" when the TASAF's current step is waiting on the caller, else null.</summary>
    private string? MyAction(Tasaf t) =>
        t.Status == TasafStatus.ForApproval && t.CurrentApproverUserId == CurrentUserId ? "approve" : null;

    private static TasafApprovalStep? CurrentStep(Tasaf t) =>
        t.CurrentStepSeq is int seq ? t.Steps.FirstOrDefault(s => s.Seq == seq) : null;

    // ── Workflow ──────────────────────────────────────────────
    public async Task<(TasafDto? dto, string? error)> SubmitAsync(int id)
    {
        var t = await repo.GetByIdAsync(id);
        if (t is null) return (null, NotFound);
        if (!CanEdit(t)) return (null, "Only the proponent can submit this TASAF, and only while it is a Draft or Returned.");

        var missing = MissingForSubmit(t);
        if (missing.Count > 0) return (null, "Complete these before submitting: " + string.Join(", ", missing) + ".");

        // Mode of Payment: ONE payment type per TASAF, carrying the whole Total Cost.
        var payTypes = new[] { t.PayInvDeductionAp + t.PayInvDeductionSalesman, t.PayCheckAp + t.PayCheckSalesman,
                               t.PayFreeGoodsAp + t.PayFreeGoodsSalesman, t.PayOffInvoiceAp + t.PayOffInvoiceSalesman }
            .Count(a => a != 0);
        if (payTypes == 0) return (null, "Select the Mode of Payment (payment type).");
        if (payTypes > 1) return (null, "Only one payment type is allowed per TASAF.");
        var payTotal = PaymentTotal(t);
        if (payTotal != t.TotalCost)
            return (null, $"Mode of Payment total ({payTotal:N2}) must equal Total Cost ({t.TotalCost:N2}).");

        // CTS ceiling per program type (promo packs: 8%).
        var programType = (TasafProgramType)t.ProgramType!;
        if (programType.MaxCtsPercent is decimal maxCts)
        {
            if (t.PromotedValue is not > 0)
                return (null, $"{programType.Name} needs a Promoted Value so CTS can be checked against the {maxCts:0.##}% limit.");
            // Over the limit is allowed only with a written justification (shown to the approvers).
            var cts = t.TotalCost / t.PromotedValue.Value * 100m;
            if (cts > maxCts && t.CtsJustification.Length == 0)
                return (null, $"CTS is {cts:0.##}% — over the {maxCts:0.##}% limit for {programType.Name}. Enter the CTS justification to submit.");
        }

        // Build the chain from the program type's approval matrix (Promo / Near Ex, or
        // Account Support): every active role with a rule for that type that is either
        // "always" or whose amount the total is MORE THAN. Vacant = skipped.
        if (programType.ApprovalTypeId is not int approvalTypeId)
            return (null, $"Program type {programType.Name} has no Approval Type. Ask an admin to set it in TASAF Setup.");
        var approvalType = await lookups.GetByIdAsync(typeof(TasafApprovalType), approvalTypeId);
        var rules = (await lookups.GetRulesAsync())
            .Where(r => r.ApprovalTypeId == approvalTypeId)
            .ToDictionary(r => r.RoleId, r => r.OverAmount);

        var sg = (TasafSalesGroup)t.SalesGroup!;
        var matrix = (await lookups.GetApproversAsync())
            .Where(a => a.SalesGroupId == sg.Id)
            .ToDictionary(a => a.RoleId, a => a.UserId);
        var roles = (await lookups.GetAllAsync(typeof(TasafApprovalRole)))
            .Where(r => r.IsActive && rules.TryGetValue(r.Id, out var over) && (over is null || t.TotalCost > over))
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Id)
            .ToList();
        if (roles.Count == 0)
            return (null, $"No approval roles apply to {approvalType?.Name}. Ask an admin to check Approval Rules in TASAF Setup.");

        var steps = new List<TasafApprovalStep>();
        foreach (var r in roles)
        {
            var user = matrix.GetValueOrDefault(r.Id) is int uid ? await users.GetByIdAsync(uid) : null;
            var vacant = user is null || !user.IsActive;
            steps.Add(new TasafApprovalStep
            {
                Seq      = steps.Count + 1,
                RoleId   = r.Id,
                RoleCode = r.Code.Length > 0 ? r.Code : r.Name,
                RoleName = r.Name,
                UserId   = vacant ? null : user!.Id,
                UserName = vacant ? "" : FullName(user!),
                Status   = vacant ? TasafStepStatus.Skipped : TasafStepStatus.Pending,
                Comment  = vacant ? "Vacant — skipped" : ""
            });
        }
        var first = steps.FirstOrDefault(s => s.Status == TasafStepStatus.Pending);
        if (first is null)
            return (null, $"No approvers are assigned for {sg.Name} ({string.Join(", ", steps.Select(s => s.RoleCode))}). Ask an admin to fill in the Approval Matrix in TASAF Setup.");

        var resubmit = t.Status == TasafStatus.Returned;
        var from = t.Status;
        t.Steps.Clear();
        t.Steps.AddRange(steps);
        t.Status                = TasafStatus.ForApproval;
        t.SubmittedAt           = DateTime.UtcNow;
        t.ApprovalTypeName      = approvalType?.Name ?? "";
        t.CurrentStepSeq        = first.Seq;
        t.CurrentApproverUserId = first.UserId;
        t.DecidedAt             = null;
        t.Actions.Add(NewAction(resubmit ? "Resubmitted" : "Submitted", from, t.Status, "KAM", "", t.ProponentName));

        // RefNo series: YEAR - Sales Group - Proponent Initials - NN (GUIDE sheet).
        // Assigned once (first submit) and kept through returns/resubmits. NN = one continuous
        // series across all TASAFs (never resets, no 99 cap) — see TasafRepository.SubmitAsync.
        var proponent = await users.GetByIdAsync(t.ProponentUserId);
        var initials = Initials(proponent?.FirstName ?? "", proponent?.LastName ?? "", t.ProponentName);
        var prefix = $"{TodayPh().Year}-{(sg.Code.Length > 0 ? sg.Code : sg.Name).ToUpperInvariant()}-{initials}-";
        await repo.SubmitAsync(t, prefix);

        await notifications.SendAsync([first.UserId],
            "TASAF for your approval",
            $"{t.RefNo} — {t.Title} ({t.TotalCost:N2}) from {t.ProponentName}{(resubmit ? ", resubmitted" : "")}. You sign as {first.RoleCode}.",
            "/tasafs", "tasaf", t.Id);

        return (Map((await repo.GetByIdAsync(id))!), null);
    }

    public async Task<(TasafDto? dto, string? error)> DecideAsync(int id, TasafDecisionDto dto)
    {
        var t = await repo.GetByIdAsync(id);
        if (t is null) return (null, NotFound);

        var step = CurrentStep(t);
        if (MyAction(t) is null || step is null) return (null, "This TASAF is not waiting for your action.");

        var decision = dto.Decision.Trim().ToLowerInvariant();
        var comment = dto.Comment.Trim();
        if (decision is not ("approve" or "return" or "reject")) return (null, "Unknown decision.");
        if (decision != "approve" && comment.Length == 0) return (null, "Please give a reason when returning or rejecting.");
        if (comment.Length > 1000) return (null, "Comment must be 1000 characters or fewer.");

        var from = t.Status;
        var myName = step.UserName.Length > 0 ? step.UserName : CurrentUser;
        var head = $"{t.RefNo} — {t.Title}";
        // Everyone who already signed, for return/reject/final notices.
        var signed = t.Steps.Where(s => s.Status == TasafStepStatus.Approved).Select(s => s.UserId).ToList();

        step.ActedAt = DateTime.UtcNow;
        step.Comment = comment;

        switch (decision)
        {
            case "approve":
                step.Status = TasafStepStatus.Approved;
                var next = t.Steps.Where(s => s.Seq > step.Seq && s.Status == TasafStepStatus.Pending).OrderBy(s => s.Seq).FirstOrDefault();
                if (next is not null)
                {
                    t.CurrentStepSeq = next.Seq;
                    t.CurrentApproverUserId = next.UserId;
                    t.Actions.Add(NewAction("Approved", from, t.Status, step.RoleCode, comment, myName));
                    await repo.UpdateAsync(t);
                    await notifications.SendAsync([next.UserId], "TASAF for your approval",
                        $"{head} ({t.TotalCost:N2}) from {t.ProponentName}, approved by {myName} ({step.RoleCode}). You sign as {next.RoleCode}.",
                        "/tasafs", "tasaf", t.Id);
                    await notifications.SendAsync([t.ProponentUserId], "TASAF approval progress",
                        $"{head} was approved by {myName} ({step.RoleCode}) and is now with {next.UserName} ({next.RoleCode}).",
                        "/tasafs", "tasaf", t.Id);
                }
                else
                {
                    t.Status = TasafStatus.Approved;
                    t.CurrentStepSeq = null;
                    t.CurrentApproverUserId = null;
                    t.DecidedAt = DateTime.UtcNow;
                    t.Actions.Add(NewAction("Approved", from, t.Status, step.RoleCode, comment, myName));
                    await repo.UpdateAsync(t);
                    await notifications.SendAsync([t.ProponentUserId, .. signed], "TASAF approved",
                        $"{head} is fully approved (final sign-off by {myName}, {step.RoleCode}).", "/tasafs", "tasaf", t.Id);
                }
                break;

            case "return":
                step.Status = TasafStepStatus.Returned;
                t.Status = TasafStatus.Returned;
                t.CurrentStepSeq = null;
                t.CurrentApproverUserId = null;
                t.Actions.Add(NewAction("Returned", from, t.Status, step.RoleCode, comment, myName));
                await repo.UpdateAsync(t);
                await notifications.SendAsync([t.ProponentUserId, .. signed], "TASAF returned",
                    $"{head} was returned by {myName} ({step.RoleCode}): {comment}", "/tasafs", "tasaf", t.Id);
                break;

            default: // reject
                step.Status = TasafStepStatus.Rejected;
                t.Status = TasafStatus.Rejected;
                t.CurrentStepSeq = null;
                t.CurrentApproverUserId = null;
                t.DecidedAt = DateTime.UtcNow;
                t.Actions.Add(NewAction("Rejected", from, t.Status, step.RoleCode, comment, myName));
                await repo.UpdateAsync(t);
                await notifications.SendAsync([t.ProponentUserId, .. signed], "TASAF rejected",
                    $"{head} was rejected by {myName} ({step.RoleCode}): {comment}", "/tasafs", "tasaf", t.Id);
                break;
        }

        return (Map((await repo.GetByIdAsync(id))!), null);
    }

    /// <summary>
    /// Admin: undo an approval mistake — an Approved, Rejected or in-progress TASAF goes back
    /// to the proponent as Returned (they can fix + resubmit, or cancel). Signed steps stay as
    /// history; resubmit rebuilds the chain.
    /// </summary>
    public async Task<(TasafDto? dto, string? error)> RevertAsync(int id, TasafCommentDto dto)
    {
        var t = await repo.GetByIdAsync(id);
        if (t is null) return (null, NotFound);
        if (!CanRevert(t)) return (null, "Only an Admin can revert a TASAF, and only while it is For Approval, Approved or Rejected.");
        var (comment, error) = Reason(dto, "reverting");
        if (error is not null) return (null, error);

        var from = t.Status;
        var waiting = t.CurrentApproverUserId;
        var signed = t.Steps.Where(s => s.Status == TasafStepStatus.Approved).Select(s => s.UserId);
        t.Status                = TasafStatus.Returned;
        t.CurrentStepSeq        = null;
        t.CurrentApproverUserId = null;
        t.DecidedAt             = null;
        t.UpdatedAt             = DateTime.UtcNow;
        t.UpdatedBy             = CurrentUser;
        t.Actions.Add(NewAction("Reverted", from, t.Status, "Admin", comment, await MyNameAsync()));
        await repo.UpdateAsync(t);

        await notifications.SendAsync([t.ProponentUserId, waiting, .. signed], "TASAF reverted",
            $"{t.RefNo} — {t.Title} ({from}) was reverted by an Admin and returned to {t.ProponentName}: {comment}",
            "/tasafs", "tasaf", t.Id);
        return (Map((await repo.GetByIdAsync(id))!), null);
    }

    /// <summary>
    /// Proponent (or Admin) withdraws the TASAF. Kept for the record — never deleted.
    /// Approved ones must be reverted by an Admin first.
    /// </summary>
    public async Task<(TasafDto? dto, string? error)> CancelAsync(int id, TasafCommentDto dto)
    {
        var t = await repo.GetByIdAsync(id);
        if (t is null) return (null, NotFound);
        if (!CanCancel(t))
            return (null, t.Status == TasafStatus.Approved
                ? "An Approved TASAF must be reverted by an Admin before it can be cancelled."
                : t.Status == TasafStatus.Cancelled ? "This TASAF is already cancelled."
                : "Only the proponent or an Admin can cancel this TASAF.");
        var (comment, error) = Reason(dto, "cancelling");
        if (error is not null) return (null, error);

        var from = t.Status;
        var waiting = t.CurrentApproverUserId;
        var signed = t.Steps.Where(s => s.Status == TasafStepStatus.Approved).Select(s => s.UserId);
        // Unsigned steps will never be acted on now.
        foreach (var s in t.Steps.Where(s => s.Status == TasafStepStatus.Pending))
        {
            s.Status  = TasafStepStatus.Skipped;
            s.Comment = "Not reached — cancelled";
        }
        t.Status                = TasafStatus.Cancelled;
        t.CurrentStepSeq        = null;
        t.CurrentApproverUserId = null;
        t.DecidedAt             = DateTime.UtcNow;
        t.UpdatedAt             = DateTime.UtcNow;
        t.UpdatedBy             = CurrentUser;
        var byProponent = t.ProponentUserId == CurrentUserId;
        t.Actions.Add(NewAction("Cancelled", from, t.Status, byProponent ? "KAM" : "Admin", comment,
            byProponent ? t.ProponentName : await MyNameAsync()));
        await repo.UpdateAsync(t);

        await notifications.SendAsync([byProponent ? null : t.ProponentUserId, waiting, .. signed], "TASAF cancelled",
            $"{t.RefNo ?? "(draft)"} — {t.Title} was cancelled by {(byProponent ? t.ProponentName : "an Admin")}: {comment}",
            "/tasafs", "tasaf", t.Id);
        return (Map((await repo.GetByIdAsync(id))!), null);
    }

    private static (string comment, string? error) Reason(TasafCommentDto dto, string verb)
    {
        var c = (dto.Comment ?? "").Trim();
        if (c.Length == 0) return (c, $"Please give a reason for {verb}.");
        if (c.Length > 1000) return (c, "Reason must be 1000 characters or fewer.");
        return (c, null);
    }

    private async Task<string> MyNameAsync() =>
        await users.GetByIdAsync(CurrentUserId) is { } u ? FullName(u) : CurrentUser;

    private static List<string> MissingForSubmit(Tasaf t)
    {
        var m = new List<string>();
        if (t.StartDate is null) m.Add("Program start date");
        if (t.EndDate is null) m.Add("Program end date");
        if (t.SalesGroupId is null) m.Add("Sales Group");
        if (t.Areas.Count == 0) m.Add("Area");
        if (t.ChainId is null) m.Add("Customer / Chain");
        if (t.Customers.Count == 0) m.Add("Cust Key");
        if (t.Branches.Count == 0) m.Add("Branch");
        if (t.ProgramTypeId is null) m.Add("Program Type");
        else if (IsOthers(t.ProgramType) && t.ProgramTypeOther.Length == 0) m.Add("Program Type (Others, indicate)");
        if (t.ObjectiveId is null) m.Add("Objective");
        else if (IsOthers(t.Objective) && t.ObjectiveOther.Length == 0) m.Add("Objective (Others, indicate)");
        if (t.TargetMarketId is null) m.Add("Target Market");
        else if (IsOthers(t.TargetMarket) && t.TargetMarketOther.Length == 0) m.Add("Target Market (Others, indicate)");
        if (t.Channels.Count == 0) m.Add("Channel");
        if (t.ProductGroupId is null) m.Add("Product Group");
        if (t.CategoryId is null) m.Add("Category");
        if (t.Background.Length == 0) m.Add("Background");
        if (t.Concessions.Length == 0) m.Add("Concessions");
        if (t.Mechanics.Length == 0) m.Add("Program Mechanics");
        // Participating Products (optional section): every listed row needs every column but SPLY.
        var badRows = t.Products.OrderBy(p => p.LineNo)
            .Where(p => p.CProdNo.Length == 0 || p.Sku.Length == 0 || p.P3MoAvgCs is null
                        || p.PromotedVolCs <= 0 || p.LspPerCase <= 0)
            .Select(p => p.LineNo).ToList();
        if (badRows.Count > 0)
            m.Add($"Participating Products row {string.Join(", ", badRows)} (ProdNo, SKU, P3 Mo Avg, Promoted Vol, LSP / Case)");
        if (t.Costs.Count == 0 || t.TotalCost <= 0) m.Add("Cost Details");
        else if (t.Costs.Any(c => c.CostTypeId is null)) m.Add("a description on every cost line");
        return m;
    }

    /// <summary>ACCOUNT SUPPORT- (Anniversary / Opening / Sponsorship) and Convention program types.</summary>
    public static bool IsSupportProgram(string programTypeName)
    {
        var n = programTypeName.Trim();
        return n.StartsWith("ACCOUNT SUPPORT", StringComparison.OrdinalIgnoreCase)
            || n.Equals("Convention", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOthers(TasafLookup? l) => l?.Name.StartsWith("Others", StringComparison.OrdinalIgnoreCase) ?? false;

    private static decimal PaymentTotal(Tasaf t) =>
        t.PayInvDeductionAp + t.PayInvDeductionSalesman + t.PayCheckAp + t.PayCheckSalesman
        + t.PayFreeGoodsAp + t.PayFreeGoodsSalesman + t.PayOffInvoiceAp + t.PayOffInvoiceSalesman;

    private TasafAction NewAction(string action, string from, string to, string role, string comment, string byName) => new()
    {
        Action     = action,
        FromStatus = from,
        ToStatus   = to,
        ByUserId   = CurrentUserId,
        ByName     = byName,
        Role       = role,
        Comment    = comment
    };

    private static string FullName(User u) => $"{u.FirstName} {u.LastName}".Trim();

    /// <summary>
    /// RefNo initials (GUIDE sheet): first letter of the first name + first letter of the
    /// surname — "Elwin Ramos" → "ER", "Ma. Teresa Araneta" → "MA", "Juan Dela Cruz Jr." → "JD".
    /// Falls back to the display name when the user record has no first/last name.
    /// </summary>
    private static string Initials(string firstName, string lastName, string fallbackName)
    {
        static char? Lead(string s) => s.FirstOrDefault(char.IsLetter) is var c && c != default ? char.ToUpperInvariant(c) : null;
        var f = Lead(firstName);
        var l = Lead(lastName);
        if (f is not null && l is not null) return $"{f}{l}";

        var words = fallbackName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length switch
        {
            0 => "XX",
            1 => new string(words[0].Where(char.IsLetter).Take(2).ToArray()).ToUpperInvariant().PadRight(2, 'X'),
            _ => $"{Lead(words[0])}{Lead(words[^1])}"
        };
    }

    private static DateTime TodayPh() => DateTime.UtcNow.AddHours(8);

    /// <summary>Validates the body and copies it onto t, recomputing every derived amount.</summary>
    private async Task<string?> ApplyAsync(Tasaf t, SaveTasafDto dto)
    {
        dto.Title = dto.Title.Trim();
        if (dto.Title.Length == 0) return "Activity Title is required.";
        if (dto.Title.Length > 200) return "Activity Title must be 200 characters or fewer.";

        foreach (var (d, label) in new[] { (dto.StartDate, "Start"), (dto.EndDate, "End") })
            if (d is { Year: < 2000 or > 2100 }) return $"{label} date is invalid.";
        if (dto is { StartDate: DateOnly start, EndDate: DateOnly end } && end < start)
            return "End date cannot be before the start date.";

        // Every picked lookup must exist (inactive is allowed, so an older draft still saves).
        var checks = new (int? id, string key, string label)[]
        {
            (dto.SalesGroupId, "sales-groups", "Sales Group"),
            (dto.ChainId, "chains", "Customer / Chain"), (dto.ProgramTypeId, "program-types", "Program Type"),
            (dto.ObjectiveId, "objectives", "Objective"), (dto.TargetMarketId, "target-markets", "Target Market"),
            (dto.ProductGroupId, "product-groups", "Product Group"),
            (dto.ProductGroup2Id, "product-groups", "Secondary Product Group"), (dto.CategoryId, "categories", "Category"),
            (dto.Category2Id, "categories", "Secondary Category"),
        };
        foreach (var (lookupId, key, label) in checks)
            if (lookupId is int lid && !await LookupExistsAsync(key, lid))
                return $"{label} not found.";

        // Account Support & Convention: no products, no pre-promo / incremental sales
        // (Promoted Value is entered as the Annual Projected Sales).
        if (dto.ProgramTypeId is int ptId && await lookups.GetByIdAsync(typeof(TasafProgramType), ptId) is TasafLookup pt
            && IsSupportProgram(pt.Name))
        {
            dto.Products = [];
            dto.PrePromoSales = null;
            dto.IncrementalSales = null;
        }

        var areaIds = (dto.AreaIds ?? []).Distinct().ToList();
        foreach (var aid in areaIds)
            if (!await LookupExistsAsync("areas", aid)) return "Area not found.";
        var channelIds = (dto.ChannelIds ?? []).Distinct().ToList();
        foreach (var cid in channelIds)
            if (!await LookupExistsAsync("channels", cid)) return "Channel not found.";

        var costTypeIds = dto.Costs.Where(c => c.CostTypeId is not null).Select(c => c.CostTypeId!.Value).Distinct();
        foreach (var ct in costTypeIds)
            if (!await LookupExistsAsync("cost-types", ct)) return "Cost description not found.";

        // Participating stores: every Cust Key must exist; names come from the customer master.
        var custKeys = dto.Customers.Select(c => (c.CustKey ?? "").Trim()).Where(k => k.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (custKeys.Count > MaxCustomers) return $"Pick at most {MaxCustomers} stores.";
        var found = (await customers.GetByCustKeysAsync(custKeys)).Values
            .GroupBy(c => c.CustKey.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var missing = custKeys.Where(k => !found.ContainsKey(k)).ToList();
        if (missing.Count > 0) return $"Customer {string.Join(", ", missing.Take(5))} not found.";

        var amounts = new[] { dto.PromotedValue, dto.PrePromoSales, dto.IncrementalSales,
            dto.PayInvDeductionAp, dto.PayInvDeductionSalesman, dto.PayCheckAp, dto.PayCheckSalesman,
            dto.PayFreeGoodsAp, dto.PayFreeGoodsSalesman, dto.PayOffInvoiceAp, dto.PayOffInvoiceSalesman };
        if (amounts.Any(a => a is < 0)
            || dto.Costs.Any(c => c.Amount < 0)
            || dto.Products.Any(p => p.P3MoAvgCs < 0 || p.SplyCs < 0 || p.PromotedVolCs < 0 || p.LspPerCase < 0))
            return "Amounts and volumes cannot be negative.";

        // Header
        t.Title             = dto.Title;
        t.StartDate         = dto.StartDate;
        t.EndDate           = dto.EndDate;
        t.SalesGroupId      = dto.SalesGroupId;
        t.ChainId           = dto.ChainId;
        t.ProgramTypeId     = dto.ProgramTypeId;
        t.ProgramTypeOther  = dto.ProgramTypeOther.Trim();
        t.ObjectiveId       = dto.ObjectiveId;
        t.ObjectiveOther    = dto.ObjectiveOther.Trim();
        t.TargetMarketId    = dto.TargetMarketId;
        t.TargetMarketOther = dto.TargetMarketOther.Trim();
        t.ProductGroupId    = dto.ProductGroupId;
        t.ProductGroup2Id   = dto.ProductGroup2Id;
        t.CategoryId        = dto.CategoryId;
        t.Category2Id       = dto.Category2Id;
        t.Background        = dto.Background.Trim();
        t.Concessions       = dto.Concessions.Trim();
        t.Mechanics         = dto.Mechanics.Trim();
        t.CtsJustification  = (dto.CtsJustification ?? "").Trim() is var just && just.Length > 1000 ? just[..1000] : just;
        t.PrePromoSales     = dto.PrePromoSales;
        t.PayInvDeductionAp       = dto.PayInvDeductionAp;
        t.PayInvDeductionSalesman = dto.PayInvDeductionSalesman;
        t.PayCheckAp              = dto.PayCheckAp;
        t.PayCheckSalesman        = dto.PayCheckSalesman;
        t.PayFreeGoodsAp          = dto.PayFreeGoodsAp;
        t.PayFreeGoodsSalesman    = dto.PayFreeGoodsSalesman;
        t.PayOffInvoiceAp         = dto.PayOffInvoiceAp;
        t.PayOffInvoiceSalesman   = dto.PayOffInvoiceSalesman;

        // Lines — replaced wholesale; blank rows dropped. Removed children are
        // orphan-deleted by EF (required FK, cascade).
        t.Areas.Clear();
        var line = 0;
        foreach (var aid in areaIds)
            t.Areas.Add(new TasafAreaLink { LineNo = ++line, AreaId = aid });

        t.Channels.Clear();
        line = 0;
        foreach (var cid in channelIds)
            t.Channels.Add(new TasafChannelLink { LineNo = ++line, ChannelId = cid });

        t.Branches.Clear();
        line = 0;
        foreach (var b in dto.Branches.Where(b => b.SiteId is not null || !string.IsNullOrWhiteSpace(b.Name))
                     .DistinctBy(b => (b.SiteId, b.Name.Trim())))
            t.Branches.Add(new TasafBranch { LineNo = ++line, SiteId = b.SiteId, Name = b.Name.Trim() });

        t.Customers.Clear();
        line = 0;
        foreach (var k in custKeys)
        {
            var c = found[k];
            t.Customers.Add(new TasafCustomer { LineNo = ++line, CustKey = c.CustKey.Trim(), CusName = c.CusName.Trim() is var n && n.Length > 150 ? n[..150] : n });
        }

        t.Products.Clear();
        line = 0;
        foreach (var p in dto.Products.Where(p => !string.IsNullOrWhiteSpace(p.CProdNo) || !string.IsNullOrWhiteSpace(p.Sku)))
            t.Products.Add(new TasafProduct
            {
                LineNo        = ++line,
                CProdNo       = p.CProdNo.Trim(),
                Sku           = p.Sku.Trim(),
                P3MoAvgCs     = p.P3MoAvgCs,
                SplyCs        = p.SplyCs,
                PromotedVolCs = p.PromotedVolCs,
                IncrVolCs     = p.IncrVolCs,
                LspPerCase    = p.LspPerCase,
                PromotedValue = Math.Round(p.PromotedVolCs * p.LspPerCase, 2)
            });

        t.Costs.Clear();
        line = 0;
        foreach (var c in dto.Costs.Where(c => c.CostTypeId is not null || c.Amount != 0 || !string.IsNullOrWhiteSpace(c.Remarks)))
            t.Costs.Add(new TasafCost { LineNo = ++line, CostTypeId = c.CostTypeId, Amount = c.Amount, Remarks = c.Remarks.Trim() });

        // Computation — from the product table when it is filled, else as entered.
        t.TotalCost = t.Costs.Sum(c => c.Amount);
        if (t.Products.Count > 0)
        {
            t.PromotedValue    = t.Products.Sum(p => p.PromotedValue);
            t.IncrementalSales = Math.Round(t.Products.Sum(p => p.IncrVolCs * p.LspPerCase), 2);
            // Pre-promo monthly avg = Σ P3Mo Avg (cs) × LSP; a seasonal SKU with no P3M uses its SPLY.
            t.PrePromoSales    = Math.Round(t.Products.Sum(p => (p.P3MoAvgCs ?? p.SplyCs ?? 0) * p.LspPerCase), 2);
        }
        else
        {
            t.PromotedValue    = dto.PromotedValue;
            t.IncrementalSales = dto.IncrementalSales;
        }
        return null;
    }

    private async Task<bool> LookupExistsAsync(string key, int id)
    {
        var def = TasafMasterService.Masters.First(m => m.Key == key);
        return await lookups.GetByIdAsync(def.EntityType, id) is not null;
    }

    private static decimal? Ratio(decimal? num, decimal? den) =>
        num is decimal n && den is decimal d && d != 0 ? Math.Round(n / d, 4) : null;

    private TasafDto Map(Tasaf t)
    {
        var payTotal = t.PayInvDeductionAp + t.PayInvDeductionSalesman + t.PayCheckAp + t.PayCheckSalesman
                     + t.PayFreeGoodsAp + t.PayFreeGoodsSalesman + t.PayOffInvoiceAp + t.PayOffInvoiceSalesman;
        return new TasafDto
        {
            Id                = t.Id,
            RefNo             = t.RefNo,
            Status            = t.Status,
            Title             = t.Title,
            StartDate         = t.StartDate,
            EndDate           = t.EndDate,
            SalesGroupId      = t.SalesGroupId,
            SalesGroupName    = t.SalesGroup?.Name,
            AreaIds           = t.Areas.OrderBy(a => a.LineNo).Select(a => a.AreaId).ToList(),
            AreaNames         = string.Join(", ", t.Areas.OrderBy(a => a.LineNo).Select(a => a.Area?.Name).Where(n => n is not null)),
            ChainId           = t.ChainId,
            ChainName         = t.Chain?.Name,
            ProgramTypeId     = t.ProgramTypeId,
            ProgramTypeName   = t.ProgramType?.Name,
            ProgramTypeOther  = t.ProgramTypeOther,
            ObjectiveId       = t.ObjectiveId,
            ObjectiveName     = t.Objective?.Name,
            ObjectiveOther    = t.ObjectiveOther,
            TargetMarketId    = t.TargetMarketId,
            TargetMarketName  = t.TargetMarket?.Name,
            TargetMarketOther = t.TargetMarketOther,
            ChannelIds        = t.Channels.OrderBy(c => c.LineNo).Select(c => c.ChannelId).ToList(),
            ChannelNames      = string.Join(", ", t.Channels.OrderBy(c => c.LineNo).Select(c => c.Channel?.Name).Where(n => n is not null)),
            ProductGroupId    = t.ProductGroupId,
            ProductGroupName  = t.ProductGroup?.Name,
            ProductGroup2Id   = t.ProductGroup2Id,
            ProductGroup2Name = t.ProductGroup2?.Name,
            CategoryId        = t.CategoryId,
            CategoryName      = t.Category?.Name,
            Category2Id       = t.Category2Id,
            Category2Name     = t.Category2?.Name,
            Background        = t.Background,
            Concessions       = t.Concessions,
            Mechanics         = t.Mechanics,
            CtsJustification  = t.CtsJustification,
            TotalCost         = t.TotalCost,
            PromotedValue     = t.PromotedValue,
            PrePromoSales     = t.PrePromoSales,
            IncrementalSales  = t.IncrementalSales,
            PromotedVsPrePromo    = Ratio(t.PromotedValue, t.PrePromoSales),
            IncrementalVsPrePromo = Ratio(t.IncrementalSales, t.PrePromoSales),
            Cts               = Ratio(t.TotalCost, t.PromotedValue),
            PayInvDeductionAp       = t.PayInvDeductionAp,
            PayInvDeductionSalesman = t.PayInvDeductionSalesman,
            PayCheckAp              = t.PayCheckAp,
            PayCheckSalesman        = t.PayCheckSalesman,
            PayFreeGoodsAp          = t.PayFreeGoodsAp,
            PayFreeGoodsSalesman    = t.PayFreeGoodsSalesman,
            PayOffInvoiceAp         = t.PayOffInvoiceAp,
            PayOffInvoiceSalesman   = t.PayOffInvoiceSalesman,
            PaymentTotal      = payTotal,
            PaymentVariance   = t.TotalCost - payTotal,
            Branches = t.Branches.OrderBy(b => b.LineNo).Select(b => new TasafBranchDto { SiteId = b.SiteId, Name = b.Name }).ToList(),
            Customers = t.Customers.OrderBy(c => c.LineNo).Select(c => new TasafCustomerDto { CustKey = c.CustKey, CusName = c.CusName }).ToList(),
            Products = t.Products.OrderBy(p => p.LineNo).Select(p => new TasafProductDto
            {
                CProdNo = p.CProdNo, Sku = p.Sku, P3MoAvgCs = p.P3MoAvgCs, SplyCs = p.SplyCs,
                PromotedVolCs = p.PromotedVolCs, IncrVolCs = p.IncrVolCs, LspPerCase = p.LspPerCase, PromotedValue = p.PromotedValue
            }).ToList(),
            Costs = t.Costs.OrderBy(c => c.LineNo).Select(c => new TasafCostDto
            {
                CostTypeId = c.CostTypeId, CostTypeName = c.CostType?.Name, Amount = c.Amount, Remarks = c.Remarks
            }).ToList(),
            ProponentUserId   = t.ProponentUserId,
            ProponentName     = t.ProponentName,
            CanEdit           = CanEdit(t),
            CanDelete         = CanDelete(t),
            CanRevert         = CanRevert(t),
            CanCancel         = CanCancel(t),
            MyAction          = MyAction(t),
            SubmittedAt       = t.SubmittedAt,
            DecidedAt         = t.DecidedAt,
            CurrentStepSeq    = t.CurrentStepSeq,
            ApprovalTypeName  = t.ApprovalTypeName,
            CurrentRoleCode   = CurrentStep(t)?.RoleCode,
            Steps = t.Steps.OrderBy(s => s.Seq).Select(s => new TasafStepDto
            {
                Seq = s.Seq, RoleCode = s.RoleCode, RoleName = s.RoleName, UserName = s.UserName,
                Status = s.Status, ActedAt = s.ActedAt, Comment = s.Comment
            }).ToList(),
            Actions = t.Actions.OrderBy(a => a.Id).Select(a => new TasafActionDto
            {
                Action = a.Action, FromStatus = a.FromStatus, ToStatus = a.ToStatus,
                ByName = a.ByName, Role = a.Role, Comment = a.Comment, At = a.At
            }).ToList(),
            CreatedAt         = t.CreatedAt,
            CreatedBy         = t.CreatedBy,
            UpdatedAt         = t.UpdatedAt,
            UpdatedBy         = t.UpdatedBy
        };
    }
}
