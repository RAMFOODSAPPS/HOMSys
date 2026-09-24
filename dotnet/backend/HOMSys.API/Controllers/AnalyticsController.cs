using HOMSys.Application.DTOs.Analytics;
using HOMSys.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>
/// Data Analytics: dataset catalog, query engine, filter values, Excel export
/// and saved reports/dashboards. Branch RLS is always applied server-side from
/// the caller's JWT "branch" claim — it is never part of the request.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "data-analytics")]
public class AnalyticsController(AnalyticsService analytics) : ControllerBase
{
    [HttpGet("meta")]
    public async Task<IActionResult> GetMeta() =>
        Ok(new { success = true, data = await analytics.GetMetaAsync() });

    [HttpGet("values")]
    public async Task<IActionResult> GetValues([FromQuery] string dataset, [FromQuery] string field,
        [FromQuery] string? q = null, [FromQuery] int take = 100)
    {
        var (values, error) = await analytics.ValuesAsync(dataset, field, q, take);
        if (error is not null) return BadRequest(new { success = false, message = error });
        return Ok(new { success = true, data = values });
    }

    [HttpPost("query")]
    public async Task<IActionResult> Query([FromBody] ReportSpecDto spec)
    {
        var (result, error) = await analytics.QueryAsync(spec);
        if (error is not null) return BadRequest(new { success = false, message = error });
        return Ok(new { success = true, data = result });
    }

    [HttpPost("export")]
    public async Task<IActionResult> Export([FromBody] ExportRequest request)
    {
        var (bytes, error) = await analytics.ExportAsync(request);
        if (error is not null) return BadRequest(new { success = false, message = error });

        var safeTitle = string.Concat((request.Title ?? "Analytics").Split(Path.GetInvalidFileNameChars())).Trim();
        var fileName = $"{(safeTitle.Length == 0 ? "Analytics" : safeTitle)}_{AnalyticsService.TodayPh():yyyyMMdd}.xlsx";
        return File(bytes!, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("items")]
    public async Task<IActionResult> GetItems() =>
        Ok(new { success = true, data = await analytics.GetItemsAsync() });

    [HttpGet("items/{id:int}")]
    public async Task<IActionResult> GetItem(int id)
    {
        var item = await analytics.GetItemAsync(id);
        if (item is null) return NotFound(new { success = false, message = AnalyticsService.NotFound });
        return Ok(new { success = true, data = item });
    }

    [HttpPost("items")]
    public async Task<IActionResult> CreateItem([FromBody] SaveItemRequest request)
    {
        var (item, error) = await analytics.CreateAsync(request);
        if (error is not null) return BadRequest(new { success = false, message = error });
        return CreatedAtAction(nameof(GetItem), new { id = item!.Id }, new { success = true, data = item });
    }

    [HttpPut("items/{id:int}")]
    public async Task<IActionResult> UpdateItem(int id, [FromBody] SaveItemRequest request)
    {
        var (item, error) = await analytics.UpdateAsync(id, request);
        if (error == AnalyticsService.NotFound) return NotFound(new { success = false, message = error });
        if (error is not null) return BadRequest(new { success = false, message = error });
        return Ok(new { success = true, data = item });
    }

    [HttpDelete("items/{id:int}")]
    public async Task<IActionResult> DeleteItem(int id)
    {
        var error = await analytics.DeleteAsync(id);
        if (error == AnalyticsService.NotFound) return NotFound(new { success = false, message = error });
        if (error is not null) return BadRequest(new { success = false, message = error });
        return Ok(new { success = true, message = "Deleted." });
    }
}
