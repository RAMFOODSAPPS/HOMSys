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
}

public class ExportZonePricelistRequest
{
    public string Branch { get; set; } = "";
    public List<string> Zones { get; set; } = [];
    public DateOnly EffectivityDate { get; set; }
    public decimal SrpMarkupPercent { get; set; } = 3m;
}

public class ExportGroupedPricelistRequest
{
    public string Branch { get; set; } = "";
    public DateOnly EffectivityDate { get; set; }
    public decimal SrpMarkupPercent { get; set; } = 3m;
    public string? BaselineCustKey { get; set; }
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
    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches()
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

        var expanded = independentOptions.Concat(honOptions)
            .OrderBy(b => b.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Ok(new { success = true, data = expanded });
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] ExportPricelistRequest request)
    {
        if (request.CustKeys.Count == 0)
            return BadRequest(new { success = false, message = "At least one customer must be selected." });

        var result = await exportService.BuildAsync(request.CustKeys, request.EffectivityDate, request.SrpMarkupPercent);
        return Ok(new { success = true, data = result });
    }

    [HttpPost("export")]
    public async Task<IActionResult> Export([FromBody] ExportPricelistRequest request)
    {
        if (request.CustKeys.Count == 0)
            return BadRequest(new { success = false, message = "At least one customer must be selected." });

        var result = await exportService.BuildAsync(request.CustKeys, request.EffectivityDate, request.SrpMarkupPercent);
        var bytes = excelBuilder.Build(result);

        var fileName = $"Pricelist_{request.EffectivityDate:yyyyMMdd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("zone-branches/{branch}/zones")]
    public async Task<IActionResult> GetZonesForBranch(string branch)
    {
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

        var result = await zoneExportService.BuildAsync(request.Branch, request.Zones, request.EffectivityDate, request.SrpMarkupPercent);
        var bytes = zoneExcelBuilder.Build(result);

        var fileName = $"ZonePricelist_{request.Branch}_{request.EffectivityDate:yyyyMMdd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpPost("grouped-preview")]
    public async Task<IActionResult> GroupedPreview([FromBody] ExportGroupedPricelistRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Branch))
            return BadRequest(new { success = false, message = "A branch must be selected." });

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

        try
        {
            var (result, baseline) = await groupedService.BuildAsync(
                request.Branch, request.EffectivityDate, request.SrpMarkupPercent, request.BaselineCustKey);

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
}
