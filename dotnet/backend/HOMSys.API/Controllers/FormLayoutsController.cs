using HOMSys.Application.DTOs.FormLayouts;
using HOMSys.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>Form Designer layouts. Any signed-in user reads (forms render with it); only Admins change them.</summary>
[ApiController]
[Route("api/form-layouts")]
[Authorize]
public class FormLayoutsController(FormLayoutService service) : ControllerBase
{
    /// <summary>data = null when the form still uses its built-in default layout.</summary>
    [HttpGet("{formKey}")]
    public async Task<IActionResult> Get(string formKey)
    {
        var (dto, error) = await service.GetAsync(formKey);
        return error is null ? Ok(new { success = true, data = dto }) : BadRequest(new { success = false, message = error });
    }

    [HttpPut("{formKey}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Save(string formKey, [FromBody] SaveFormLayoutRequest req)
    {
        var (dto, error) = await service.SaveAsync(formKey, req);
        return error is null ? Ok(new { success = true, data = dto }) : BadRequest(new { success = false, message = error });
    }

    /// <summary>Back to the built-in default layout.</summary>
    [HttpDelete("{formKey}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reset(string formKey)
    {
        var error = await service.ResetAsync(formKey);
        return error is null ? Ok(new { success = true }) : BadRequest(new { success = false, message = error });
    }
}
