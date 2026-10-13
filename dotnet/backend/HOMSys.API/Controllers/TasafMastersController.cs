using HOMSys.Application.DTOs.Tasaf;
using HOMSys.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>CRUD for every TASAF master list; {key} picks the list (see TasafMasterService.Masters).</summary>
[ApiController]
[Route("api/tasaf-masters")]
[Authorize(Policy = "tasaf-masters")]
public class TasafMastersController(TasafMasterService service) : ControllerBase
{
    [HttpGet("meta")]
    public IActionResult GetMeta() => Ok(new { success = true, data = service.GetMeta() });

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers() =>
        Ok(new { success = true, data = await service.GetUsersAsync() });

    [HttpGet("approval-matrix")]
    public async Task<IActionResult> GetMatrix() =>
        Ok(new { success = true, data = await service.GetMatrixAsync() });

    [HttpPut("approval-matrix")]
    public async Task<IActionResult> SaveMatrix([FromBody] List<TasafApproverCellDto> cells)
    {
        var (matrix, error) = await service.SaveMatrixAsync(cells);
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true, data = matrix });
    }

    [HttpGet("approval-rules")]
    public async Task<IActionResult> GetRules() =>
        Ok(new { success = true, data = await service.GetRulesAsync() });

    [HttpPut("approval-rules")]
    public async Task<IActionResult> SaveRules([FromBody] List<TasafApprovalRuleCellDto> cells)
    {
        var (rules, error) = await service.SaveRulesAsync(cells);
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true, data = rules });
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> GetAll(string key)
    {
        var (rows, error) = await service.GetAllAsync(key);
        if (error is not null)
            return NotFound(new { success = false, message = error });

        return Ok(new { success = true, data = rows });
    }

    [HttpGet("{key}/{id:int}")]
    public async Task<IActionResult> GetById(string key, int id)
    {
        var (row, error) = await service.GetByIdAsync(key, id);
        if (error is not null)
            return NotFound(new { success = false, message = error });

        return Ok(new { success = true, data = row });
    }

    [HttpPost("{key}")]
    public async Task<IActionResult> Create(string key, [FromBody] SaveTasafLookupDto dto)
    {
        var (row, error) = await service.CreateAsync(key, dto);
        if (error == TasafMasterService.UnknownMaster)
            return NotFound(new { success = false, message = error });
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return CreatedAtAction(nameof(GetById), new { key, id = row!.Id }, new { success = true, data = row });
    }

    [HttpPut("{key}/{id:int}")]
    public async Task<IActionResult> Update(string key, int id, [FromBody] SaveTasafLookupDto dto)
    {
        var (row, error) = await service.UpdateAsync(key, id, dto);
        if (error is TasafMasterService.UnknownMaster or TasafMasterService.NotFound)
            return NotFound(new { success = false, message = error });
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true, data = row });
    }

    [HttpDelete("{key}/{id:int}")]
    public async Task<IActionResult> Delete(string key, int id)
    {
        var error = await service.DeleteAsync(key, id);
        if (error is TasafMasterService.UnknownMaster or TasafMasterService.NotFound)
            return NotFound(new { success = false, message = error });
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true, message = "Deleted." });
    }
}
