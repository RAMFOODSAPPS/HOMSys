using HOMSys.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "authorization")]
public class PermissionsController(IPermissionRepository permRepo, IRoleRepository roleRepo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var permissions = await permRepo.GetAllAsync();
        return Ok(new { success = true, data = permissions });
    }

    [HttpGet("role/{roleId:int}")]
    public async Task<IActionResult> GetByRole(int roleId)
    {
        var role = await roleRepo.GetByIdAsync(roleId);
        if (role is null)
            return NotFound(new { success = false, message = "Role not found." });

        var permissions = await permRepo.GetByRoleIdAsync(roleId);
        return Ok(new { success = true, data = permissions.Select(p => p.Id) });
    }

    [HttpPut("role/{roleId:int}")]
    public async Task<IActionResult> SetRolePermissions(int roleId, [FromBody] SetRolePermissionsDto dto)
    {
        var role = await roleRepo.GetByIdAsync(roleId);
        if (role is null)
            return NotFound(new { success = false, message = "Role not found." });

        await permRepo.SetRolePermissionsAsync(roleId, dto.PermissionIds);
        return Ok(new { success = true, message = "Permissions updated." });
    }
}

public record SetRolePermissionsDto(IEnumerable<int> PermissionIds);
