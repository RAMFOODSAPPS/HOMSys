using HOMSys.Application.DTOs.Sites;
using HOMSys.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "sites")]
public class SitesController(SiteService siteService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var sites = await siteService.GetAllAsync();
        return Ok(new { success = true, data = sites });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var site = await siteService.GetByIdAsync(id);
        if (site is null)
            return NotFound(new { success = false, message = "Site not found." });

        return Ok(new { success = true, data = site });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSiteDto dto)
    {
        var (site, error) = await siteService.CreateAsync(dto);
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return CreatedAtAction(nameof(GetById), new { id = site!.Id }, new { success = true, data = site });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSiteDto dto)
    {
        var (site, error) = await siteService.UpdateAsync(id, dto);
        if (error == "Site not found.")
            return NotFound(new { success = false, message = error });
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true, data = site });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await siteService.DeleteAsync(id);
        if (!deleted)
            return NotFound(new { success = false, message = "Site not found." });

        return Ok(new { success = true, message = "Site deleted." });
    }
}
