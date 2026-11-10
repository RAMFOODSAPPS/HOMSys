using System.Security.Claims;
using HOMSys.Application.DTOs.Rdp;
using HOMSys.Infrastructure.Data;
using HOMSys.Infrastructure.Rdp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.API.Controllers;

/// <summary>
/// RDP (RAM Distributor Partner) sales: distributor file uploads, the RDP list and channel mapping (rdp-upload),
/// and the UBA (unique buying accounts) dashboard (rdp-uba).
/// </summary>
[ApiController]
[Route("api/rdp")]
[Authorize]
public class RdpController(RdpSalesService rdp, RdpUbaService uba, AppDbContext db) : ControllerBase
{
    private string Me => User.FindFirstValue(ClaimTypes.Name) ?? "";

    private IActionResult Result<T>((T? data, string? error) r) =>
        r.error is null ? Ok(new { success = true, data = r.data }) : BadRequest(new { success = false, message = r.error });

    // ── UBA dashboard ───────────────────────────────────────────────────────

    [HttpGet("uba/filters"), Authorize(Policy = "rdp-uba")]
    public async Task<IActionResult> Filters() => Ok(new { success = true, data = await uba.FiltersAsync(rdp) });

    [HttpPost("uba/summary"), Authorize(Policy = "rdp-uba")]
    public async Task<IActionResult> Summary([FromBody] UbaFilterDto filter) => Ok(new { success = true, data = await uba.SummaryAsync(filter) });

    [HttpPost("uba/split"), Authorize(Policy = "rdp-uba")]
    public async Task<IActionResult> Split([FromBody] UbaSplitRequestDto req) => Result(await uba.SplitAsync(req));

    // ── uploads ─────────────────────────────────────────────────────────────

    /// <summary>Step 1: what the file would import and replace. Nothing is saved.</summary>
    [HttpPost("uploads/preview"), Authorize(Policy = "rdp-upload")]
    [RequestSizeLimit(RdpSalesService.MaxFileBytes + 1024 * 1024)]
    public async Task<IActionResult> Preview([FromForm] int? distributorId, IFormFile? file)
    {
        if (file is null) return BadRequest(new { success = false, message = "Choose the distributor's sales file." });
        await using var stream = file.OpenReadStream();
        return Result(await rdp.PreviewAsync(distributorId, file.FileName, stream, file.Length));
    }

    /// <summary>Step 2: import. Replaces each distributor's months that the file contains.</summary>
    [HttpPost("uploads"), Authorize(Policy = "rdp-upload")]
    [RequestSizeLimit(RdpSalesService.MaxFileBytes + 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] int? distributorId, IFormFile? file)
    {
        if (file is null) return BadRequest(new { success = false, message = "Choose the distributor's sales file." });
        await using var stream = file.OpenReadStream();
        return Result(await rdp.UploadAsync(distributorId, file.FileName, stream, file.Length, Me));
    }

    [HttpGet("uploads"), Authorize(Policy = "rdp-upload")]
    public async Task<IActionResult> Uploads() => Ok(new { success = true, data = await rdp.ListUploadsAsync() });

    [HttpDelete("uploads/{id:int}"), Authorize(Policy = "rdp-upload")]
    public async Task<IActionResult> DeleteUpload(int id) => Result(await rdp.DeleteUploadAsync(id));

    /// <summary>Imported lines in the template's columns, paged.</summary>
    [HttpGet("lines"), Authorize(Policy = "rdp-upload")]
    public async Task<IActionResult> Lines([FromQuery] int? uploadId, [FromQuery] int? distributorId, [FromQuery] string? period,
        [FromQuery] string? search, [FromQuery] int skip = 0, [FromQuery] int take = 50)
        => Result(await rdp.ListLinesAsync(uploadId, distributorId, period, search, skip, take));

    // ── RDP list ────────────────────────────────────────────────────────────

    [HttpGet("distributors"), Authorize(Policy = "rdp-upload")]
    public async Task<IActionResult> Distributors() => Ok(new { success = true, data = await rdp.ListDistributorsAsync() });

    [HttpPost("distributors"), Authorize(Policy = "rdp-upload")]
    public async Task<IActionResult> CreateDistributor([FromBody] SaveRdpDistributorDto dto) => Result(await rdp.SaveDistributorAsync(null, dto, Me));

    [HttpPut("distributors/{id:int}"), Authorize(Policy = "rdp-upload")]
    public async Task<IActionResult> UpdateDistributor(int id, [FromBody] SaveRdpDistributorDto dto) => Result(await rdp.SaveDistributorAsync(id, dto, Me));

    /// <summary>Servicing-branch picklist for the RDP list (the Sites page needs its own permission).</summary>
    [HttpGet("sites"), Authorize(Policy = "rdp-upload")]
    public async Task<IActionResult> Sites() => Ok(new
    {
        success = true,
        data = await db.Sites.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).Select(s => new { s.Id, s.Name, s.Code }).ToListAsync(),
    });

    // ── channel mapping ─────────────────────────────────────────────────────

    [HttpGet("channels"), Authorize(Policy = "rdp-upload")]
    public async Task<IActionResult> Channels() => Ok(new { success = true, data = await rdp.ListChannelsAsync() });

    [HttpPut("channels"), Authorize(Policy = "rdp-upload")]
    public async Task<IActionResult> SaveChannel([FromBody] SaveRdpChannelMapDto dto) => Result(await rdp.SaveChannelMapAsync(dto, Me));

    [HttpDelete("channels/{id:int}"), Authorize(Policy = "rdp-upload")]
    public async Task<IActionResult> DeleteChannel(int id) => Result(await rdp.DeleteChannelMapAsync(id));
}
