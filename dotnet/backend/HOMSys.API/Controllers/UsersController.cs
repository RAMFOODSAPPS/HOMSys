using HOMSys.Application.DTOs.Users;
using HOMSys.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "users")]
public class UsersController(UserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await userService.GetAllAsync();
        return Ok(new { success = true, data = users });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await userService.GetByIdAsync(id);
        if (user is null)
            return NotFound(new { success = false, message = "User not found." });

        return Ok(new { success = true, data = user });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
    {
        var (user, error) = await userService.CreateAsync(dto);
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return CreatedAtAction(nameof(GetById), new { id = user!.Id }, new { success = true, data = user });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto)
    {
        var (user, error) = await userService.UpdateAsync(id, dto);
        if (error == "User not found.")
            return NotFound(new { success = false, message = error });
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true, data = user });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await userService.DeleteAsync(id);
        if (!deleted)
            return NotFound(new { success = false, message = "User not found." });

        return Ok(new { success = true, message = "User deleted." });
    }
}
