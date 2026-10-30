using HOMSys.Application.DTOs.Collections;
using HOMSys.Infrastructure.Collections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>
/// Remittance Advice (NKA): encoded by the Custodian Verifier (collections-ra-encode), verified / returned as a whole by
/// the Finance Verifier (collections-verify). A CR's OR soft copy goes through the normal collection attachment endpoints.
/// </summary>
[ApiController]
[Route("api/collections/remittances")]
[Authorize(Policy = "collections-any")]
public class RemittancesController(CollectionService collections) : ControllerBase
{
    private CollectionUser Me => CollectionsController.From(User);

    private IActionResult Result<T>((T? data, string? error) r) =>
        r.error is null ? Ok(new { success = true, data = r.data }) : BadRequest(new { success = false, message = r.error });

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status) =>
        Ok(new { success = true, data = await collections.ListRemittancesAsync(Me, status) });

    [HttpGet("head-office")]
    public async Task<IActionResult> HeadOffice() => Ok(new { success = true, data = await collections.HeadOfficeAsync(Me) });

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var dto = await collections.GetRemittanceAsync(Me, id);
        return dto is null ? NotFound(new { success = false, message = "Remittance Advice not found." }) : Ok(new { success = true, data = dto });
    }

    [HttpPost, Authorize(Policy = "collections-ra-encode")]
    public async Task<IActionResult> Create([FromBody] SaveRemittanceDto dto) => Result(await collections.CreateRemittanceAsync(Me, dto));

    [HttpPut("{id:int}"), Authorize(Policy = "collections-ra-encode")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveRemittanceDto dto) => Result(await collections.UpdateRemittanceAsync(Me, id, dto));

    /// <summary>Adds a CR from its RA Excel: multipart file + receiptNo (OR no.) + dcrNo + optional receiptDate.</summary>
    [HttpPost("{id:int}/crs"), Authorize(Policy = "collections-ra-encode")]
    [RequestSizeLimit(CollectionService.MaxAttachmentBytes + 1024 * 1024)]
    public Task<IActionResult> AddCr(int id, IFormFile? file, [FromForm] string? receiptNo, [FromForm] string? dcrNo, [FromForm] DateOnly? receiptDate) =>
        Upload(id, null, file, receiptNo, dcrNo, receiptDate);

    /// <summary>Replaces a CR's RA Excel (its lines and acceptances are rebuilt from the new file).</summary>
    [HttpPost("{id:int}/crs/{crId:int}/excel"), Authorize(Policy = "collections-ra-encode")]
    [RequestSizeLimit(CollectionService.MaxAttachmentBytes + 1024 * 1024)]
    public Task<IActionResult> ReplaceExcel(int id, int crId, IFormFile? file, [FromForm] string? receiptNo, [FromForm] string? dcrNo, [FromForm] DateOnly? receiptDate) =>
        Upload(id, crId, file, receiptNo, dcrNo, receiptDate);

    private async Task<IActionResult> Upload(int id, int? crId, IFormFile? file, string? receiptNo, string? dcrNo, DateOnly? receiptDate)
    {
        if (file is null) return BadRequest(new { success = false, message = "Choose the RA Excel file." });
        await using var stream = file.OpenReadStream();
        return Result(await collections.UploadRemittanceCrAsync(Me, id, crId, new SaveRemittanceCrDto(receiptNo, dcrNo, receiptDate),
            file.FileName, stream, file.Length));
    }

    [HttpPut("{id:int}/crs/{crId:int}"), Authorize(Policy = "collections-ra-encode")]
    public async Task<IActionResult> UpdateCr(int id, int crId, [FromBody] SaveRemittanceCrDto dto) =>
        Result(await collections.UpdateRemittanceCrAsync(Me, id, crId, dto));

    [HttpDelete("{id:int}/crs/{crId:int}"), Authorize(Policy = "collections-ra-encode")]
    public async Task<IActionResult> RemoveCr(int id, int crId) => Result(await collections.RemoveRemittanceCrAsync(Me, id, crId));

    /// <summary>Re-checks every line (the page calls it while invoices are being fetched from HO).</summary>
    [HttpPost("{id:int}/rematch"), Authorize(Policy = "collections-ra-encode")]
    public async Task<IActionResult> Rematch(int id) => Result(await collections.RematchRemittanceAsync(Me, id));

    [HttpPost("{id:int}/submit"), Authorize(Policy = "collections-ra-encode")]
    public async Task<IActionResult> Submit(int id) => Result(await collections.SubmitRemittanceAsync(Me, id));

    [HttpPost("{id:int}/verify"), Authorize(Policy = "collections-verify")]
    public async Task<IActionResult> Verify(int id) => Result(await collections.VerifyRemittanceAsync(Me, id));

    [HttpPost("{id:int}/return"), Authorize(Policy = "collections-verify")]
    public async Task<IActionResult> Return(int id, [FromBody] ReasonDto dto) => Result(await collections.ReturnRemittanceAsync(Me, id, dto?.Reason));

    /// <summary>Custodian Verifier: Draft / Returned; Finance Manager: any (service enforces).</summary>
    [HttpPost("{id:int}/void")]
    public async Task<IActionResult> Void(int id, [FromBody] ReasonDto dto) => Result(await collections.VoidRemittanceAsync(Me, id, dto?.Reason));
}
