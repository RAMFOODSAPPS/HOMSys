using System.Security.Claims;
using HOMSys.Application.DTOs.Collections;
using HOMSys.Infrastructure.Collections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>Branch collection encoding (Invoice Custodian). Finance read access via collections-verify / -manage.</summary>
[ApiController]
[Route("api/collections")]
[Authorize(Policy = "collections-any")]
public class CollectionsController(CollectionService collections) : ControllerBase
{
    private CollectionUser Me => From(User);

    internal static CollectionUser From(ClaimsPrincipal user) => new(
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0,
        user.FindFirstValue(ClaimTypes.Name) ?? "",
        user.HasClaim("permission", "collections-encode"),
        user.HasClaim("permission", "collections-verify"),
        user.HasClaim("permission", "collections-manage"),
        user.HasClaim("permission", "pdc-tag"),
        user.HasClaim("permission", "pdc-confirm"),
        user.HasClaim("permission", "collections-ra-encode"));

    private IActionResult Result<T>((T? data, string? error) r) =>
        r.error is null ? Ok(new { success = true, data = r.data }) : BadRequest(new { success = false, message = r.error });

    [HttpGet("my-sites")]
    public async Task<IActionResult> MySites() => Ok(new { success = true, data = await collections.GetMySitesAsync(Me) });

    /// <summary>One posted invoice's payment history (RFCs + every collection applied to it).</summary>
    [HttpGet("invoices/{postedInvoiceId:int}/history")]
    public async Task<IActionResult> InvoiceHistory(int postedInvoiceId)
    {
        var (data, error) = await collections.InvoiceHistoryAsync(Me, postedInvoiceId);
        return error is null ? Ok(new { success = true, data }) : NotFound(new { success = false, message = error });
    }

    /// <summary>Posted invoices with open balances, by customer key and/or invoice numbers (comma-separated).</summary>
    [HttpGet("invoices")]
    public async Task<IActionResult> Invoices([FromQuery] int siteId, [FromQuery] string? custKey, [FromQuery] string? docNos, [FromQuery] bool openOnly = false)
    {
        var nos = (docNos ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n > 0).Distinct().ToList();
        return Result(await collections.GetInvoicesAsync(Me, siteId, custKey, nos, openOnly));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? siteId, [FromQuery] string? status) =>
        Ok(new { success = true, data = await collections.ListAsync(Me, siteId, status) });

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var dto = await collections.GetAsync(Me, id);
        return dto is null ? NotFound(new { success = false, message = "Collection not found." }) : Ok(new { success = true, data = dto });
    }

    [HttpPost, Authorize(Policy = "collections-encode")]
    public async Task<IActionResult> Create([FromBody] SaveCollectionDto dto) => Result(await collections.CreateAsync(Me, dto));

    [HttpPut("{id:int}"), Authorize(Policy = "collections-encode")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveCollectionDto dto) => Result(await collections.UpdateAsync(Me, id, dto));

    [HttpPost("{id:int}/submit"), Authorize(Policy = "collections-encode")]
    public async Task<IActionResult> Submit(int id) => Result(await collections.SubmitAsync(Me, id));

    /// <summary>Custodian: own Draft/Returned; Finance Manager: any (service enforces).</summary>
    [HttpPost("{id:int}/void")]
    public async Task<IActionResult> Void(int id, [FromBody] ReasonDto dto) => Result(await collections.VoidAsync(Me, id, dto?.Reason));

    [HttpPost("{id:int}/verify"), Authorize(Policy = "collections-verify")]
    public async Task<IActionResult> Verify(int id) => Result(await collections.VerifyAsync(Me, id));

    [HttpPost("{id:int}/return"), Authorize(Policy = "collections-verify")]
    public async Task<IActionResult> Return(int id, [FromBody] ReasonDto dto) => Result(await collections.ReturnAsync(Me, id, dto?.Reason));

    [HttpPost("{id:int}/homs-posting/retry"), Authorize(Policy = "collections-manage")]
    public async Task<IActionResult> RetryHoms(int id) => Result(await collections.RetryHomsAsync(Me, id));

    [HttpPost("{id:int}/bank-match/confirm"), Authorize(Policy = "collections-verify")]
    public async Task<IActionResult> ConfirmMatch(int id) => Result(await collections.ConfirmMatchAsync(Me, id));

    [HttpPost("{id:int}/bank-match/reject"), Authorize(Policy = "collections-verify")]
    public async Task<IActionResult> RejectMatch(int id, [FromBody] ReasonDto dto) => Result(await collections.RejectMatchAsync(Me, id, dto?.Reason));

    [HttpPost("{id:int}/deposit"), Authorize(Policy = "collections-any")]
    public async Task<IActionResult> RecordDeposit(int id, [FromBody] DepositInfoDto dto) => Result(await collections.RecordDepositAsync(Me, id, dto));

    [HttpPost("{id:int}/attachments"), Authorize(Policy = "collections-encode")]
    [RequestSizeLimit(CollectionService.MaxAttachmentBytes + 1024 * 1024)]
    public async Task<IActionResult> AddAttachment(int id, IFormFile? file)
    {
        if (file is null) return BadRequest(new { success = false, message = "Choose a file." });
        await using var stream = file.OpenReadStream();
        return Result(await collections.AddAttachmentAsync(Me, id, file.FileName, stream, file.Length));
    }

    [HttpDelete("{id:int}/attachments/{attachmentId:int}"), Authorize(Policy = "collections-encode")]
    public async Task<IActionResult> DeleteAttachment(int id, int attachmentId) => Result(await collections.DeleteAttachmentAsync(Me, id, attachmentId));

    [HttpGet("{id:int}/attachments/{attachmentId:int}")]
    public async Task<IActionResult> Download(int id, int attachmentId)
    {
        var file = await collections.OpenAttachmentAsync(Me, id, attachmentId);
        if (file is null) return NotFound(new { success = false, message = "Attachment not found." });
        // Inline so the verifier can view it in the browser; still only via this authorized endpoint.
        Response.Headers.ContentDisposition = $"inline; filename=\"{file.Value.fileName.Replace("\"", "")}\"";
        return File(file.Value.stream, file.Value.contentType);
    }
}
