using HOMSys.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

public class ExportPricelistRequest
{
    public List<string> CustKeys { get; set; } = [];
    public DateOnly EffectivityDate { get; set; }
    public decimal SrpMarkupPercent { get; set; } = 3m;
}

/// <summary>Downloadable branch pricelist export, one sheet comparing selected customers by column.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PricelistController(
    PricelistExportService exportService,
    PricelistExcelBuilder excelBuilder) : ControllerBase
{
    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] ExportPricelistRequest request)
    {
        if (request.CustKeys.Count == 0)
            return BadRequest(new { success = false, message = "At least one customer must be selected." });

        var result = await exportService.BuildAsync(request.CustKeys, request.EffectivityDate, request.SrpMarkupPercent);
        return Ok(new { success = true, data = result });
    }

    [HttpPost("export")]
    public async Task<IActionResult> Export([FromBody] ExportPricelistRequest request)
    {
        if (request.CustKeys.Count == 0)
            return BadRequest(new { success = false, message = "At least one customer must be selected." });

        var result = await exportService.BuildAsync(request.CustKeys, request.EffectivityDate, request.SrpMarkupPercent);
        var bytes = excelBuilder.Build(result);

        var fileName = $"Pricelist_{request.EffectivityDate:yyyyMMdd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
