using System.Security.Claims;
using HOMSys.Application.DTOs.Roles;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "roles")]
public class RolesController(IRoleRepository roleRepo, IHttpContextAccessor http) : ControllerBase
{
    private string CurrentUser =>
        http.HttpContext?.User?.FindFirstValue(ClaimTypes.Name) ?? "system";

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var roles = await roleRepo.GetAllAsync();
        return Ok(new { success = true, data = roles });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var role = await roleRepo.GetByIdAsync(id);
        if (role is null)
            return NotFound(new { success = false, message = "Role not found." });

        return Ok(new { success = true, data = role });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoleDto dto)
    {
        var existing = await roleRepo.GetByNameAsync(dto.Name);
        if (existing is not null)
            return BadRequest(new { success = false, message = "Role name already exists." });

        var role = new Role
        {
            Name        = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            CreatedBy   = CurrentUser
        };
        var created = await roleRepo.CreateAsync(role);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new { success = true, data = created });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRoleDto dto)
    {
        var role = await roleRepo.GetByIdAsync(id);
        if (role is null)
            return NotFound(new { success = false, message = "Role not found." });

        var duplicate = await roleRepo.GetByNameAsync(dto.Name);
        if (duplicate is not null && duplicate.Id != id)
            return BadRequest(new { success = false, message = "Role name already exists." });

        role.Name        = dto.Name.Trim();
        role.Description = dto.Description?.Trim();
        role.UpdatedAt   = DateTime.UtcNow;
        role.UpdatedBy   = CurrentUser;
        await roleRepo.UpdateAsync(role);
        return Ok(new { success = true, data = role });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await roleRepo.DeleteAsync(id);
        if (!deleted)
            return NotFound(new { success = false, message = "Role not found." });

        return Ok(new { success = true, message = "Role deleted." });
    }
}
