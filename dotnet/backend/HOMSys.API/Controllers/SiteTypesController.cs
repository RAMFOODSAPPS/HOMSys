using HOMSys.Application.DTOs.SiteTypes;
using HOMSys.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

[ApiController]
[Route("api/site-types")]
[Authorize(Policy = "site-types")]
public class SiteTypesController(SiteTypeService siteTypeService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var siteTypes = await siteTypeService.GetAllAsync();
        return Ok(new { success = true, data = siteTypes });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var siteType = await siteTypeService.GetByIdAsync(id);
        if (siteType is null)
            return NotFound(new { success = false, message = "Site type not found." });

        return Ok(new { success = true, data = siteType });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSiteTypeDto dto)
    {
        var (siteType, error) = await siteTypeService.CreateAsync(dto);
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return CreatedAtAction(nameof(GetById), new { id = siteType!.Id }, new { success = true, data = siteType });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSiteTypeDto dto)
    {
        var (siteType, error) = await siteTypeService.UpdateAsync(id, dto);
        if (error == "Site type not found.")
            return NotFound(new { success = false, message = error });
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true, data = siteType });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await siteTypeService.DeleteAsync(id);
        if (!deleted)
            return NotFound(new { success = false, message = "Site type not found." });

        return Ok(new { success = true, message = "Site type deleted." });
    }
}
