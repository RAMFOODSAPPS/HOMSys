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
    ArofInvoiceSyncImporter arof,
    ArofMirrorImporter arofMirror,
    InvoiceSyncRequestService requests,
    BmsCollectionImporter bms,
    BirSeriesImporter birSeries,
    HomsPostingService postings,
    RfcFollowUpService rfcLinks,
    IConfiguration config,
    ILogger<CollectionSyncController> logger) : ControllerBase
{
    /// <summary>After RFCs came in from AROF4HO: links returns now matched by exactly one RFC of the same amount and
    /// releases their held OR / RPDC (user 2026-11-24). Never fails the sync itself.</summary>
    private async Task LinkReturnsAsync()
    {
        try
        {
            var n = await rfcLinks.AutoLinkAsync();
            if (n > 0) logger.LogInformation("RFC follow-up: {Count} return(s) linked to their RFC after the AROF4HO sync", n);
        }
        catch (Exception ex) { logger.LogWarning(ex, "RFC follow-up auto-link after the AROF4HO sync failed"); }
    }

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

    /// <summary>New / changed invoices + RFCs from HOMS's A/R open file (AROF4HO.DBF); safe to re-send (idempotent).</summary>
    [HttpPost("arof-invoices")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> ArofInvoices([FromHeader(Name = "X-Api-Key")] string? apiKey, [FromBody] ArofSyncRequest? request)
    {
        if (!KeyOk(apiKey)) return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });
        if (request is null) return BadRequest(new { success = false, message = "groups / removed are required." });
        var result = await arof.ApplyAsync(request);
        logger.LogInformation("AROF4HO invoice sync: {Result}", result);
        await LinkReturnsAsync();
        return Ok(new { success = true, data = result.ToString() });
    }

    /// <summary>The AROF4HO mirror as HOMSys holds it (file header + block fingerprints), for the watcher's compare.</summary>
    [HttpGet("arof-mirror")]
    public async Task<IActionResult> ArofMirrorState([FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!KeyOk(apiKey)) return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });
        return Ok(new { success = true, data = await arofMirror.GetStateAsync() });
    }

    /// <summary>Changed / new AROF4HO blocks (whole, by RecNo span) + the file header; rebuilds the invoices they name.</summary>
    [HttpPost("arof-mirror")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> ArofMirrorPush([FromHeader(Name = "X-Api-Key")] string? apiKey, [FromBody] ArofMirrorPush? request)
    {
        if (!KeyOk(apiKey)) return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });
        if (request?.File is null) return BadRequest(new { success = false, message = "file is required." });
        var (result, error) = await arofMirror.ApplyAsync(request);
        if (error is not null) return BadRequest(new { success = false, message = error });
        logger.LogInformation("AROF4HO mirror push: {Result}", result);
        await LinkReturnsAsync();
        return Ok(new { success = true, data = result!.ToString() });
    }

    /// <summary>One page of rebuilding the invoices / RFCs from the AROF4HO mirror (after a big push, or as a repair).</summary>
    [HttpPost("arof-mirror/rebuild")]
    public async Task<IActionResult> ArofMirrorRebuild([FromHeader(Name = "X-Api-Key")] string? apiKey, [FromBody] ArofRebuildRequest? request)
    {
        if (!KeyOk(apiKey)) return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });
        if (request is null) return BadRequest(new { success = false, message = "afterNo / take are required." });
        var result = await arofMirror.RebuildAsync(request.AfterNo, request.Take);
        logger.LogInformation("AROF4HO invoice rebuild after {After}: {Result}", request.AfterNo, result);
        await LinkReturnsAsync();
        return Ok(new { success = true, data = result });
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

    /// <summary>HOMS's B1 files (invoice no. → BIR series), changed invoices only; idempotent.</summary>
    [HttpPost("bir-series")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> BirSeries([FromHeader(Name = "X-Api-Key")] string? apiKey, [FromBody] BirSeriesSyncRequest? request)
    {
        if (!KeyOk(apiKey)) return Unauthorized(new { success = false, message = "Invalid or missing X-Api-Key." });
        if (request is null || string.IsNullOrWhiteSpace(request.SourceFile))
            return BadRequest(new { success = false, message = "sourceFile is required." });
        try
        {
            var result = await birSeries.ImportAsync(request);
            logger.LogInformation("BIR series sync: {Result}", result);
            return Ok(new { success = true, data = result });
        }
        catch (ArgumentException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }
}
