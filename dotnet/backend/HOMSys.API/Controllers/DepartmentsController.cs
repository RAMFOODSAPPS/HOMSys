using HOMSys.Application.DTOs.Departments;
using HOMSys.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "departments")]
public class DepartmentsController(DepartmentService departmentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var departments = await departmentService.GetAllAsync();
        return Ok(new { success = true, data = departments });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var department = await departmentService.GetByIdAsync(id);
        if (department is null)
            return NotFound(new { success = false, message = "Department not found." });

        return Ok(new { success = true, data = department });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDepartmentDto dto)
    {
        var (department, error) = await departmentService.CreateAsync(dto);
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return CreatedAtAction(nameof(GetById), new { id = department!.Id }, new { success = true, data = department });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDepartmentDto dto)
    {
        var (department, error) = await departmentService.UpdateAsync(id, dto);
        if (error == "Department not found.")
            return NotFound(new { success = false, message = error });
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true, data = department });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await departmentService.DeleteAsync(id);
        if (!deleted)
            return NotFound(new { success = false, message = "Department not found." });

        return Ok(new { success = true, message = "Department deleted." });
    }
}
