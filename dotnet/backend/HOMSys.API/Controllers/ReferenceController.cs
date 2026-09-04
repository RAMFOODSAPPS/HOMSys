using HOMSys.Application.DTOs.Monitoring;
using HOMSys.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>
/// Headless sync trigger for reference data (Customers/Products from
/// \\Acastillano\setup\ADC\BMSRAM), for the same Python watcher that already
/// hits PricingController.Sync (see watcher\legacy_master_watcher.py) — add
/// this endpoint's URL to HOMSYS_SYNC_URLS to enable it. Authenticated by
/// the same static API key as PricingController.Sync, not JWT. Goes through
/// SyncStatusService (not ReferenceDataImporter directly) so it shares the
/// same in-progress mutex as the manual "Sync Now" button.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ReferenceController(
    SyncStatusService syncStatusService,
    IConfiguration config,
    ILogger<ReferenceController> logger) : ControllerBase
{
    [HttpPost("sync")]
    [AllowAnonymous]
    public async Task<IActionResult> Sync([FromHeader(Name = "X-Api-Key")] string? apiKey, [FromBody] SyncOverrideRequest? request)
    {
        var expected = config["HeadlessApiKey"];
        if (string.IsNullOrEmpty(expected) || apiKey != expected)
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        try
        {
            var result = await syncStatusService.TriggerReferenceSyncAsync(msg => logger.LogInformation("{Msg}", msg), request?.Path);
            return Ok(new { success = true, data = result.ToString() });
        }
        catch (SyncInProgressException ex)
        {
            return Conflict(new { success = false, message = ex.Message });
        }
    }
}
