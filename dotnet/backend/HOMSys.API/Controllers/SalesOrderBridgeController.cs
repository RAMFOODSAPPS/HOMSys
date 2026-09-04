using HOMSys.Application.DTOs.SalesOrders;
using HOMSys.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>
/// Surface for the standalone Python SO write-back bridge (watcher\salesorder_bridge.py) —
/// not the encode UI. Authenticated by a static API key (X-Api-Key), same
/// convention as PricingController's /sync endpoint: the bridge is a headless
/// service account of one, not a logged-in user.
/// </summary>
[ApiController]
[Route("api/salesorders/bridge")]
[AllowAnonymous]
public class SalesOrderBridgeController(
    SalesOrderBridgeService bridgeService,
    IConfiguration config) : ControllerBase
{
    private bool IsAuthorized(string? apiKey)
    {
        var expected = config["HeadlessApiKey"];
        return !string.IsNullOrEmpty(expected) && apiKey == expected;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> Pending(
        [FromQuery] string branch,
        [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!IsAuthorized(apiKey))
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        if (string.IsNullOrWhiteSpace(branch))
            return BadRequest(new { success = false, message = "branch query parameter is required." });

        return Ok(new { success = true, data = await bridgeService.GetPendingAsync(branch) });
    }

    [HttpPost("{soId:int}/confirm")]
    public async Task<IActionResult> Confirm(
        int soId,
        [FromBody] BridgeConfirmDto dto,
        [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!IsAuthorized(apiKey))
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        var error = await bridgeService.ConfirmAsync(soId, dto.SoNo, dto.DocNo);
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true });
    }

    [HttpPost("{soId:int}/invoice")]
    public async Task<IActionResult> Invoice(
        int soId,
        [FromBody] BridgeInvoiceDto dto,
        [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!IsAuthorized(apiKey))
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        var error = await bridgeService.ConfirmInvoiceAsync(soId, dto.InvNo, dto.InvDate, dto.InvAmt);
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true });
    }

    [HttpPost("{soId:int}/oos-status")]
    public async Task<IActionResult> OosStatus(
        int soId,
        [FromBody] BridgeOosStatusDto dto,
        [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!IsAuthorized(apiKey))
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        var error = await bridgeService.SyncOosStatusAsync(soId, dto);
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true });
    }

    [HttpPost("{soId:int}/deallocate")]
    public async Task<IActionResult> Deallocate(
        int soId,
        [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!IsAuthorized(apiKey))
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        var error = await bridgeService.DeallocateAsync(soId);
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true });
    }

    [HttpPost("{soId:int}/lock")]
    public async Task<IActionResult> Lock(
        int soId,
        [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!IsAuthorized(apiKey))
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        var error = await bridgeService.LockAsync(soId);
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true });
    }

    [HttpGet("resync-pending")]
    public async Task<IActionResult> ResyncPending(
        [FromQuery] string branch,
        [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!IsAuthorized(apiKey))
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        if (string.IsNullOrWhiteSpace(branch))
            return BadRequest(new { success = false, message = "branch query parameter is required." });

        return Ok(new { success = true, data = await bridgeService.GetResyncPendingAsync(branch) });
    }

    [HttpPost("{soId:int}/resync-confirm")]
    public async Task<IActionResult> ResyncConfirm(
        int soId,
        [FromBody] BridgeResyncConfirmDto dto,
        [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!IsAuthorized(apiKey))
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        var error = await bridgeService.ConfirmResyncAsync(soId, dto.Ok);
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true });
    }
}
