using HOMSys.Application.DTOs.Tasaf;
using HOMSys.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>
/// No-login approve / reject from an approval email. The one-time token in the link stands in
/// for the approver (only its SHA-256 is stored, on their approval step) and works only while
/// it's still their turn. GET never changes anything (mail scanners open links); only the
/// POST with a comment decides.
/// </summary>
[ApiController]
[Route("api/tasaf-email")]
[AllowAnonymous]
public class TasafEmailController(TasafService service) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<IActionResult> Get(string token)
    {
        var (dto, error) = await service.GetEmailDecisionAsync(token);
        return dto is null ? NotFound(new { success = false, message = error }) : Ok(new { success = true, data = dto });
    }

    [HttpPost("{token}/decide")]
    public async Task<IActionResult> Decide(string token, [FromBody] TasafDecisionDto body)
    {
        var (dto, error) = await service.EmailDecideAsync(token, body);
        return error is not null ? BadRequest(new { success = false, message = error }) : Ok(new { success = true, data = dto });
    }
}
