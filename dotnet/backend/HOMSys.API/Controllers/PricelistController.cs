using System.Security.Claims;
using HOMSys.Application.Interfaces;
using HOMSys.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

public class ExportPricelistRequest
{
    public List<string> CustKeys { get; set; } = [];
    public DateOnly EffectivityDate { get; set; }
    public decimal SrpMarkupPercent { get; set; } = 3m;
    public string? SkuFilter { get; set; }
}

public class ExportZonePricelistRequest
{
    public string Branch { get; set; } = "";
    public List<string> Zones { get; set; } = [];
    public DateOnly EffectivityDate { get; set; }
    public decimal SrpMarkupPercent { get; set; } = 3m;
    public string? SkuFilter { get; set; }
}

public class ExportGroupedPricelistRequest
{
    public string Branch { get; set; } = "";
    public DateOnly EffectivityDate { get; set; }
    public decimal SrpMarkupPercent { get; set; } = 3m;
    public string? BaselineCustKey { get; set; }
    public string? SkuFilter { get; set; }
}

/// <summary>Zone picker option: code + description + a representative-customer label,
/// e.g. "1200 - CAGAYAN DE ORO - Juan Dela Cruz & 42 more".</summary>
public record ZoneOptionDto(string Zone, string? Description, string Label);

/// <summary>Branch picker option — Value is what every other endpoint expects back
/// (the raw pricing-folder code for independent branches, or the Site name for
/// hon-priced ones); Label is a friendly display name for the same thing.</summary>
public record BranchOptionDto(string Value, string Label);

