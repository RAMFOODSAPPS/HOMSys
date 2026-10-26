using HOMSys.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>The signed-in user's own notifications — any logged-in user.</summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController(NotificationService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetFeed([FromQuery] int? take) =>
        Ok(new { success = true, data = await service.GetFeedAsync(take ?? 30) });

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        await service.MarkReadAsync(id);
        return Ok(new { success = true });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await service.MarkAllReadAsync();
        return Ok(new { success = true });
    }
}
