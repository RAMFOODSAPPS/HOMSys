using HOMSys.Application.DTOs.Collections;
using HOMSys.Infrastructure.Collections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>Supporting-document types for collections (user 2026-11-23): read by every collections user, maintained by
/// the Finance Manager (collections-manage).</summary>
[ApiController]
[Route("api/collection-document-kinds")]
[Authorize(Policy = "collections-any")]
public class CollectionDocumentKindsController(CollectionDocumentKindService kinds) : ControllerBase
{
    private string Me => CollectionsController.From(User).Name;

    private IActionResult Result((CollectionDocumentKindDto? data, string? error) r) =>
        r.error is null ? Ok(new { success = true, data = r.data }) : BadRequest(new { success = false, message = r.error });

    /// <summary>all = also the inactive ones (labels of older files, and the maintenance list).</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool all = false) => Ok(new { success = true, data = await kinds.ListAsync(all) });

    [HttpPost, Authorize(Policy = "collections-manage")]
    public async Task<IActionResult> Create([FromBody] SaveDocumentKindDto dto) => Result(await kinds.CreateAsync(dto, Me));

    [HttpPut("{id:int}"), Authorize(Policy = "collections-manage")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveDocumentKindDto dto) => Result(await kinds.UpdateAsync(id, dto, Me));

    [HttpDelete("{id:int}"), Authorize(Policy = "collections-manage")]
    public async Task<IActionResult> Delete(int id) =>
        await kinds.DeleteAsync(id) is { } error ? BadRequest(new { success = false, message = error }) : Ok(new { success = true });
}
