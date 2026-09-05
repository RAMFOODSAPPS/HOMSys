using HOMSys.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>Legacy DBF sync status + manual trigger, for the Master Data → Legacy Monitoring page.</summary>
[ApiController]
[Route("api/sync-status")]
[Authorize(Policy = "legacy-monitoring")]
public class SyncStatusController(SyncStatusService syncStatusService, ILogger<SyncStatusController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetStatus()
    {
        var status = await syncStatusService.GetStatusAsync();
        return Ok(new { success = true, data = status });
    }

    [HttpPost("pricing-sync")]
    public async Task<IActionResult> TriggerPricingSync()
    {
        try
        {
            var result = await syncStatusService.TriggerPricingSyncAsync(msg => logger.LogInformation("{Msg}", msg));
            return Ok(new { success = true, data = result.ToString() });
        }
        catch (SyncInProgressException ex)
        {
            return Conflict(new { success = false, message = ex.Message });
        }
    }
}
