using HOMSys.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>
/// Called by the legacy fm01 (Customer File Maintenance) form at the branch when
/// the Withholding Tax checkbox changes. fm01 calls production directly with the
/// URL and X-Api-Key compiled in (no config.json).
/// </summary>
[ApiController]
[Route("api/customers/withholding")]
[AllowAnonymous]
public class CustomerWithholdingController(
    CustomerWithholdingService withholdingService,
    IConfiguration config) : ControllerBase
{
    // fm01 carries this key compiled into every branch's BMS exe, so it gets its
    // own key when WithholdingApiKey is set: a leaked fm01 key can then only
    // flip this flag, not reach the masters sync or SO bridge.
    private bool IsAuthorized(string? apiKey)
    {
        var expected = config["WithholdingApiKey"];
        if (string.IsNullOrEmpty(expected))
            expected = config["HeadlessApiKey"];
        return !string.IsNullOrEmpty(expected) && apiKey == expected;
    }

    /// <summary>POST api/customers/withholding?custKey=2100226&amp;withholding=true&amp;updatedBy=USER@PC
    /// — 404 when the customer isn't in HOMSys, so fm01 blocks the save.</summary>
    [HttpPost]
    public async Task<IActionResult> Set(
        [FromQuery] string? custKey,
        [FromQuery] bool withholding,
        [FromQuery] string? updatedBy,
        [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!IsAuthorized(apiKey))
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });

        if (string.IsNullOrWhiteSpace(custKey))
            return BadRequest(new { success = false, message = "custKey is required." });

        var (dto, error) = await withholdingService.SetAsync(custKey, withholding, updatedBy);
        if (dto is null)
            return NotFound(new { success = false, message = error });

        return Ok(new { success = true, data = dto });
    }
}
