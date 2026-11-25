using HOMSys.Application.DTOs.Tasaf;
using HOMSys.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>TASAF Dashboard: budget vs TASAFs per sales group and account chain (permission tasaf-dashboard).</summary>
[ApiController]
[Route("api/tasaf-dashboard")]
[Authorize(Policy = "tasaf-dashboard")]
public class TasafDashboardController(TasafDashboardService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? year) =>
        Ok(new { success = true, data = await service.GetAsync(year) });

    /// <summary>Enter / change budgets for a year (TMM / Admin — tasaf-masters).</summary>
    [HttpPut("budgets")]
    public async Task<IActionResult> SaveBudgets([FromBody] SaveTasafBudgetsDto dto) =>
        await service.SaveBudgetsAsync(dto) is { } error
            ? BadRequest(new { success = false, message = error })
            : Ok(new { success = true });
}
