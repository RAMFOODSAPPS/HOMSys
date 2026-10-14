using HOMSys.Application.DTOs.Companies;
using HOMSys.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "companies")]
public class CompaniesController(CompanyService companyService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var companies = await companyService.GetAllAsync();
        return Ok(new { success = true, data = companies });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var company = await companyService.GetByIdAsync(id);
        if (company is null)
            return NotFound(new { success = false, message = "Company not found." });

        return Ok(new { success = true, data = company });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCompanyDto dto)
    {
        var (company, error) = await companyService.CreateAsync(dto);
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return CreatedAtAction(nameof(GetById), new { id = company!.Id }, new { success = true, data = company });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCompanyDto dto)
    {
        var (company, error) = await companyService.UpdateAsync(id, dto);
        if (error == "Company not found.")
            return NotFound(new { success = false, message = error });
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true, data = company });
    }

    /// <summary>
    /// The company logo image. Anonymous so an &lt;img&gt; can load it (a logo isn't secret); cached a
    /// day — the page adds ?v=LogoVersion so a new upload shows at once.
    /// </summary>
    [HttpGet("{id:int}/logo")]
    [AllowAnonymous]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetLogo(int id) =>
        await companyService.GetLogoAsync(id) is var (data, type) ? File(data, type) : NotFound();

    /// <summary>Upload / replace the logo (multipart "file"; PNG or JPG, resized in the browser, ≤ 300 KB).</summary>
    [HttpPost("{id:int}/logo")]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> UploadLogo(int id, IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { success = false, message = "Choose an image file." });
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var (company, error) = await companyService.SaveLogoAsync(id, ms.ToArray());
        if (error == "Company not found.") return NotFound(new { success = false, message = error });
        return error is not null ? BadRequest(new { success = false, message = error }) : Ok(new { success = true, data = company });
    }

    [HttpDelete("{id:int}/logo")]
    public async Task<IActionResult> RemoveLogo(int id)
    {
        var (company, error) = await companyService.RemoveLogoAsync(id);
        return error is not null ? NotFound(new { success = false, message = error }) : Ok(new { success = true, data = company });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await companyService.DeleteAsync(id);
        if (!deleted)
            return NotFound(new { success = false, message = "Company not found." });

        return Ok(new { success = true, message = "Company deleted." });
    }
}
