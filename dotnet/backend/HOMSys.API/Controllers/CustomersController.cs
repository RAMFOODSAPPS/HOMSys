using HOMSys.Application.DTOs.Customers;
using HOMSys.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>Read-only customer master (synced from each branch's BMS CUST4WIN).</summary>
[ApiController]
[Route("api/customers")]
[Authorize(Policy = "customers")]
public class CustomersController(CustomerMasterService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] CustomerQuery query) =>
        Ok(new { success = true, data = await service.GetPageAsync(query) });

    [HttpGet("branches")]
    public async Task<IActionResult> Branches() =>
        Ok(new { success = true, data = await service.GetBranchesAsync() });
}
