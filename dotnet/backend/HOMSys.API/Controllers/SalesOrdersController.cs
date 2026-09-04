using HOMSys.Application.DTOs.SalesOrders;
using HOMSys.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SalesOrdersController(SalesOrderService salesOrderService) : ControllerBase
{
    [HttpGet, Authorize(Policy = "sales-orders")]
    public async Task<IActionResult> GetAll() =>
        Ok(new { success = true, data = await salesOrderService.GetAllAsync() });

    [HttpGet("{id:int}"), Authorize(Policy = "sales-orders")]
    public async Task<IActionResult> GetById(int id)
    {
        var order = await salesOrderService.GetByIdAsync(id);
        if (order is null)
            return NotFound(new { success = false, message = "Sales order not found." });

        return Ok(new { success = true, data = order });
    }

    [HttpPost, Authorize(Policy = "sales-orders")]
    public async Task<IActionResult> Create([FromBody] CreateSalesOrderDto dto)
    {
        var (order, error) = await salesOrderService.CreateAsync(dto);
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return CreatedAtAction(nameof(GetById), new { id = order!.SoId },
            new { success = true, data = order });
    }

    [HttpPut("{id:int}"), Authorize(Policy = "sales-orders")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateSalesOrderDto dto)
    {
        var (order, error) = await salesOrderService.UpdateAsync(id, dto);
        if (error is not null)
            return BadRequest(new { success = false, message = error });

        return Ok(new { success = true, data = order });
    }

    /// <summary>Customer context for the encode-customer step.</summary>
    [HttpGet("lookup/customer/{custKey}"), Authorize(Policy = "sales-orders")]
    public async Task<IActionResult> LookupCustomer(string custKey)
    {
        var customer = await salesOrderService.LookupCustomerAsync(custKey);
        if (customer is null)
            return NotFound(new { success = false, message = $"Customer {custKey} not found." });

        return Ok(new { success = true, data = customer });
    }

    /// <summary>Product details for the encode-prodno step.</summary>
    [HttpGet("lookup/product/{cProdNo}"), Authorize(Policy = "sales-orders")]
    public async Task<IActionResult> LookupProduct(string cProdNo)
    {
        var product = await salesOrderService.LookupProductAsync(cProdNo);
        if (product is null)
            return NotFound(new { success = false, message = $"Product {cProdNo} not found." });

        return Ok(new { success = true, data = product });
    }

    /// <summary>Typeahead suggestions for the Customer Key field. Also used by Pricelist Export.</summary>
    [HttpGet("search/customer")]
    [Authorize(Policy = "customer-search")]
    public async Task<IActionResult> SearchCustomers([FromQuery] string? term) =>
        Ok(new { success = true, data = await salesOrderService.SearchCustomersAsync(term ?? "") });

    /// <summary>Typeahead suggestions for the Prodno field.</summary>
    [HttpGet("search/product"), Authorize(Policy = "sales-orders")]
    public async Task<IActionResult> SearchProducts([FromQuery] string? term) =>
        Ok(new { success = true, data = await salesOrderService.SearchProductsAsync(term ?? "") });

    /// <summary>
    /// PO duplicate check. Always 200 — AlreadyEncoded is a warning, not an
    /// error, matching the legacy form which lets the operator continue.
    /// </summary>
    [HttpGet("check-po/{poNum}"), Authorize(Policy = "sales-orders")]
    public async Task<IActionResult> CheckPo(string poNum) =>
        Ok(new { success = true, data = await salesOrderService.CheckPoNumberAsync(poNum) });

    /// <summary>Import file-hash check — HARD BLOCK when AlreadyProcessed is true.</summary>
    [HttpGet("check-import-file/{fileHash}"), Authorize(Policy = "sales-orders")]
    public async Task<IActionResult> CheckImportFile(string fileHash) =>
        Ok(new { success = true, data = await salesOrderService.CheckFileImportedAsync(fileHash) });

    /// <summary>Fallback Customer+PO duplicate check — warning only, never blocks.</summary>
    [HttpPost("check-import-duplicates"), Authorize(Policy = "sales-orders")]
    public async Task<IActionResult> CheckImportDuplicates([FromBody] List<ImportCheckRowDto> rows) =>
        Ok(new { success = true, data = await salesOrderService.CheckRowDuplicatesAsync(rows) });

    /// <summary>Early PO-Number-only import check, run right after Next — HARD BLOCK when any match is found.</summary>
    [HttpPost("check-import-ponums"), Authorize(Policy = "sales-orders")]
    public async Task<IActionResult> CheckImportPoNumbers([FromBody] List<string> poNums) =>
        Ok(new { success = true, data = await salesOrderService.CheckPoNumbersImportedAsync(poNums) });

    /// <summary>Document Classification combo options.</summary>
    [HttpGet("docclasses"), Authorize(Policy = "sales-orders")]
    public async Task<IActionResult> GetDocClasses() =>
        Ok(new { success = true, data = await salesOrderService.GetDocClassesAsync() });

    /// <summary>
    /// Known Customer Identifier -> CustKey mappings, for pre-filling the
    /// "Import by Customer Name" mapping dialog. Pass identifiers found in
    /// the just-parsed Excel rows.
    /// </summary>
    [HttpGet("customer-identifier-map"), Authorize(Policy = "sales-orders")]
    public async Task<IActionResult> GetCustomerIdentifierMaps([FromQuery] string[] identifiers) =>
        Ok(new { success = true, data = await salesOrderService.GetCustomerIdentifierMapsAsync(identifiers) });

    /// <summary>Persists the encoder's Customer Identifier -> CustKey choices from the mapping dialog.</summary>
    [HttpPost("customer-identifier-map"), Authorize(Policy = "sales-orders")]
    public async Task<IActionResult> SaveCustomerIdentifierMaps([FromBody] List<SaveCustomerIdentifierMapDto> mappings)
    {
        await salesOrderService.SaveCustomerIdentifierMapsAsync(mappings);
        return Ok(new { success = true });
    }
}
