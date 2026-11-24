using HOMSys.Application.DTOs.Tasaf;
using HOMSys.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>TPAR page: TPARs (Trade Promo Analysis Reports) entered against Approved TASAFs.</summary>
[ApiController]
[Route("api/tpars")]
[Authorize(Policy = "tpar")]
public class TparsController(TparService service) : ControllerBase
{
    /// <summary>Approved TASAFs (plus Completed ones when asked) with their TPAR total and balance.</summary>
    [HttpGet("tasafs")]
    public async Task<IActionResult> Tasafs([FromQuery] bool includeCompleted = false) =>
        Ok(new { success = true, data = await service.GetTasafsAsync(includeCompleted) });

    /// <summary>TPARs, newest first; tasafId narrows to one TASAF.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? tasafId) =>
        Ok(new { success = true, data = await service.GetListAsync(tasafId) });

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id) =>
        await service.GetAsync(id) is { } dto ? Ok(new { success = true, data = dto })
            : NotFound(new { success = false, message = TparService.NotFound });

    /// <summary>New TPAR (Draft).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveTparDto dto) =>
        Envelope(await service.CreateAsync(dto));

    /// <summary>Change a Draft TPAR.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveTparDto dto) =>
        Envelope(await service.UpdateAsync(id, dto));

    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> Submit(int id) => Envelope(await service.SubmitAsync(id));

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id) => Envelope(await service.ApproveAsync(id));

    /// <summary>Withdraw it (kept as Cancelled): frees its amount and its TPAR No.</summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id) => Envelope(await service.CancelAsync(id));

    /// <summary>Submitted / Approved → Draft (body: reason) so it can be changed.</summary>
    [HttpPost("{id:int}/revert")]
    public async Task<IActionResult> Revert(int id, [FromBody] TasafCommentDto dto) => Envelope(await service.RevertAsync(id, dto.Comment));

    private IActionResult Envelope((TparDto? dto, string? error) r) =>
        r.error is TparService.NotFound or TparService.TasafNotFound ? NotFound(new { success = false, message = r.error })
        : r.error is not null ? BadRequest(new { success = false, message = r.error })
        : Ok(new { success = true, data = r.dto });
}
