using HOMSys.Application.DTOs.Tasaf;
using HOMSys.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>TASAF worksheets (LF Breakdown / Near Ex): filled out by the proponent, attached to a TASAF.</summary>
[ApiController]
[Route("api/tasaf-worksheets")]
[Authorize(Policy = "tasaf")]
public class TasafWorksheetsController(TasafWorksheetService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(new { success = true, data = await service.GetListAsync() });

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id) => Result(await service.GetByIdAsync(id));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveTasafWorksheetDto dto) => Result(await service.CreateAsync(dto));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveTasafWorksheetDto dto) => Result(await service.UpdateAsync(id, dto));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var error = await service.DeleteAsync(id);
        return error switch
        {
            null => Ok(new { success = true, message = "Worksheet deleted." }),
            TasafWorksheetService.NotFound => NotFound(new { success = false, message = error }),
            _ => BadRequest(new { success = false, message = error })
        };
    }

    [HttpPost("{id:int}/attach")]
    public async Task<IActionResult> Attach(int id, [FromBody] AttachTasafWorksheetDto dto) =>
        Result(await service.AttachAsync(id, dto.TasafId));

    [HttpPost("{id:int}/detach")]
    public async Task<IActionResult> Detach(int id) => Result(await service.DetachAsync(id));

    private IActionResult Result((TasafWorksheetDto? dto, string? error) r) => r switch
    {
        ({ } dto, _) => Ok(new { success = true, data = dto }),
        (_, TasafWorksheetService.NotFound) => NotFound(new { success = false, message = r.error }),
        (_, TasafWorksheetService.Forbidden) => StatusCode(403, new { success = false, message = r.error }),
        _ => BadRequest(new { success = false, message = r.error })
    };
}
