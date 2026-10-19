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
    TasafPriceService prices,
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
                    Amount   = (x as ITasafAmountLookup)?.Amount,
                    BranchCodes = (x as TasafArea)?.BranchCodes
                })
                .ToList();
        }
        dto.Branches = (await sites.GetAllAsync())
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new TasafBranchDto { SiteId = s.Id, Name = s.Name, Code = s.Code })
            .Concat(BranchScopes.Select(b => new TasafBranchDto { Name = b }))
            .ToList();
        dto.SubCategories = await SubCategoryOptionsAsync();
        dto.MySalesGroupIds = await OwnSalesGroupIdsAsync(CurrentUserId);
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
    /// Setup › Customers / Chains) AND of the picked branches (Sites) — or, with no branch picked,
    /// of the picked areas (their branch codes, TASAF Setup › Areas). filtered = false when nothing
    /// narrows anything (the form searches everything).
    /// </summary>
    public async Task<(List<CustomerMatchInfo> customers, bool filtered)> GetCustomerOptionsAsync(
        int? chainId, IReadOnlyList<int> areaIds, IReadOnlyList<int> siteIds)
    {
        var fragments = new List<string>();
        if (chainId is int cid && await lookups.GetByIdAsync(typeof(TasafChain), cid) is TasafChain chain)
            fragments = SplitList(chain.CustomerMatch).Where(f => f.Length >= 2).ToList();

        var scope = await BranchScopeAsync(areaIds, siteIds);
        if (fragments.Count == 0 && scope is null) return ([], false);
        return (await customers.MatchAsync(fragments, scope, take: MaxCustomers), true);
    }

    /// <summary>
    /// Customer / Chain dropdown: active stores per chain in the picked branches (else areas).
    /// null = nothing narrows (every chain is offered). Chains with no name keywords (e.g. "LKA
    /// Others") can't be counted and are left out — the form keeps offering them.
    /// </summary>
    public async Task<Dictionary<int, int>?> GetChainStoreCountsAsync(IReadOnlyList<int> areaIds, IReadOnlyList<int> siteIds)
    {
        var scope = await BranchScopeAsync(areaIds, siteIds);
        if (scope is null) return null;
        var counts = new Dictionary<int, int>();
        foreach (var chain in (await lookups.GetAllAsync(typeof(TasafChain))).OfType<TasafChain>().Where(c => c.IsActive))
        {
            var fragments = SplitList(chain.CustomerMatch).Where(f => f.Length >= 2).ToList();
            if (fragments.Count > 0) counts[chain.Id] = await customers.CountMatchAsync(fragments, scope);
        }
        return counts;
    }

    /// <summary>
    /// The picked branches (Sites), else the picked areas' branches, as a customer filter; null = no
    /// filter (nothing picked, or an area with no branch codes, e.g. Nationwide). A customer belongs
    /// to a Site when its WhseNo is in the Site's Cuwhsenos; a Site with none (HON-B) owns its
    /// folder's unclaimed WhseNos.
    /// </summary>
    private async Task<CustomerBranchScope?> BranchScopeAsync(IReadOnlyList<int> areaIds, IReadOnlyList<int> siteIds)
    {
        var allSites = (await sites.GetAllAsync()).ToList();
        var picked = allSites.Where(s => siteIds.Contains(s.Id)).ToList();
        if (picked.Count == 0)
        {
            var codes = await AreaCodesAsync(areaIds);
            if (codes is null) return null;
            picked = allSites.Where(s => codes.Contains(s.Code) || codes.Contains(s.Name)).ToList();
        }

        var claimed = allSites.SelectMany(s => ParseWhseNos(s.Cuwhsenos)).Distinct().ToList();
        var whse = picked.SelectMany(s => ParseWhseNos(s.Cuwhsenos)).Distinct().ToList();
        var folders = picked.Where(s => ParseWhseNos(s.Cuwhsenos).Count == 0)
            .Select(s => s.Code.Split('-')[0].ToLowerInvariant()).Distinct().ToList();
        return new CustomerBranchScope(whse, folders, claimed);
    }

    /// <summary>The picked areas' Site codes; null = no area, or one with no codes (all branches).</summary>
    private async Task<HashSet<string>?> AreaCodesAsync(IReadOnlyList<int> areaIds)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in areaIds.Distinct())
        {
            if (await lookups.GetByIdAsync(typeof(TasafArea), id) is not TasafArea area) continue;
            var list = SplitList(area.BranchCodes);
            if (list.Count == 0) return null;
            codes.UnionWith(list);
        }
        return codes.Count == 0 ? null : codes;
    }

    private static List<string> SplitList(string s) =>
        s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static List<int> ParseWhseNos(string cuwhsenos) =>
        SplitList(cuwhsenos).Select(x => int.TryParse(x, out var n) ? n : -1).Where(n => n >= 0).ToList();

    /// <summary>
    /// Participating SKU dropdown: active products (not Delisted / Phased Out) of the TASAF's product
    /// scope (Book2.xlsx), mapped to the product master: a Sub-Category = its code (a product master
    /// category, ProductCategories.CategoryCode, e.g. "2a"); a Product Category with none of its
    /// sub-categories picked = the whole category (GroupNo = its code, "02" → 2). A non-numeric category
    /// code (old CORP) or no scope at all = every active product.
    /// </summary>
    public async Task<List<TasafProductOptionDto>> GetProductOptionsAsync(
        IReadOnlyList<int> productCategoryIds, IReadOnlyList<string> subCategoryCodes)
    {
        var catalog = (await products.GetCatalogAsync())
            .Where(p => !string.Equals(p.GroupDesc?.Trim(), ProductCatalogService.Delisted, StringComparison.OrdinalIgnoreCase)
                        && !p.PhOut)
            .ToList();

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var groupNos = new HashSet<int>();
        var everything = false;
        async Task WholeCategory(int? categoryId)
        {
            if (categoryId is int id && await lookups.GetByIdAsync(typeof(TasafProductGroup), id) is { } cat)
            {
                if (int.TryParse(cat.Code.Trim(), out var no)) groupNos.Add(no);
                else everything = true;
            }
        }

        var covered = new HashSet<int>();
        var subs = (await SubCategoryOptionsAsync()).ToDictionary(o => o.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var code in subCategoryCodes.Select(c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!subs.TryGetValue(code, out var sub)) continue;
            covered.Add(sub.ProductCategoryId);
            codes.Add(sub.Code);
        }
        foreach (var id in productCategoryIds.Distinct().Where(id => !covered.Contains(id)))
            await WholeCategory(id);

        IEnumerable<HOMSys.Application.DTOs.Products.ProductCatalogDto> picked = catalog;
        if (!everything && (codes.Count > 0 || groupNos.Count > 0))
            picked = catalog.Where(p => codes.Contains(p.Category.Trim()) || groupNos.Contains(p.GroupNo));

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

        await repo.SaveWithRefNoAsync(t, await PrepareRefNoAsync(t), isNew: true);
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
        await repo.SaveWithRefNoAsync(t, await PrepareRefNoAsync(t));
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

        // Every picked store must have the same price for each SKU (one LSP per SKU).
        var mixed = await MixedPriceSkusAsync(t);
        if (mixed.Count > 0)
            return (null, "The picked Cust Keys have different prices for " + string.Join("; ", mixed)
                + ". Keep only stores with the same price.");

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

        // One-time secret for the first approver's email Approve / Reject links.
        var token = NewEmailToken(first);

        // RefNo is normally assigned on the first save (PrepareRefNoAsync); a TASAF saved before
        // that existed gets it now.
        await repo.SaveWithRefNoAsync(t, t.RefNo is null ? await RefPrefixAsync(t, TodayPh().Year) : null);

        await NotifyApproverAsync(id, first.UserId, token,
            $"{t.RefNo} — {t.Title} ({t.TotalCost:N2}) from {t.ProponentName}{(resubmit ? ", resubmitted" : "")}. You sign as {first.RoleCode}.");

        return (Map((await repo.GetByIdAsync(id))!), null);
    }

    /// <summary>
    /// "TASAF for your approval" to the approver whose turn it is: bell + an email holding the whole
    /// TASAF and Approve / Reject buttons (TasafEmailRenderer).
    /// </summary>
    private async Task NotifyApproverAsync(int tasafId, int? userId, string token, string message)
    {
        var dto = Map((await repo.GetByIdAsync(tasafId))!);
        await notifications.SendAsync([userId], "TASAF for your approval", message, "/tasafs", "tasaf", tasafId,
            emailHtml: TasafEmailRenderer.Render(dto, message, token), emailRef: dto.RefNo);
    }

    // ── Email decisions (no login) ────────────────────────────
    /// <summary>How long an approver's email Approve / Reject links stay usable.</summary>
    private static readonly TimeSpan EmailTokenLife = TimeSpan.FromDays(30);

    /// <summary>New 256-bit secret for this step's email links; only its SHA-256 is stored.</summary>
    private static string NewEmailToken(TasafApprovalStep step)
    {
        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        step.EmailTokenHash = HashToken(token);
        step.EmailTokenExpires = DateTime.UtcNow.Add(EmailTokenLife);
        return token;
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    /// <summary>The TASAF + step an email link belongs to, and why it can't be used (null = usable).</summary>
    private async Task<(Tasaf? t, TasafApprovalStep? step, string? notUsable)> FromEmailTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100) return (null, null, null);
        var hash = HashToken(token.Trim());
        var t = await repo.GetByEmailTokenAsync(hash);
        var step = t?.Steps.FirstOrDefault(s => s.EmailTokenHash == hash);
        if (t is null || step is null) return (null, null, null);
        string? why = null;
        if (step.Status != TasafStepStatus.Pending)
            why = $"You already {step.Status.ToLowerInvariant()} this TASAF{(step.ActedAt is DateTime a ? $" on {a.AddHours(8):MMM d, yyyy h:mm tt}" : "")}.";
        else if (t.Status != TasafStatus.ForApproval || t.CurrentStepSeq != step.Seq)
            why = $"This TASAF is no longer waiting for your approval (status: {t.Status}).";
        else if (step.EmailTokenExpires is DateTime exp && exp < DateTime.UtcNow)
            why = "This link has expired. Please open the TASAF in HOMSys to approve or reject it.";
        return (t, step, why);
    }

    /// <summary>GET tasaf-email/{token}: the TASAF for the no-login decision page.</summary>
    public async Task<(TasafEmailDecisionDto? dto, string? error)> GetEmailDecisionAsync(string token)
    {
        var (t, step, why) = await FromEmailTokenAsync(token);
        if (t is null || step is null) return (null, "This link is not valid.");
        return (new TasafEmailDecisionDto
        {
            RefNo = t.RefNo, Title = t.Title, ApproverName = step.UserName, RoleCode = step.RoleCode,
            CanDecide = why is null, Message = why,
            Html = TasafEmailRenderer.Render(Map(t), "", null, email: false)
        }, null);
    }

    /// <summary>POST tasaf-email/{token}/decide: approve / return / reject as the step's approver (comment required).</summary>
    public async Task<(TasafEmailDecisionDto? dto, string? error)> EmailDecideAsync(string token, TasafDecisionDto dto)
    {
        var (t, step, why) = await FromEmailTokenAsync(token);
        if (t is null || step is null) return (null, "This link is not valid.");
        if (why is not null) return (null, why);
        if (dto.Decision.Trim().ToLowerInvariant() is not ("approve" or "return" or "reject")) return (null, "Choose Approve, Return or Reject.");

        var error = await DecideCoreAsync(t, step, dto, step.UserName.Length > 0 ? step.UserName : "(email)", viaEmail: true);
        if (error is not null) return (null, error);
        // The hash is kept, so the page now shows "You already approved …".
        var (after, _) = await GetEmailDecisionAsync(token);
        return (after, null);
    }

    public async Task<(TasafDto? dto, string? error)> DecideAsync(int id, TasafDecisionDto dto)
    {
        var t = await repo.GetByIdAsync(id);
        if (t is null) return (null, NotFound);

        var step = CurrentStep(t);
        if (MyAction(t) is null || step is null) return (null, "This TASAF is not waiting for your action.");

        var error = await DecideCoreAsync(t, step, dto, step.UserName.Length > 0 ? step.UserName : CurrentUser, viaEmail: false);
        return error is not null ? (null, error) : (Map((await repo.GetByIdAsync(id))!), null);
    }

    /// <summary>
    /// Approve / return / reject the current step — from the app (DecideAsync) or an email link
    /// (EmailDecideAsync). A comment is always required. Returns an error, or null when saved.
    /// </summary>
    private async Task<string?> DecideCoreAsync(Tasaf t, TasafApprovalStep step, TasafDecisionDto dto, string myName, bool viaEmail)
    {
        var decision = dto.Decision.Trim().ToLowerInvariant();
        var comment = dto.Comment.Trim();
        if (decision is not ("approve" or "return" or "reject")) return "Unknown decision.";
        if (comment.Length == 0) return "Please enter a comment.";
        if (comment.Length > 1000) return "Comment must be 1000 characters or fewer.";

        var from = t.Status;
        var head = $"{t.RefNo} — {t.Title}";
        var how = viaEmail ? " via email" : "";
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
                    t.Actions.Add(NewAction($"Approved{how}", from, t.Status, step.RoleCode, comment, myName));
                    var nextToken = NewEmailToken(next);
                    await repo.UpdateAsync(t);
                    await NotifyApproverAsync(t.Id, next.UserId, nextToken,
                        $"{head} ({t.TotalCost:N2}) from {t.ProponentName}, approved by {myName} ({step.RoleCode}). You sign as {next.RoleCode}.");
                    await notifications.SendAsync([t.ProponentUserId], "TASAF approval progress",
                        $"{head} was approved by {myName} ({step.RoleCode}): \"{comment}\". Now with {next.UserName} ({next.RoleCode}).",
                        "/tasafs", "tasaf", t.Id, emailRef: t.RefNo);
                }
                else
                {
                    t.Status = TasafStatus.Approved;
                    t.CurrentStepSeq = null;
                    t.CurrentApproverUserId = null;
                    t.DecidedAt = DateTime.UtcNow;
                    t.Actions.Add(NewAction($"Approved{how}", from, t.Status, step.RoleCode, comment, myName));
                    await repo.UpdateAsync(t);
                    await notifications.SendAsync([t.ProponentUserId, .. signed], "TASAF approved",
                        $"{head} is fully approved (final sign-off by {myName}, {step.RoleCode}): \"{comment}\".", "/tasafs", "tasaf", t.Id, emailRef: t.RefNo);
                }
                break;

            case "return":
                step.Status = TasafStepStatus.Returned;
                t.Status = TasafStatus.Returned;
                t.CurrentStepSeq = null;
                t.CurrentApproverUserId = null;
                t.Actions.Add(NewAction($"Returned{how}", from, t.Status, step.RoleCode, comment, myName));
                await repo.UpdateAsync(t);
                await notifications.SendAsync([t.ProponentUserId, .. signed], "TASAF returned",
                    $"{head} was returned by {myName} ({step.RoleCode}): {comment}", "/tasafs", "tasaf", t.Id, emailRef: t.RefNo);
                break;

            default: // reject
                step.Status = TasafStepStatus.Rejected;
                t.Status = TasafStatus.Rejected;
                t.CurrentStepSeq = null;
                t.CurrentApproverUserId = null;
                t.DecidedAt = DateTime.UtcNow;
                t.Actions.Add(NewAction($"Rejected{how}", from, t.Status, step.RoleCode, comment, myName));
                await repo.UpdateAsync(t);
                await notifications.SendAsync([t.ProponentUserId, .. signed], "TASAF rejected",
                    $"{head} was rejected by {myName} ({step.RoleCode}): {comment}", "/tasafs", "tasaf", t.Id, emailRef: t.RefNo);
                break;
        }

        return null;
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
            "/tasafs", "tasaf", t.Id, emailRef: t.RefNo);
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
            "/tasafs", "tasaf", t.Id, emailRef: t.RefNo);
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
        if (t.ProductCategories.Count == 0) m.Add("Product Category");
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
    /// <summary>
    /// RefNo prefix "YEAR-SG-INITIALS-" (GUIDE sheet), e.g. "2026-RKA-ER-"; null while the TASAF has
    /// no sales group yet. INITIALS = the proponent's first-name + last-name initials.
    /// </summary>
    private async Task<string?> RefPrefixAsync(Tasaf t, int year)
    {
        if (t.SalesGroupId is not int sgId || await lookups.GetByIdAsync(typeof(TasafSalesGroup), sgId) is not { } sg)
            return null;
        var proponent = await users.GetByIdAsync(t.ProponentUserId);
        var initials = Initials(proponent?.FirstName ?? "", proponent?.LastName ?? "", t.ProponentName);
        return $"{year}-{(sg.Code.Length > 0 ? sg.Code : sg.Name).ToUpperInvariant()}-{initials}-";
    }

    /// <summary>
    /// Before a save. RefNo = YEAR-SG-INITIALS-NN, taken on the FIRST save that has a sales group:
    /// returns the prefix for the repository to add the next NN (one continuous series, never
    /// resets — TasafRepository.SaveWithRefNoAsync). Once numbered, YEAR and NN never change; a
    /// changed sales group just re-labels the SG part while the TASAF is still editable.
    /// </summary>
    private async Task<string?> PrepareRefNoAsync(Tasaf t)
    {
        if (t.RefNo is null) return await RefPrefixAsync(t, TodayPh().Year);
        var parts = t.RefNo.Split('-');
        if (parts.Length >= 4 && int.TryParse(parts[0], out var year) && await RefPrefixAsync(t, year) is string prefix)
            t.RefNo = prefix + parts[^1];
        return null;
    }

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

    /// <summary>
    /// TASAF Sub-Category options = product master categories (ProductCategories, the Products page) whose
    /// GroupNo has a Product Category (Master Data › Product Categories, code "02" = GroupNo 2): code,
    /// SubCat as the name, GroupDesc as the Segment. Groups with no Product Category (Dummy, Delisted) are left out.
    /// </summary>
    private async Task<List<TasafSubCategoryOptionDto>> SubCategoryOptionsAsync()
    {
        var categories = (await lookups.GetAllAsync(typeof(TasafProductGroup)))
            .Where(c => int.TryParse(c.Code.Trim(), out _))
            .GroupBy(c => int.Parse(c.Code.Trim()))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.IsActive).First().Id);
        return (await products.GetCategoriesAsync())
            .Where(c => categories.ContainsKey(c.GroupNo) && c.CategoryCode.Trim().Length > 0)
            .Select(c => new TasafSubCategoryOptionDto
            {
                Code = c.CategoryCode.Trim(), Name = c.SubCat.Trim(), Segment = c.GroupDesc.Trim(),
                ProductCategoryId = categories[c.GroupNo]
            })
            .ToList();
    }

    /// <summary>LSP is priced as of the program start date (today, Manila, when not set).</summary>
    private static DateOnly PriceDate(Tasaf t) => t.StartDate ?? DateOnly.FromDateTime(TodayPh());

    /// <summary>Price the TASAF's SKUs for its stores (form: LSP / case). See TasafPriceService.</summary>
    public Task<List<TasafSkuPriceDto>> GetProductPricesAsync(TasafPriceRequest req) =>
        prices.PriceAsync(req.CProdNos.Take(200), req.CustKeys.Take(MaxCustomers),
            req.AsOf ?? DateOnly.FromDateTime(TodayPh()));

    /// <summary>"7615 (462.37 × 2 stores, 471.33 × 1 store)" for each SKU whose stores differ in price.</summary>
    private async Task<List<string>> MixedPriceSkusAsync(Tasaf t)
    {
        if (t.Products.Count == 0 || t.Customers.Count == 0) return [];
        var priced = await prices.PriceAsync(t.Products.Select(p => p.CProdNo), t.Customers.Select(c => c.CustKey), PriceDate(t));
        return priced.Where(p => p.Groups.Count > 1)
            .Select(p => $"{p.CProdNo} ({string.Join(", ", p.Groups.Select(g => $"{g.Price:N2} × {g.CustKeys.Count} store{(g.CustKeys.Count == 1 ? "" : "s")}"))})")
            .ToList();
    }

    /// <summary>
    /// Sales groups a proponent may pick: their own (TASAF Setup › Sales Groups › Users). Empty = no limit —
    /// admins, View All users, and users in no group yet.
    /// </summary>
    private async Task<List<int>> OwnSalesGroupIdsAsync(int userId) =>
        IsAdmin || CanViewAll || userId == 0 ? [] : await lookups.GetSalesGroupIdsOfUserAsync(userId);

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
        };
        foreach (var (lookupId, key, label) in checks)
            if (lookupId is int lid && !await LookupExistsAsync(key, lid))
                return $"{label} not found.";

        // A proponent in sales groups files only for those (a group already on the TASAF may stay).
        if (dto.SalesGroupId is int sgPick && sgPick != t.SalesGroupId)
        {
            var own = await OwnSalesGroupIdsAsync(t.ProponentUserId != 0 ? t.ProponentUserId : CurrentUserId);
            if (own.Count > 0 && !own.Contains(sgPick)) return "You can only pick your own Sales Group (TASAF Setup › Sales Groups).";
        }

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
        var productCategoryIds = (dto.ProductCategoryIds ?? []).Distinct().ToList();
        foreach (var pcid in productCategoryIds)
            if (!await LookupExistsAsync("product-groups", pcid)) return "Product Category not found.";
        // Sub-Categories = product master categories; the names are snapshotted onto the TASAF.
        var subOptions = (await SubCategoryOptionsAsync()).ToDictionary(o => o.Code, StringComparer.OrdinalIgnoreCase);
        var keptSubs = t.SubCategories.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var subCategories = new List<TasafSubCategoryLink>();
        foreach (var code in (dto.SubCategoryCodes ?? []).Select(c => (c ?? "").Trim()).Where(c => c.Length > 0)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (subOptions.TryGetValue(code, out var o))
                subCategories.Add(new TasafSubCategoryLink { Code = o.Code, Name = o.Name, Segment = o.Segment });
            else if (keptSubs.TryGetValue(code, out var kept)) // no longer in the master: keep the saved snapshot
                subCategories.Add(new TasafSubCategoryLink { Code = kept.Code, Name = kept.Name, Segment = kept.Segment });
            else return $"Product Sub-Category {code} not found.";
        }

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

        t.ProductCategories.Clear();
        line = 0;
        foreach (var pcid in productCategoryIds)
            t.ProductCategories.Add(new TasafProductCategoryLink { LineNo = ++line, ProductGroupId = pcid });

        t.SubCategories.Clear();
        line = 0;
        foreach (var sc in subCategories)
        {
            sc.LineNo = ++line;
            t.SubCategories.Add(sc);
        }

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
                LspComputed   = null, // set below from the picked stores
                PromotedValue = Math.Round(p.PromotedVolCs * p.LspPerCase, 2)
            });

        // LSP the picked stores actually have (one price) — kept so approvers see an overridden LSP.
        if (t.Products.Count > 0 && t.Customers.Count > 0)
        {
            var priced = (await prices.PriceAsync(t.Products.Select(p => p.CProdNo), t.Customers.Select(c => c.CustKey), PriceDate(t)))
                .ToDictionary(x => x.CProdNo, StringComparer.OrdinalIgnoreCase);
            foreach (var p in t.Products)
                p.LspComputed = priced.TryGetValue(p.CProdNo, out var sp) && sp.Groups.Count == 1 ? sp.Groups[0].Price : null;
        }

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
            CtsLimitPercent   = t.ProgramType?.MaxCtsPercent,
            ProgramTypeOther  = t.ProgramTypeOther,
            ObjectiveId       = t.ObjectiveId,
            ObjectiveName     = t.Objective?.Name,
            ObjectiveOther    = t.ObjectiveOther,
            TargetMarketId    = t.TargetMarketId,
            TargetMarketName  = t.TargetMarket?.Name,
            TargetMarketOther = t.TargetMarketOther,
            ChannelIds        = t.Channels.OrderBy(c => c.LineNo).Select(c => c.ChannelId).ToList(),
            ChannelNames      = string.Join(", ", t.Channels.OrderBy(c => c.LineNo).Select(c => c.Channel?.Name).Where(n => n is not null)),
            ProductCategoryIds   = t.ProductCategories.OrderBy(c => c.LineNo).Select(c => c.ProductGroupId).ToList(),
            ProductCategoryNames = string.Join(", ", t.ProductCategories.OrderBy(c => c.LineNo).Select(c => c.ProductGroup?.Name).Where(n => n is not null)),
            SubCategoryCodes     = t.SubCategories.OrderBy(c => c.LineNo).Select(c => c.Code).ToList(),
            SubCategoryNames     = string.Join(", ", t.SubCategories.OrderBy(c => c.LineNo).Select(c => $"{c.Code} - {c.Name}")),
            SegmentNames         = string.Join(", ", t.SubCategories.OrderBy(c => c.LineNo).Select(c => c.Segment).Where(n => n.Length > 0)),
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
                PromotedVolCs = p.PromotedVolCs, IncrVolCs = p.IncrVolCs, LspPerCase = p.LspPerCase, LspComputed = p.LspComputed,
                PromotedValue = p.PromotedValue
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
