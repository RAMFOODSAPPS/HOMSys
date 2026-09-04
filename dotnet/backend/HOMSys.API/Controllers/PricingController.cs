using HOMSys.Application.DTOs.Pricing;
using HOMSys.Application.Services;
using HOMSys.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>Display-only price quotes for the Sales Order encode grid.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PricingController(
    PriceCalculationService pricingService,
    PricingDeltaImporter pricingDeltaImporter,
    IConfiguration config,
    ILogger<PricingController> logger) : ControllerBase
{
    [HttpGet("quote")]
    public async Task<IActionResult> Quote([FromQuery] string cProdNo, [FromQuery] string? custKey)
    {
        if (string.IsNullOrWhiteSpace(cProdNo))
            return BadRequest(new { success = false, message = "cProdNo is required." });

        var quote = await pricingService.GetQuoteAsync(cProdNo, custKey);
        return Ok(new { success = true, data = quote });
    }

    /// <summary>
    /// Applies a pricing delta computed and sent by LegacyMasterWatcher.exe,
    /// which reads and parses F:\ itself (Azure can't reach it) and diffs
    /// against its own local snapshot — this only ever contains rows that
    /// changed since the last run, so it's safe to hit this on a short
    /// interval. Authenticated by a static API key (X-Api-Key header)
    /// instead of JWT — the watcher is a headless service account of one,
    /// not a logged-in user, so it skips the login/refresh flow entirely.
    /// </summary>
    [HttpPost("/api/masters/sync")]
    [AllowAnonymous]
    [DisableRequestSizeLimit] // first-run baseline (no prior snapshot) sends every row nationwide — tens of MB, over Kestrel's ~30MB default
    public async Task<IActionResult> Sync([FromHeader(Name = "X-Api-Key")] string? apiKey, [FromBody] PricingSyncDeltaRequest? request)
    {
        var expected = config["HeadlessApiKey"];
        if (string.IsNullOrEmpty(expected) || apiKey != expected)
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        request ??= new PricingSyncDeltaRequest(null, null, null, null, null, null);
        var result = await pricingDeltaImporter.ApplyAsync(request, msg => logger.LogInformation("{Msg}", msg));
        return Ok(new { success = true, data = result.ToString() });
    }
}
