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

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await companyService.DeleteAsync(id);
        if (!deleted)
            return NotFound(new { success = false, message = "Company not found." });

        return Ok(new { success = true, message = "Company deleted." });
    }
}
