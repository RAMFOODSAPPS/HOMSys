using HOMSys.Application.DTOs.Collections;
using HOMSys.Infrastructure.Collections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>PDC module: maturity report (pdc-view), Succeeded / Bounced tagging (pdc-tag), recording a matured PDC's
/// deposit, and the CNC Verifier's confirmation of its bank match (pdc-confirm — the only trigger of the PDC's JV).</summary>
[ApiController]
[Route("api/pdc")]
[Authorize(Policy = "pdc-view")]
public class PdcController(CollectionService collections, BankStatementService bank) : ControllerBase
{
    private CollectionUser Me => CollectionsController.From(User);

    private IActionResult Result((CollectionDto? data, string? error) r) =>
        r.error is null ? Ok(new { success = true, data = r.data }) : BadRequest(new { success = false, message = r.error });

    /// <summary>nearDays: how many days ahead counts as "near maturity" (default 7).</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? siteId, [FromQuery] int nearDays = 7) =>
        Ok(new { success = true, data = await collections.ListPdcAsync(Me, siteId, Math.Clamp(nearDays, 1, 90)) });

    /// <summary>Branch filter (CNC Verifiers have no collections access, so the PDC Monitor asks here).</summary>
    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> History(int id)
    {
        var (data, error) = await collections.PdcHistoryAsync(Me, id);
        return error is null ? Ok(new { success = true, data }) : NotFound(new { success = false, message = error });
    }

    [HttpGet("sites")]
    public async Task<IActionResult> Sites() => Ok(new { success = true, data = await collections.GetMySitesAsync(Me) });

    [HttpPost("{id:int}/tag"), Authorize(Policy = "pdc-tag")]
    public async Task<IActionResult> Tag(int id, [FromBody] PdcTagDto dto) => Result(await collections.TagPdcAsync(Me, id, dto));

    /// <summary>Accounts the PDC's branch deposits into. Who may record the deposit is checked when it's saved.</summary>
    [HttpGet("deposit-accounts")]
    public async Task<IActionResult> DepositAccounts([FromQuery] int siteId) => Ok(new { success = true, data = await bank.DepositOptionsAsync(siteId) });

    [HttpPost("{id:int}/deposit")]
    public async Task<IActionResult> RecordDeposit(int id, [FromBody] DepositInfoDto dto) => Result(await collections.RecordDepositAsync(Me, id, dto));

    [HttpPost("{id:int}/confirm-match"), Authorize(Policy = "pdc-confirm")]
    public async Task<IActionResult> ConfirmMatch(int id) => Result(await collections.ConfirmPdcMatchAsync(Me, id));

    [HttpPost("{id:int}/reject-match"), Authorize(Policy = "pdc-confirm")]
    public async Task<IActionResult> RejectMatch(int id, [FromBody] ReasonDto dto) => Result(await collections.RejectPdcMatchAsync(Me, id, dto?.Reason));

    // ── bounced PDC follow-up (pdc-tag): Letter of Dishonor -> settled by a new CR, or endorsed for legal action ──

    [HttpGet("lod-settings"), Authorize(Policy = "pdc-tag")]
    public IActionResult LodSettings() => Ok(new { success = true, data = new { settleDays = collections.LodSettleDaysDefault } });

    [HttpPost("{id:int}/lod"), Authorize(Policy = "pdc-tag")]
    public async Task<IActionResult> IssueLod(int id, [FromBody] IssueLodDto dto) => Result(await collections.IssueLodAsync(Me, id, dto));

    [HttpGet("{id:int}/lod"), Authorize(Policy = "pdc-tag")]
    public async Task<IActionResult> LodLetter(int id)
    {
        var (data, error) = await collections.LodLetterAsync(Me, id);
        return error is null ? Ok(new { success = true, data }) : BadRequest(new { success = false, message = error });
    }

    /// <summary>Multipart: date (yyyy-MM-dd) + file (the customer's received copy).</summary>
    [HttpPost("{id:int}/lod/received"), Authorize(Policy = "pdc-tag")]
    [RequestSizeLimit(CollectionService.MaxAttachmentBytes + 1024 * 1024)]
    public async Task<IActionResult> LodReceived(int id, [FromForm] DateOnly? date, IFormFile? file)
    {
        await using var stream = file?.OpenReadStream();
        return Result(await collections.RecordLodReceivedAsync(Me, id, date, file?.FileName, stream, file?.Length ?? 0));
    }

    [HttpGet("{id:int}/lod/copy"), Authorize(Policy = "pdc-tag")]
    public async Task<IActionResult> LodCopy(int id)
    {
        var file = await collections.OpenLodCopyAsync(Me, id);
        if (file is null) return NotFound(new { success = false, message = "No received copy on file." });
        Response.Headers.ContentDisposition = $"inline; filename=\"{file.Value.fileName.Replace("\"", "")}\"";
        return File(file.Value.stream, file.Value.contentType);
    }

    [HttpGet("{id:int}/settlement-candidates"), Authorize(Policy = "pdc-tag")]
    public async Task<IActionResult> SettlementCandidates(int id)
    {
        var (data, error) = await collections.SettlementCandidatesAsync(Me, id);
        return error is null ? Ok(new { success = true, data }) : BadRequest(new { success = false, message = error });
    }

    [HttpPost("{id:int}/settle"), Authorize(Policy = "pdc-tag")]
    public async Task<IActionResult> Settle(int id, [FromBody] SettleBouncedDto dto) => Result(await collections.SettleBouncedAsync(Me, id, dto));

    [HttpPost("{id:int}/unsettle"), Authorize(Policy = "pdc-tag")]
    public async Task<IActionResult> Unsettle(int id, [FromBody] ReasonDto dto) => Result(await collections.UnsettleBouncedAsync(Me, id, dto?.Reason));

    [HttpPost("{id:int}/legal"), Authorize(Policy = "pdc-tag")]
    public async Task<IActionResult> EndorseLegal(int id, [FromBody] EndorseLegalDto dto) => Result(await collections.EndorseLegalAsync(Me, id, dto));
}
