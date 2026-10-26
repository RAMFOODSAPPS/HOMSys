using HOMSys.Infrastructure.Collections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>Which branches (Site ids) a user may encode collections for — set by an admin on the user page.</summary>
[ApiController]
[Route("api/users/{userId:int}/branches")]
[Authorize(Policy = "users")]
public class UserBranchesController(UserBranchService branches) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(int userId) => Ok(new { success = true, data = await branches.GetAsync(userId) });

    [HttpPut]
    public async Task<IActionResult> Set(int userId, [FromBody] List<int> siteIds)
    {
        var error = await branches.SetAsync(userId, siteIds ?? []);
        return error is null ? Ok(new { success = true }) : BadRequest(new { success = false, message = error });
    }
}
