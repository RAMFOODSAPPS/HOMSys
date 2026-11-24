using HOMSys.Application.DTOs.Collections;
using HOMSys.Infrastructure.Collections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>PDC › Returns awaiting RFC (user 2026-11-24): the CNC Verifier (pdc-confirm) or Finance Manager
/// (collections-manage) follows up the RFC posting of each "Return from customer" and links it; the receipt's OR / RPDC
/// goes to HOMS once every return on it has its RFC.</summary>
[ApiController]
[Route("api/rfc-follow-up")]
[Authorize(Policy = "rfc-follow-up")]
public class RfcFollowUpController(RfcFollowUpService rfc) : ControllerBase
{
    private CollectionUser Me => CollectionsController.From(User);

    /// <summary>linkedDays: also list returns linked in the last N days (0 = awaiting only).</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int linkedDays = 0) =>
        Ok(new { success = true, data = await rfc.ListAsync(Math.Clamp(linkedDays, 0, 90)) });

    /// <summary>Re-checks every awaiting return against the RFCs HOMSys has (done after each AROF4HO sync anyway).</summary>
    [HttpPost("auto-link")]
    public async Task<IActionResult> AutoLink() => Ok(new { success = true, data = await rfc.AutoLinkAsync() });

    [HttpPost("{deductionId:int}/link")]
    public async Task<IActionResult> Link(int deductionId, [FromBody] LinkRfcDto dto)
    {
        var error = await rfc.LinkAsync(deductionId, dto.RfcNo, Me.Name);
        return error is null ? Ok(new { success = true }) : BadRequest(new { success = false, message = error });
    }
}
