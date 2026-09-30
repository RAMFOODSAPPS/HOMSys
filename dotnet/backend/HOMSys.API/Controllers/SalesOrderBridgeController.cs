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

    /// <summary>claimant = this bridge PC: orders it already claimed stay listed (its own
    /// crash recovery); orders claimed by another PC are hidden.</summary>
    [HttpGet("pending")]
    public async Task<IActionResult> Pending(
        [FromQuery] string branch,
        [FromQuery] string? claimant,
        [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!IsAuthorized(apiKey))
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        if (string.IsNullOrWhiteSpace(branch))
            return BadRequest(new { success = false, message = "branch query parameter is required." });

        return Ok(new { success = true, data = await bridgeService.GetPendingAsync(branch, claimant) });
    }

    /// <summary>Reserve an order before taking a SO# from docnum.dbf. 409 = another PC
    /// holds it or it is already in BMS: skip it.</summary>
    [HttpPost("{soId:int}/claim")]
    public async Task<IActionResult> Claim(
        int soId,
        [FromQuery] string branch,
        [FromBody] BridgeClaimDto dto,
        [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!IsAuthorized(apiKey))
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        if (string.IsNullOrWhiteSpace(branch))
            return BadRequest(new { success = false, message = "branch query parameter is required." });

        var (found, conflict, error) = await bridgeService.ClaimAsync(soId, branch, dto.ClaimedBy);
        if (!found)
            return NotFound(new { success = false, message = error });
        if (conflict)
            return Conflict(new { success = false, message = error });

        return Ok(new { success = true });
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

    [HttpPost("by-sono/{soNo:int}/delivery")]
    public async Task<IActionResult> Delivery(
        int soNo,
        [FromQuery] string branch,
        [FromBody] BridgeDeliveryDto dto,
        [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!IsAuthorized(apiKey))
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        if (string.IsNullOrWhiteSpace(branch))
            return BadRequest(new { success = false, message = "branch query parameter is required." });

        var (found, error) = await bridgeService.ConfirmDeliveryAsync(soNo, branch, dto);
        if (!found)
            return NotFound(new { success = false, message = error });
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true });
    }

    // ── by-sono variants of the soId endpoints above ────────────────────────
    // Used by the bridge's outbox (salesorder_bridge.py) instead of its old
    // per-workstation ledger lookup. 404 = not a HOMSys order (BMS fires these
    // for every order it processes/deallocates/invoices, most aren't ours), which
    // the outbox drops quietly. The soId routes stay for bridges not yet upgraded.

    [HttpPost("by-sono/{soNo:int}/lock")]
    public Task<IActionResult> LockBySoNo(int soNo, [FromQuery] string branch,
        [FromHeader(Name = "X-Api-Key")] string? apiKey) =>
        BySoNo(soNo, branch, apiKey, bridgeService.LockAsync);

    [HttpPost("by-sono/{soNo:int}/deallocate")]
    public Task<IActionResult> DeallocateBySoNo(int soNo, [FromQuery] string branch,
        [FromHeader(Name = "X-Api-Key")] string? apiKey) =>
        BySoNo(soNo, branch, apiKey, bridgeService.DeallocateAsync);

    [HttpPost("by-sono/{soNo:int}/invoice")]
    public Task<IActionResult> InvoiceBySoNo(int soNo, [FromQuery] string branch, [FromBody] BridgeInvoiceDto dto,
        [FromHeader(Name = "X-Api-Key")] string? apiKey) =>
        BySoNo(soNo, branch, apiKey, soId => bridgeService.ConfirmInvoiceAsync(soId, dto.InvNo, dto.InvDate, dto.InvAmt));

    [HttpPost("by-sono/{soNo:int}/oos-status")]
    public Task<IActionResult> OosStatusBySoNo(int soNo, [FromQuery] string branch, [FromBody] BridgeOosStatusDto dto,
        [FromHeader(Name = "X-Api-Key")] string? apiKey) =>
        BySoNo(soNo, branch, apiKey, soId => bridgeService.SyncOosStatusAsync(soId, dto));

    private async Task<IActionResult> BySoNo(int soNo, string branch, string? apiKey, Func<int, Task<string?>> action)
    {
        if (!IsAuthorized(apiKey))
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        if (string.IsNullOrWhiteSpace(branch))
            return BadRequest(new { success = false, message = "branch query parameter is required." });

        var soId = await bridgeService.FindSoIdBySoNoAsync(soNo, branch);
        if (soId is null)
            return NotFound(new { success = false, message = $"No sales order found for SO {soNo} in branch {branch}." });

        var error = await action(soId.Value);
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true });
    }

    // ── Offshore hand-off (origin BMS -> HOMSys -> ForBranch BMS) ───────────

    [HttpGet("offshore-awaiting")]
    public async Task<IActionResult> OffshoreAwaiting(
        [FromQuery] string branch,
        [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!IsAuthorized(apiKey))
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        if (string.IsNullOrWhiteSpace(branch))
            return BadRequest(new { success = false, message = "branch query parameter is required." });

        return Ok(new { success = true, data = await bridgeService.GetOffshoreAwaitingAsync(branch) });
    }

    /// <summary>409 = the target already has this SO#; the outbox parks it in failed\.</summary>
    [HttpPost("by-sono/{soNo:int}/offshore-upload")]
    public async Task<IActionResult> OffshoreUpload(
        int soNo,
        [FromQuery] string branch,
        [FromBody] BridgeOffshoreUploadDto dto,
        [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!IsAuthorized(apiKey))
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        if (string.IsNullOrWhiteSpace(branch))
            return BadRequest(new { success = false, message = "branch query parameter is required." });

        var (found, conflict, error) = await bridgeService.UploadOffshoreAsync(soNo, branch, dto);
        if (!found)
            return NotFound(new { success = false, message = error });
        if (conflict)
            return Conflict(new { success = false, message = error });

        return Ok(new { success = true });
    }

    [HttpGet("offshore-inbound")]
    public async Task<IActionResult> OffshoreInbound(
        [FromQuery] string branch,
        [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!IsAuthorized(apiKey))
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        if (string.IsNullOrWhiteSpace(branch))
            return BadRequest(new { success = false, message = "branch query parameter is required." });

        return Ok(new { success = true, data = await bridgeService.GetOffshoreInboundAsync(branch) });
    }

    [HttpPost("by-sono/{soNo:int}/offshore-received")]
    public Task<IActionResult> OffshoreReceived(int soNo, [FromQuery] string branch, [FromBody] BridgeOffshoreReceivedDto dto,
        [FromHeader(Name = "X-Api-Key")] string? apiKey) =>
        BySoNo(soNo, branch, apiKey, soId => bridgeService.ConfirmOffshoreReceivedAsync(soId, dto));

    /// <summary>Posted BMS RFCs per invoice (merged) — see SalesOrderBridgeService.SyncRfcsAsync.
    /// Always 200 with the invoices that matched a HOMSys order.</summary>
    [HttpPost("invoice-rfcs")]
    public async Task<IActionResult> InvoiceRfcs(
        [FromQuery] string branch,
        [FromBody] BridgeRfcSyncDto dto,
        [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!IsAuthorized(apiKey))
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        if (string.IsNullOrWhiteSpace(branch))
            return BadRequest(new { success = false, message = "branch query parameter is required." });

        var matchedInvNos = await bridgeService.SyncRfcsAsync(branch, dto);
        return Ok(new { success = true, matched = matchedInvNos.Count, matchedInvNos });
    }

    /// <summary>Bridge heartbeat — see SalesOrderBridgeService.HeartbeatAsync.</summary>
    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat(
        [FromQuery] string branch,
        [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!IsAuthorized(apiKey))
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        if (string.IsNullOrWhiteSpace(branch))
            return BadRequest(new { success = false, message = "branch query parameter is required." });

        await bridgeService.HeartbeatAsync(branch);
        return Ok(new { success = true });
    }

    [HttpGet("reconcile-candidates")]
    public async Task<IActionResult> ReconcileCandidates(
        [FromQuery] string branch,
        [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!IsAuthorized(apiKey))
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        if (string.IsNullOrWhiteSpace(branch))
            return BadRequest(new { success = false, message = "branch query parameter is required." });

        return Ok(new { success = true, data = await bridgeService.GetReconcileCandidatesAsync(branch) });
    }

    [HttpPost("invoice-cancel")]
    public async Task<IActionResult> InvoiceCancel(
        [FromQuery] string branch,
        [FromBody] BridgeInvoiceCancelDto dto,
        [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!IsAuthorized(apiKey))
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        if (string.IsNullOrWhiteSpace(branch))
            return BadRequest(new { success = false, message = "branch query parameter is required." });

        var (found, error) = await bridgeService.CancelInvoiceAsync(branch, dto);
        if (!found)
            return NotFound(new { success = false, message = error });
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
