using System.Security.Claims;
using HOMSys.Application.DTOs.Collections;
using HOMSys.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>
/// Branch-side "Fetch invoices" trigger. The branch only queues a request; LegacyMasterWatcher.exe
/// (HO, can read F:\) picks it up within about a minute and loads those invoices from the SL files.
/// </summary>
[ApiController]
[Route("api/collections/invoice-sync")]
[Authorize(Policy = "collections-encode")]
public class InvoiceSyncRequestsController(InvoiceSyncRequestService requests) : ControllerBase
{
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private string UserName => User.FindFirstValue(ClaimTypes.Name) ?? "";
    private bool CanManageAll => User.HasClaim("permission", "collections-manage");

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceSyncRequestDto dto)
    {
        var (result, error) = await requests.CreateAsync(UserId, UserName, CanManageAll, dto);
        return error is null ? Ok(new { success = true, data = result }) : BadRequest(new { success = false, message = error });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var result = await requests.GetAsync(id, UserId, CanManageAll);
        return result is null ? NotFound(new { success = false, message = "Request not found." }) : Ok(new { success = true, data = result });
    }
}
