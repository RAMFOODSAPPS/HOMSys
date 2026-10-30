using HOMSys.Application.DTOs.Tasaf;
using HOMSys.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>Trade Activity Spend Approval Forms.</summary>
[ApiController]
[Route("api/tasafs")]
[Authorize(Policy = "tasaf")]
public class TasafsController(TasafService service, SalesOrderService salesOrderService, CompanyService companies) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(new { success = true, data = await service.GetListAsync() });

    /// <summary>All dropdown lists + branch choices for the form.</summary>
    [HttpGet("options")]
    public async Task<IActionResult> Options() =>
        Ok(new { success = true, data = await service.GetOptionsAsync() });

    [HttpGet("search/customer")]
    public async Task<IActionResult> SearchCustomers([FromQuery] string? term) =>
        Ok(new { success = true, data = await salesOrderService.SearchCustomersAsync(term ?? "", take: 50) });

    /// <summary>Cust Key dropdown: stores of the picked chains (listed, else name keywords) and branches (else areas).</summary>
    [HttpGet("customer-options")]
    public async Task<IActionResult> CustomerOptions([FromQuery] int[]? chainIds, [FromQuery] int[]? areaIds,
        [FromQuery] int[]? siteIds, [FromQuery] int? salesGroupId)
    {
        var (customers, filtered, siteCodes) = await service.GetCustomerOptionsAsync(chainIds ?? [], areaIds ?? [], siteIds ?? [], salesGroupId);
        return Ok(new { success = true, data = new { filtered, customers, siteCodes } });
    }

    /// <summary>LSP / case: each SKU's price for the picked stores, grouped by price.</summary>
    [HttpPost("product-prices")]
    public async Task<IActionResult> ProductPrices([FromBody] TasafPriceRequest req) =>
        Ok(new { success = true, data = await service.GetProductPricesAsync(req) });

    /// <summary>Customer / Chain dropdown: stores per chain in the picked branches (else areas); null = no filter.</summary>
    [HttpGet("chain-store-counts")]
    public async Task<IActionResult> ChainStoreCounts([FromQuery] int[]? areaIds, [FromQuery] int[]? siteIds,
        [FromQuery] int? salesGroupId) =>
        Ok(new { success = true, data = await service.GetChainStoreCountsAsync(areaIds ?? [], siteIds ?? [], salesGroupId) });

    /// <summary>Participating SKU dropdown for the TASAF's Product Categories / Sub-Categories / Segment.</summary>
    [HttpGet("product-options")]
    public async Task<IActionResult> ProductOptions([FromQuery] int[]? productCategoryIds, [FromQuery] string[]? subCategoryCodes) =>
        Ok(new { success = true, data = await service.GetProductOptionsAsync(productCategoryIds ?? [], subCategoryCodes ?? []) });

    [HttpGet("search/product")]
    public async Task<IActionResult> SearchProducts([FromQuery] string? term) =>
        Ok(new { success = true, data = await salesOrderService.SearchProductsAsync(term ?? "") });

    [HttpGet("lookup/product/{cProdNo}")]
    public async Task<IActionResult> LookupProduct(string cProdNo)
    {
        var product = await service.LookupProductAsync(cProdNo);
        if (product is null)
            return NotFound(new { success = false, message = $"Product {cProdNo} not found." });

        return Ok(new { success = true, data = product });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var (tasaf, error) = await service.GetByIdAsync(id);
        if (error == TasafService.NotFound)
            return NotFound(new { success = false, message = error });
        if (error is not null)
            return StatusCode(403, new { success = false, message = error });

        return Ok(new { success = true, data = tasaf });
    }

    /// <summary>Print / PDF: the TASAF as an HTML document (approval-email layout) + the company logo (data: URI or null).</summary>
    [HttpGet("{id:int}/document")]
    public async Task<IActionResult> Document(int id)
    {
        var (tasaf, error) = await service.GetByIdAsync(id);
        if (error == TasafService.NotFound)
            return NotFound(new { success = false, message = error });
        if (error is not null)
            return StatusCode(403, new { success = false, message = error });

        // Logo of the proponent's company (TASAF print / PDF header); the PDF builder reuses it.
        var logo = await companies.LogoDataUriForUserAsync(tasaf!.ProponentUserId);
        return Ok(new { success = true, data = new { html = TasafEmailRenderer.Render(tasaf, "", null, logo, email: false), logo } });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveTasafDto dto)
    {
        var (tasaf, error) = await service.CreateAsync(dto);
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return CreatedAtAction(nameof(GetById), new { id = tasaf!.Id }, new { success = true, data = tasaf });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveTasafDto dto)
    {
        var (tasaf, error) = await service.UpdateAsync(id, dto);
        if (error == TasafService.NotFound)
            return NotFound(new { success = false, message = error });
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true, data = tasaf });
    }

    /// <summary>KAM submits (or resubmits a Returned) TASAF → GKAM review.</summary>
    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> Submit(int id)
    {
        var (tasaf, error) = await service.SubmitAsync(id);
        if (error == TasafService.NotFound)
            return NotFound(new { success = false, message = error });
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true, data = tasaf });
    }

    /// <summary>GKAM review or final approval: approve / return / reject.</summary>
    [HttpPost("{id:int}/decision")]
    public async Task<IActionResult> Decide(int id, [FromBody] TasafDecisionDto dto)
    {
        var (tasaf, error) = await service.DecideAsync(id, dto);
        if (error == TasafService.NotFound)
            return NotFound(new { success = false, message = error });
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true, data = tasaf });
    }

    /// <summary>Admin: undo an approval mistake — back to the proponent as Returned.</summary>
    [HttpPost("{id:int}/revert")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Revert(int id, [FromBody] TasafCommentDto dto) =>
        Envelope(await service.RevertAsync(id, dto));

    /// <summary>Proponent or Admin: withdraw the TASAF (kept as Cancelled, not deleted).</summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] TasafCommentDto dto) =>
        Envelope(await service.CancelAsync(id, dto));

    private IActionResult Envelope((TasafDto? dto, string? error) r) =>
        r.error == TasafService.NotFound ? NotFound(new { success = false, message = r.error })
        : r.error is not null ? BadRequest(new { success = false, message = r.error })
        : Ok(new { success = true, data = r.dto });

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var error = await service.DeleteAsync(id);
        if (error == TasafService.NotFound)
            return NotFound(new { success = false, message = error });
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true, message = "TASAF deleted." });
    }
}
