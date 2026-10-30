using HOMSys.Application.DTOs.Collections;
using HOMSys.Infrastructure.Collections;
using HOMSys.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>Headless inbound sync from LegacyMasterWatcher.exe (X-Api-Key, same key as /api/masters/sync).</summary>
[ApiController]
[Route("api/collections/sync")]
[AllowAnonymous]
public class CollectionSyncController(
    PostedInvoiceSyncImporter importer,
    InvoiceSyncRequestService requests,
    BmsCollectionImporter bms,
    HomsPostingService postings,
    IConfiguration config,
    ILogger<CollectionSyncController> logger) : ControllerBase
{
    private bool KeyOk(string? apiKey)
    {
        var expected = config["HeadlessApiKey"];
        return !string.IsNullOrEmpty(expected) && apiKey == expected;
    }

    /// <summary>Watcher poll (every minute): branch fetch requests waiting for the SL files.</summary>
    [HttpGet("pending")]
    public async Task<IActionResult> Pending([FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!KeyOk(apiKey)) return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });
        return Ok(new { success = true, data = await requests.ClaimPendingAsync() });
    }

    /// <summary>Watcher report once a request has been read from the SL files (or failed).</summary>
    [HttpPost("requests/{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, [FromHeader(Name = "X-Api-Key")] string? apiKey, [FromBody] CompleteInvoiceSyncDto? dto)
    {
        if (!KeyOk(apiKey)) return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });
        var error = await requests.CompleteAsync(id, dto ?? new CompleteInvoiceSyncDto(null, "Empty report."));
        return error is null ? Ok(new { success = true }) : NotFound(new { success = false, message = error });
    }

    /// <summary>One SL file's invoices + RFC sums; safe to re-send (idempotent).</summary>
    [HttpPost("posted-invoices")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> PostedInvoices([FromHeader(Name = "X-Api-Key")] string? apiKey, [FromBody] PostedInvoiceSyncRequest? request)
    {
        var expected = config["HeadlessApiKey"];
        if (string.IsNullOrEmpty(expected) || apiKey != expected)
            return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });
        if (request is null || request.Wh <= 0 || string.IsNullOrWhiteSpace(request.SourceFile))
            return BadRequest(new { success = false, message = "wh and sourceFile are required." });

        var result = await importer.ApplyAsync(request);
        logger.LogInformation("Posted-invoice sync {File} (WH {Wh}): {Result}", request.SourceFile, request.Wh, result);
        return Ok(new { success = true, data = result.ToString() });
    }

    /// <summary>Watcher poll (every minute): verified collections to write into the HOMS ARCHIVES files.</summary>
    [HttpGet("homs-postings/pending")]
    public async Task<IActionResult> HomsPostingsPending([FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!KeyOk(apiKey)) return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });
        return Ok(new { success = true, data = await postings.ClaimPendingAsync() });
    }

    /// <summary>A JV's number from HOMS DOCNUM, recorded before the JV is written so a retry reuses it.</summary>
    [HttpPost("homs-postings/{id:int}/docno")]
    public async Task<IActionResult> HomsPostingDocNo(int id, [FromHeader(Name = "X-Api-Key")] string? apiKey, [FromBody] HomsPostingDocNoDto dto)
    {
        if (!KeyOk(apiKey)) return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });
        var error = await postings.SetDocNoAsync(id, dto.DocNo);
        return error is null ? Ok(new { success = true }) : Conflict(new { success = false, message = error });
    }

    [HttpPost("homs-postings/{id:int}/complete")]
    public async Task<IActionResult> HomsPostingComplete(int id, [FromHeader(Name = "X-Api-Key")] string? apiKey, [FromBody] CompleteHomsPostingDto dto)
    {
        if (!KeyOk(apiKey)) return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });
        var error = await postings.CompleteAsync(id, dto);
        if (error is null) logger.LogInformation("HOMS posting {Id}: {Result}", id, dto.Error ?? (dto.AlreadyPresent ? "already present" : dto.File));
        return error is null ? Ok(new { success = true }) : NotFound(new { success = false, message = error });
    }

    /// <summary>The BANKS4W sync was removed (2026-10-16): bank accounts are kept in HOMSys. An older watcher that still
    /// sends BANKS4W gets a success, so it stops resending, and nothing is changed.</summary>
    [HttpPost("homs-banks")]
    public IActionResult HomsBanks([FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!KeyOk(apiKey)) return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });
        return Ok(new { success = true, data = new { ignored = true, message = "The BANKS4W sync was removed; bank accounts are kept in HOMSys." } });
    }

    /// <summary>BMS posted collections (OH03/OD03{MMYY}), changed receipts only; idempotent.</summary>
    [HttpPost("bms-collections")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> BmsCollections([FromHeader(Name = "X-Api-Key")] string? apiKey, [FromBody] BmsCollectionSyncRequest? request)
    {
        if (!KeyOk(apiKey)) return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });
        if (request is null || string.IsNullOrWhiteSpace(request.SourceFile))
            return BadRequest(new { success = false, message = "sourceFile is required." });
        var result = await bms.ImportAsync(request);
        logger.LogInformation("BMS collections sync: {Result}", result);
        return Ok(new { success = true, data = result });
    }
}
