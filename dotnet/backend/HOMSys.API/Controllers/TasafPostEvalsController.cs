using HOMSys.Application.DTOs.Tasaf;
using HOMSys.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>Post Evaluation of Completed TASAFs (Promo Post Evaluation / Post Activity form); its approval closes the TASAF.</summary>
[ApiController]
[Route("api/tasaf-post-evals")]
[Authorize(Policy = "tasaf")]
public class TasafPostEvalsController(TasafPostEvalService service) : ControllerBase
{
    /// <summary>Completed / Closed TASAFs the caller may see, with their post evaluation.</summary>
    [HttpGet]
    public async Task<IActionResult> List() => Ok(new { success = true, data = await service.GetListAsync() });

    /// <summary>The TASAF's post evaluation, or a new one (id 0) pre-filled from it.</summary>
    [HttpGet("{tasafId:int}")]
    public async Task<IActionResult> Get(int tasafId) => Envelope(await service.GetAsync(tasafId));

    /// <summary>Create / change (Draft or Returned).</summary>
    [HttpPut("{tasafId:int}")]
    public async Task<IActionResult> Save(int tasafId, [FromBody] SaveTasafPostEvalDto dto) => Envelope(await service.SaveAsync(tasafId, dto));

    /// <summary>Save and submit to the TASAF's approval chain.</summary>
    [HttpPost("{tasafId:int}/submit")]
    public async Task<IActionResult> Submit(int tasafId, [FromBody] SaveTasafPostEvalDto dto) => Envelope(await service.SubmitAsync(tasafId, dto));

    /// <summary>Approve / return the current step (comment required).</summary>
    [HttpPost("{tasafId:int}/decide")]
    public async Task<IActionResult> Decide(int tasafId, [FromBody] TasafDecisionDto dto) => Envelope(await service.DecideAsync(tasafId, dto));

    [HttpPost("{tasafId:int}/revert")]
    public async Task<IActionResult> Revert(int tasafId, [FromBody] TasafCommentDto dto) => Envelope(await service.RevertAsync(tasafId, dto));

    /// <summary>Add a supporting file (PDF / image / Office, max 10 MB) while Draft / Returned.</summary>
    [HttpPost("{tasafId:int}/attachments")]
    [RequestSizeLimit(TasafPostEvalService.MaxAttachmentBytes + 1024 * 1024)]
    public async Task<IActionResult> AddAttachment(int tasafId, IFormFile? file)
    {
        if (file is null) return BadRequest(new { success = false, message = "Choose a file." });
        await using var stream = file.OpenReadStream();
        return Envelope(await service.AddAttachmentAsync(tasafId, file.FileName, stream, file.Length));
    }

    [HttpDelete("{tasafId:int}/attachments/{attachmentId:int}")]
    public async Task<IActionResult> DeleteAttachment(int tasafId, int attachmentId) =>
        Envelope(await service.DeleteAttachmentAsync(tasafId, attachmentId));

    [HttpGet("{tasafId:int}/attachments/{attachmentId:int}")]
    public async Task<IActionResult> Download(int tasafId, int attachmentId)
    {
        var file = await service.OpenAttachmentAsync(tasafId, attachmentId);
        if (file is null) return NotFound(new { success = false, message = "File not found." });
        Response.Headers.ContentDisposition = $"inline; filename=\"{file.Value.fileName.Replace("\"", "")}\"";
        return File(file.Value.stream, file.Value.contentType);
    }

    private IActionResult Envelope((TasafPostEvalDto? dto, string? error) r) =>
        r.error == TasafPostEvalService.NotFound ? NotFound(new { success = false, message = r.error })
        : r.error is not null ? BadRequest(new { success = false, message = r.error })
        : Ok(new { success = true, data = r.dto });
}
