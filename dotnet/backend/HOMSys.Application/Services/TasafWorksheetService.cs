using System.Security.Claims;
using HOMSys.Application.DTOs.Tasaf;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace HOMSys.Application.Services;

/// <summary>
/// TASAF worksheets — the "LF Breakdown" and "Near Ex" sheets of TASA Form_MT version template 2026.xlsx, filled out
/// by the proponent and attached to (or detached from) their TASAF. No approval of their own; attaching is optional.
/// Typed columns stay as typed; the computed ones use the workbook's formulas (<see cref="Compute(TasafListingFeeLine)"/>).
/// Unattached = only its creator sees it; attached = also the TASAF's proponent / approvers and TASAF View All.
/// </summary>
public class TasafWorksheetService(
    ITasafWorksheetRepository repo,
    ITasafRepository tasafs,
    IUserRepository users,
    IHttpContextAccessor http)
{
    public const string NotFound  = "Worksheet not found.";
    public const string Forbidden = "You can only open your own worksheets or those attached to a TASAF you review.";
    private const int MaxLines = 200;

    private ClaimsPrincipal? Principal => http.HttpContext?.User;
    private string CurrentUser => Principal?.FindFirstValue(ClaimTypes.Name) ?? "system";
    private int CurrentUserId => int.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private bool CanViewAll => Principal?.HasClaim("permission", "tasaf-view-all") ?? false;

    public static string TypeLabel(string type) => type switch
    {
        TasafWorksheetType.ListingFee => "Listing Fee",
        TasafWorksheetType.NearEx     => "Near Ex Moveout",
        _ => type
    };

    /// <summary>Attached to a TASAF that is past Draft / Returned: the approvers sign what they saw.</summary>
    public static bool IsLocked(Tasaf? t) =>
        t is not null && t.Status is not (TasafStatus.Draft or TasafStatus.Returned);

    /// <summary>
    /// The creator always; once attached, also the TASAF's proponent / approvers and TASAF View All.
    /// An unattached worksheet is a private draft — only its creator sees it (user decision 2026-10-30).
    /// </summary>
    private bool CanView(TasafWorksheet w) =>
        w.ProponentUserId == CurrentUserId
        || (w.Tasaf is { } t && (CanViewAll || t.ProponentUserId == CurrentUserId || t.Steps.Any(s => s.UserId == CurrentUserId)));

    private bool CanEdit(TasafWorksheet w) => w.ProponentUserId == CurrentUserId && !IsLocked(w.Tasaf);

    // ── Read ──────────────────────────────────────────────────
    public async Task<List<TasafWorksheetListItemDto>> GetListAsync() =>
        (await repo.GetListAsync(CurrentUserId, allAttached: CanViewAll))
            .Select(w => new TasafWorksheetListItemDto
            {
                Id            = w.Id,
                Type          = w.Type,
                TypeLabel     = TypeLabel(w.Type),
                Title         = w.Title,
                ProponentName = w.ProponentName,
                TasafId       = w.TasafId,
                TasafRefNo    = w.Tasaf?.RefNo,
                TasafTitle    = w.Tasaf?.Title,
                TasafStatus   = w.Tasaf?.Status,
                LineCount     = w.Type == TasafWorksheetType.NearEx ? w.NearExLines.Count : w.ListingFeeLines.Count,
                Total         = TotalOf(w),
                IsMine        = w.ProponentUserId == CurrentUserId,
                Locked        = IsLocked(w.Tasaf),
                CreatedAt     = w.CreatedAt,
                UpdatedAt     = w.UpdatedAt
            })
            .ToList();

    public async Task<(TasafWorksheetDto? dto, string? error)> GetByIdAsync(int id)
    {
        var w = await repo.GetByIdAsync(id);
        if (w is null) return (null, NotFound);
        if (!CanView(w)) return (null, Forbidden);
        return (ToDto(w, w.Tasaf, CurrentUserId), null);
    }

    // ── Write ─────────────────────────────────────────────────
    public async Task<(TasafWorksheetDto? dto, string? error)> CreateAsync(SaveTasafWorksheetDto dto)
    {
        var user = await users.GetByIdAsync(CurrentUserId);
        if (user is null) return (null, "Your user account was not found.");
        if (!TasafWorksheetType.All.Contains(dto.Type)) return (null, "Unknown worksheet type.");

        var w = new TasafWorksheet
        {
            Type            = dto.Type,
            ProponentUserId = user.Id,
            ProponentName   = $"{user.FirstName} {user.LastName}".Trim(),
            CreatedBy       = CurrentUser
        };
        var error = Apply(w, dto);
        if (error is not null) return (null, error);

        await repo.CreateAsync(w);
        return await GetByIdAsync(w.Id);
    }

    public async Task<(TasafWorksheetDto? dto, string? error)> UpdateAsync(int id, SaveTasafWorksheetDto dto)
    {
        var w = await repo.GetByIdAsync(id);
        if (w is null) return (null, NotFound);
        if (w.ProponentUserId != CurrentUserId) return (null, "Only the proponent can edit this worksheet.");
        if (IsLocked(w.Tasaf)) return (null, LockedMessage(w.Tasaf!));

        var error = Apply(w, dto);
        if (error is not null) return (null, error);
        w.UpdatedAt = DateTime.UtcNow;
        w.UpdatedBy = CurrentUser;

        await repo.UpdateAsync(w);
        return await GetByIdAsync(id);
    }

    public async Task<string?> DeleteAsync(int id)
    {
        var w = await repo.GetByIdAsync(id);
        if (w is null) return NotFound;
        if (w.ProponentUserId != CurrentUserId) return "Only the proponent can delete this worksheet.";
        if (IsLocked(w.Tasaf)) return LockedMessage(w.Tasaf!);
        await repo.DeleteAsync(w);
        return null;
    }

    /// <summary>Attach to one of the caller's own TASAFs that is still Draft / Returned (replaces any earlier link).</summary>
    public async Task<(TasafWorksheetDto? dto, string? error)> AttachAsync(int id, int tasafId)
    {
        var w = await repo.GetByIdAsync(id);
        if (w is null) return (null, NotFound);
        if (w.ProponentUserId != CurrentUserId) return (null, "Only the proponent can attach this worksheet.");
        if (IsLocked(w.Tasaf)) return (null, LockedMessage(w.Tasaf!));

        var t = await tasafs.GetByIdAsync(tasafId);
        if (t is null) return (null, TasafService.NotFound);
        if (t.ProponentUserId != CurrentUserId) return (null, "You can only attach worksheets to your own TASAFs.");
        if (IsLocked(t)) return (null, $"TASAF {t.RefNo ?? "#" + t.Id} is {t.Status}; attach worksheets while it is Draft or Returned.");

        w.TasafId   = t.Id;
        w.UpdatedAt = DateTime.UtcNow;
        w.UpdatedBy = CurrentUser;
        await repo.UpdateAsync(w);
        return await GetByIdAsync(id);
    }

    public async Task<(TasafWorksheetDto? dto, string? error)> DetachAsync(int id)
    {
        var w = await repo.GetByIdAsync(id);
        if (w is null) return (null, NotFound);
        if (w.ProponentUserId != CurrentUserId) return (null, "Only the proponent can detach this worksheet.");
        if (IsLocked(w.Tasaf)) return (null, LockedMessage(w.Tasaf!));

        w.TasafId   = null;
        w.UpdatedAt = DateTime.UtcNow;
        w.UpdatedBy = CurrentUser;
        await repo.UpdateAsync(w);
        return await GetByIdAsync(id);
    }

    private static string LockedMessage(Tasaf t) =>
        $"This worksheet is attached to TASAF {t.RefNo ?? "#" + t.Id}, which is {t.Status} — it can't be changed or detached.";

    // ── Validation + computation ──────────────────────────────
    private static string? Apply(TasafWorksheet w, SaveTasafWorksheetDto dto)
    {
        if (dto.Type != w.Type) return "A worksheet's type can't be changed.";
        var title = (dto.Title ?? "").Trim();
        if (title.Length > 200) return "Title must be 200 characters or fewer.";
        w.Title = title.Length > 0 ? title : TypeLabel(w.Type);

        if (w.Type == TasafWorksheetType.ListingFee)
        {
            var rows = dto.ListingFeeLines.Where(l => !IsBlank(l)).ToList();
            if (rows.Count > MaxLines) return $"A worksheet can have at most {MaxLines} rows.";
            var lines = new List<TasafListingFeeLine>();
            foreach (var (l, i) in rows.Select((l, i) => (l, i)))
            {
                if (l.LfPerSku is < 0 || l.SkuCount is < 0 || l.DoorCount is < 0 || l.ProjectedAnnualSales is < 0)
                    return $"Row {i + 1}: amounts and counts can't be negative.";
                if (!TasafListingFeeBasis.All.Contains(l.Basis)) return $"Row {i + 1}: pick how the TOTAL is computed.";
                var line = new TasafListingFeeLine
                {
                    LineNo = i + 1, SalesGroup = Clip(l.SalesGroup), Channel = Clip(l.Channel), Account = Clip(l.Account),
                    Category = Clip(l.Category), LfPerSku = l.LfPerSku, SkuCount = l.SkuCount, DoorCount = l.DoorCount,
                    Basis = l.Basis, ProjectedAnnualSales = l.ProjectedAnnualSales, Remarks = Clip(l.Remarks, 500)
                };
                Compute(line);
                lines.Add(line);
            }
            w.ListingFeeLines.Clear();
            w.ListingFeeLines.AddRange(lines);
        }
        else
        {
            var rows = dto.NearExLines.Where(l => !IsBlank(l)).ToList();
            if (rows.Count > MaxLines) return $"A worksheet can have at most {MaxLines} rows.";
            var lines = new List<TasafNearExLine>();
            foreach (var (l, i) in rows.Select((l, i) => (l, i)))
            {
                if (l.LspPerCase is < 0 || l.LspPerPc is < 0 || l.PcsPerCase is < 0 || l.InvCases is < 0 || l.InvPieces is < 0)
                    return $"Row {i + 1}: prices and quantities can't be negative.";
                if (l.Discount is < 0 or > 1) return $"Row {i + 1}: Discount / Rebate must be between 0% and 100%.";
                var line = new TasafNearExLine
                {
                    LineNo = i + 1, SalesGroup = Clip(l.SalesGroup), Account = Clip(l.Account), Category = Clip(l.Category),
                    Sku = Clip(l.Sku), LspPerCase = l.LspPerCase, LspPerPc = l.LspPerPc, PcsPerCase = l.PcsPerCase,
                    Expiry = l.Expiry is DateOnly e ? new DateOnly(e.Year, e.Month, 1) : null,
                    InvCases = l.InvCases, InvPieces = l.InvPieces, MoveoutScheme = Clip(l.MoveoutScheme), Discount = l.Discount
                };
                Compute(line);
                lines.Add(line);
            }
            w.NearExLines.Clear();
            w.NearExLines.AddRange(lines);
        }
        return null;
    }

    /// <summary>LF Breakdown: TOTAL = LF/SKU × # of Doors (=F*H) or × # of SKUs (=F*G); CTS = TOTAL ÷ PROJECTED ANNUAL SALES (=I/J).</summary>
    public static void Compute(TasafListingFeeLine l)
    {
        var lf = l.LfPerSku ?? 0;
        l.Total = Math.Round(lf * (l.Basis == TasafListingFeeBasis.AllDoors ? l.SkuCount ?? 0 : l.DoorCount ?? 0), 2);
        l.Cts = l.ProjectedAnnualSales is > 0 and var sales ? Math.Round(l.Total / sales, 6) : null;
    }

    /// <summary>Near Ex: Total in Pcs = Cases × Pcs/Cs + Pieces (=(J*H)+K); Total Value = Cases × LSP/Case + Pieces × LSP/Pc
    /// (=(J*F)+(G*K)); Moveout Cost = Total Value × Discount (=M*O).</summary>
    public static void Compute(TasafNearExLine l)
    {
        int cases = l.InvCases ?? 0, pieces = l.InvPieces ?? 0;
        l.TotalPcs    = cases * (l.PcsPerCase ?? 0) + pieces;
        l.TotalValue  = Math.Round(cases * (l.LspPerCase ?? 0) + pieces * (l.LspPerPc ?? 0), 2);
        l.MoveoutCost = Math.Round(l.TotalValue * (l.Discount ?? 0), 2);
    }

    private static bool IsBlank(TasafListingFeeLineDto l) =>
        string.IsNullOrWhiteSpace(l.SalesGroup) && string.IsNullOrWhiteSpace(l.Channel) && string.IsNullOrWhiteSpace(l.Account)
        && string.IsNullOrWhiteSpace(l.Category) && l.LfPerSku is null && l.SkuCount is null && l.DoorCount is null
        && l.ProjectedAnnualSales is null && string.IsNullOrWhiteSpace(l.Remarks);

    private static bool IsBlank(TasafNearExLineDto l) =>
        string.IsNullOrWhiteSpace(l.SalesGroup) && string.IsNullOrWhiteSpace(l.Account) && string.IsNullOrWhiteSpace(l.Category)
        && string.IsNullOrWhiteSpace(l.Sku) && l.LspPerCase is null && l.LspPerPc is null && l.PcsPerCase is null
        && l.Expiry is null && l.InvCases is null && l.InvPieces is null && string.IsNullOrWhiteSpace(l.MoveoutScheme)
        && l.Discount is null;

    private static string Clip(string? s, int max = 150)
    {
        var v = (s ?? "").Trim();
        return v.Length > max ? v[..max] : v;
    }

    public static decimal TotalOf(TasafWorksheet w) =>
        w.Type == TasafWorksheetType.NearEx ? w.NearExLines.Sum(l => l.MoveoutCost) : w.ListingFeeLines.Sum(l => l.Total);

    /// <summary>Full DTO; <paramref name="t"/> = the TASAF it is attached to (for its RefNo / status), if loaded.</summary>
    public static TasafWorksheetDto ToDto(TasafWorksheet w, Tasaf? t, int currentUserId) => new()
    {
        Id              = w.Id,
        Type            = w.Type,
        TypeLabel       = TypeLabel(w.Type),
        Title           = w.Title,
        ProponentUserId = w.ProponentUserId,
        ProponentName   = w.ProponentName,
        TasafId         = w.TasafId,
        TasafRefNo      = t?.RefNo,
        TasafTitle      = t?.Title,
        TasafStatus     = t?.Status,
        Total           = TotalOf(w),
        Locked          = IsLocked(t),
        CanEdit         = w.ProponentUserId == currentUserId && !IsLocked(t),
        CreatedAt       = w.CreatedAt,
        CreatedBy       = w.CreatedBy,
        UpdatedAt       = w.UpdatedAt,
        UpdatedBy       = w.UpdatedBy,
        ListingFeeLines = w.ListingFeeLines.OrderBy(l => l.LineNo).Select(l => new TasafListingFeeLineDto
        {
            SalesGroup = l.SalesGroup, Channel = l.Channel, Account = l.Account, Category = l.Category,
            LfPerSku = l.LfPerSku, SkuCount = l.SkuCount, DoorCount = l.DoorCount, Basis = l.Basis, Total = l.Total,
            ProjectedAnnualSales = l.ProjectedAnnualSales, Cts = l.Cts, Remarks = l.Remarks
        }).ToList(),
        NearExLines = w.NearExLines.OrderBy(l => l.LineNo).Select(l => new TasafNearExLineDto
        {
            SalesGroup = l.SalesGroup, Account = l.Account, Category = l.Category, Sku = l.Sku,
            LspPerCase = l.LspPerCase, LspPerPc = l.LspPerPc, PcsPerCase = l.PcsPerCase, Expiry = l.Expiry,
            InvCases = l.InvCases, InvPieces = l.InvPieces, TotalPcs = l.TotalPcs, TotalValue = l.TotalValue,
            MoveoutScheme = l.MoveoutScheme, Discount = l.Discount, MoveoutCost = l.MoveoutCost
        }).ToList()
    };
}