/// <summary>
/// Downloadable branch pricelist, in three report modes: Per Account (one
/// column per customer), Per Zone (one column per zone), and Grouped by Price
/// (customers collapsed into identical-price groups, optionally diffed
/// against a baseline customer). One page (pricelist-export) hosts all three.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PricelistController(
    PricelistExportService exportService,
    PricelistExcelBuilder excelBuilder,
    ZonePricelistExportService zoneExportService,
    ZonePricelistExcelBuilder zoneExcelBuilder,
    GroupedPricelistService groupedService,
    GroupedPricelistExcelBuilder groupedExcelBuilder,
    IPricingRepository pricingRepo,
    ICustomerRepository customerRepo,
    ISiteRepository siteRepo) : ControllerBase
{
    private async Task<List<BranchOptionDto>> BuildBranchOptionsAsync()
    {
        var branches = await pricingRepo.GetBranchesWithZonesAsync();
        var honBranches = await zoneExportService.GetHonBranchesAsync();

        // Sites.Code follows "{FOLDER}-B" (e.g. "BAC-B" for the "bac" ADDON
        // folder) — strip the suffix to map an independent branch's raw
        // pricing-folder code to its friendly Sites.Name. Value stays the raw
        // code (every other endpoint resolves it as-is); only the Label changes.
        var sites = await siteRepo.GetAllAsync();
        var displayNameByCode = sites
            .Where(s => s.Code.EndsWith("-B", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                s => s.Code[..^2],
                s => s.Name,
                StringComparer.OrdinalIgnoreCase);

        var independentOptions = branches
            .Where(b => !string.Equals(b, "hon", StringComparison.OrdinalIgnoreCase))
            .Select(b => new BranchOptionDto(b, displayNameByCode.GetValueOrDefault(b, b)));
        var honOptions = honBranches.Keys.Select(name => new BranchOptionDto(name, name));

        return independentOptions.Concat(honOptions)
            .OrderBy(b => b.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches()
    {
        var expanded = await BuildBranchOptionsAsync();
        return Ok(new { success = true, data = expanded });
    }

    /// <summary>
    /// Resolves the signed-in user's own User.BranchCode (a free-text field on
    /// the Users page, entered in whatever format the Sites page shows — the
    /// Site *Code*, e.g. "CDC-B", not the raw pricing-folder code/Site name
    /// the branch picker's Value actually uses) down to the exact
    /// BranchOptionDto.Value from GetBranches, so branch-locked roles
    /// (Branch Administrator, Sales Encoder) always land on a real, selectable
    /// option instead of a client-side guess that can silently fail to match
    /// (e.g. hon-priced sites like Cabuyao, whose Value is the Site Name
    /// "CABUYAO", not "cdc" or "CDC-B").
    /// </summary>
    [HttpGet("my-branch")]
    public async Task<IActionResult> GetMyBranch() =>
        Ok(new { success = true, data = await ResolveMyBranchAsync() });

    private async Task<string?> ResolveMyBranchAsync()
    {
        var rawCode = User.FindFirstValue("branch");
        if (string.IsNullOrWhiteSpace(rawCode))
            return null;

        var strippedCode = rawCode.EndsWith("-B", StringComparison.OrdinalIgnoreCase)
            ? rawCode[..^2]
            : rawCode;

        // Find the Site this user's BranchCode actually names — try the raw
        // value as a full Site.Code first ("CDC-B"), then as the stripped
        // folder code ("CDC" against a Code of "CDC-B").
        var sites = await siteRepo.GetAllAsync();
        var site =
            sites.FirstOrDefault(s => string.Equals(s.Code, rawCode, StringComparison.OrdinalIgnoreCase)) ??
            sites.FirstOrDefault(s => string.Equals(s.Code, strippedCode + "-B", StringComparison.OrdinalIgnoreCase));

        // hon-priced sites (no ADDON folder of their own) are keyed by
        // Site.Name in the picker (see BuildBranchOptionsAsync's honOptions);
        // everything else is keyed by the raw pricing-folder code — resolve
        // that code's real casing off the live ZoneAddOns branch list rather
        // than trusting whatever case ended up in the free-text field.
        if (site is { PricesOffHon: true })
            return site.Name;

        var branches = await pricingRepo.GetBranchesWithZonesAsync();
        return branches.FirstOrDefault(b => string.Equals(b, strippedCode, StringComparison.OrdinalIgnoreCase))
               ?? strippedCode;
    }

    // ── Branch lock, enforced server-side ────────────────────────────────
    // The page disables the branch picker for these roles, but that is only
    // UI — every endpoint that returns prices re-checks the request against
    // the caller's own JWT "branch" claim. Role names are admin-typed, so
    // compare case-insensitively (same as the frontend's hasRole).
    private static readonly string[] BranchLockedRoles = ["Branch Administrator", "Sales Encoder"];

    private bool IsBranchLocked() =>
        User.FindAll(ClaimTypes.Role)
            .Any(c => BranchLockedRoles.Contains(c.Value, StringComparer.OrdinalIgnoreCase));

    private ObjectResult BranchForbidden() =>
        StatusCode(StatusCodes.Status403Forbidden,
            new { success = false, message = "You can only generate pricelists for your own branch." });

    /// <summary>Null when allowed; otherwise the 403 to return.</summary>
    private async Task<IActionResult?> CheckBranchAccessAsync(string branch)
    {
        if (!IsBranchLocked()) return null;
        var own = await ResolveMyBranchAsync();
        return own is not null && string.Equals(own, branch, StringComparison.OrdinalIgnoreCase)
            ? null
            : BranchForbidden();
    }

    /// <summary>
    /// Null when every customer belongs to the caller's branch; otherwise the
    /// 403 to return. Checks the same most-recently-imported Customer row the
    /// Per Account export prices from (GetByCustKeysAsync), using the same
    /// folder + cuwhsenos scoping as the branch-scoped customer search.
    /// </summary>
    private async Task<IActionResult?> CheckCustomerAccessAsync(IReadOnlyCollection<string> custKeys)
    {
        if (!IsBranchLocked()) return null;
        var own = await ResolveMyBranchAsync();
        if (own is null) return BranchForbidden();

        var honBranches = await zoneExportService.GetHonBranchesAsync();
        var pricingFolder = ZonePricelistExportService.ResolvePricingFolder(own, honBranches.Keys);
        honBranches.TryGetValue(own, out var whseNos);

        var customers = await customerRepo.GetByCustKeysAsync(custKeys);
        var allInBranch = custKeys.All(k =>
            customers.TryGetValue(k, out var c) &&
            string.Equals(c.Branch, pricingFolder, StringComparison.OrdinalIgnoreCase) &&
            (whseNos is not { Count: > 0 } || whseNos.Contains(c.WhseNo)));
        return allInBranch ? null : BranchForbidden();
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] ExportPricelistRequest request)
    {
        if (request.CustKeys.Count == 0)
            return BadRequest(new { success = false, message = "At least one customer must be selected." });
        if (await CheckCustomerAccessAsync(request.CustKeys) is { } denied)
            return denied;

        var result = await exportService.BuildAsync(request.CustKeys, request.EffectivityDate, request.SrpMarkupPercent);
        return Ok(new { success = true, data = result });
    }

    [HttpPost("export")]
    public async Task<IActionResult> Export([FromBody] ExportPricelistRequest request)
    {
        if (request.CustKeys.Count == 0)
            return BadRequest(new { success = false, message = "At least one customer must be selected." });
        if (await CheckCustomerAccessAsync(request.CustKeys) is { } denied)
            return denied;

        var result = await exportService.BuildAsync(request.CustKeys, request.EffectivityDate, request.SrpMarkupPercent);
        result = ApplySkuFilter(result, request.SkuFilter);
        var bytes = excelBuilder.Build(result);

        var fileName = $"Pricelist_{request.EffectivityDate:yyyyMMdd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("zone-branches/{branch}/zones")]
    public async Task<IActionResult> GetZonesForBranch(string branch)
    {
        if (await CheckBranchAccessAsync(branch) is { } denied)
            return denied;

        var honBranches = await zoneExportService.GetHonBranchesAsync();
        var pricingFolder = ZonePricelistExportService.ResolvePricingFolder(branch, honBranches.Keys);
        var hasWhseNos = honBranches.TryGetValue(branch, out var whseNos) && whseNos.Count > 0;
        var zones = hasWhseNos
            ? await pricingRepo.GetZonesForBranchWhseNosAsync(pricingFolder, whseNos!.ToList())
            : await pricingRepo.GetZonesForBranchAsync(pricingFolder);

        var descriptions = await pricingRepo.GetZoneDescriptionsAsync(pricingFolder);
        var summaries = await customerRepo.GetZoneCustomerSummariesAsync(pricingFolder, hasWhseNos ? whseNos!.ToList() : null);

        var options = zones.Select(z =>
        {
            var description = descriptions.GetValueOrDefault(z);
            var parts = new List<string> { z };
            if (!string.IsNullOrWhiteSpace(description)) parts.Add(description);
            if (summaries.TryGetValue(z, out var summary))
                parts.Add(summary.Count > 1 ? $"{summary.RepresentativeName} & {summary.Count - 1} more" : summary.RepresentativeName);
            return new ZoneOptionDto(z, description, string.Join(" - ", parts));
        }).ToList();

        return Ok(new { success = true, data = options });
    }

    [HttpPost("zone-preview")]
    public async Task<IActionResult> ZonePreview([FromBody] ExportZonePricelistRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Branch))
            return BadRequest(new { success = false, message = "A branch must be selected." });
        if (request.Zones.Count == 0)
            return BadRequest(new { success = false, message = "At least one zone must be selected." });
        if (await CheckBranchAccessAsync(request.Branch) is { } denied)
            return denied;

        var result = await zoneExportService.BuildAsync(request.Branch, request.Zones, request.EffectivityDate, request.SrpMarkupPercent);
        return Ok(new { success = true, data = result });
    }

    [HttpPost("zone-export")]
    public async Task<IActionResult> ZoneExport([FromBody] ExportZonePricelistRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Branch))
            return BadRequest(new { success = false, message = "A branch must be selected." });
        if (request.Zones.Count == 0)
            return BadRequest(new { success = false, message = "At least one zone must be selected." });
        if (await CheckBranchAccessAsync(request.Branch) is { } denied)
            return denied;

        var result = await zoneExportService.BuildAsync(request.Branch, request.Zones, request.EffectivityDate, request.SrpMarkupPercent);
        result = ApplySkuFilter(result, request.SkuFilter);
        var bytes = zoneExcelBuilder.Build(result);

        var fileName = $"ZonePricelist_{request.Branch}_{request.EffectivityDate:yyyyMMdd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpPost("grouped-preview")]
    public async Task<IActionResult> GroupedPreview([FromBody] ExportGroupedPricelistRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Branch))
            return BadRequest(new { success = false, message = "A branch must be selected." });
        if (await CheckBranchAccessAsync(request.Branch) is { } denied)
            return denied;

        try
        {
            var (result, _) = await groupedService.BuildAsync(
                request.Branch, request.EffectivityDate, request.SrpMarkupPercent, request.BaselineCustKey);
            return Ok(new { success = true, data = result });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("grouped-export")]
    public async Task<IActionResult> GroupedExport([FromBody] ExportGroupedPricelistRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Branch))
            return BadRequest(new { success = false, message = "A branch must be selected." });
        if (await CheckBranchAccessAsync(request.Branch) is { } denied)
            return denied;

        try
        {
            var (result, baseline) = await groupedService.BuildAsync(
                request.Branch, request.EffectivityDate, request.SrpMarkupPercent, request.BaselineCustKey);
            result = ApplySkuFilter(result, request.SkuFilter);

            var bytes = excelBuilder.Build(result);
            if (baseline is not null)
                bytes = groupedExcelBuilder.AddDiffSheets(bytes, result, baseline);

            var fileName = $"GroupedPricelist_{request.Branch}_{request.EffectivityDate:yyyyMMdd}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // Mirrors the frontend's live-preview filter (filteredRows/zoneFilteredRows:
    // SKU, description, or category header), so the exported file only
    // contains whatever the user was actually looking at.
    private static bool MatchesFilter(string term, string? header, string cProdNo, string prodDesc) =>
        (header?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
        cProdNo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        prodDesc.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static PricelistExportResult ApplySkuFilter(PricelistExportResult result, string? skuFilter)
    {
        if (string.IsNullOrWhiteSpace(skuFilter)) return result;
        var term = skuFilter.Trim();
        var groups = result.Groups
            .Select(g => new PricelistCategoryGroup(g.Header,
                g.Rows.Where(r => MatchesFilter(term, g.Header, r.CProdNo, r.ProdDesc)).ToList()))
            .Where(g => g.Rows.Count > 0)
            .ToList();
        return result with { Groups = groups };
    }

    private static ZonePricelistExportResult ApplySkuFilter(ZonePricelistExportResult result, string? skuFilter)
    {
        if (string.IsNullOrWhiteSpace(skuFilter)) return result;
        var term = skuFilter.Trim();
        var groups = result.Groups
            .Select(g => new ZonePricelistCategoryGroup(g.Header,
                g.Rows.Where(r => MatchesFilter(term, g.Header, r.CProdNo, r.ProdDesc)).ToList()))
            .Where(g => g.Rows.Count > 0)
            .ToList();
        return result with { Groups = groups };
    }
}
