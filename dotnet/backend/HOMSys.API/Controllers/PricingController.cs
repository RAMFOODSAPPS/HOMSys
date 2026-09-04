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
    PricingDataImporter pricingImporter,
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
    /// Re-check F:\ for changes and re-import if anything is newer than the
    /// last sync. Called on a timer by the standalone Python watcher service
    /// (see watcher\pricing_sync_watcher.py) — a call that finds nothing new
    /// just returns fast, so it's safe to hit this on a short interval.
    /// Authenticated by a static API key (X-Api-Key header) instead of JWT —
    /// the watcher is a headless service account of one, not a logged-in
    /// user, so it skips the login/refresh flow entirely.
    /// </summary>
    [HttpPost("sync")]
    [AllowAnonymous]
    public async Task<IActionResult> Sync([FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        var expected = config["HeadlessApiKey"];
        if (string.IsNullOrEmpty(expected) || apiKey != expected)
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        var result = await pricingImporter.ImportAllAsync(PricingDataImporter.DefaultRoot, msg => logger.LogInformation("{Msg}", msg));
        return Ok(new { success = true, data = result.ToString() });
    }
}
