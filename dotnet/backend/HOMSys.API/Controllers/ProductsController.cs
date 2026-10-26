using HOMSys.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMSys.API.Controllers;

/// <summary>Read-only product master (synced from BMS PROD4WIN).</summary>
[ApiController]
[Route("api/products")]
[Authorize(Policy = "products")]
public class ProductsController(ProductCatalogService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(new { success = true, data = await service.GetAllAsync() });

    [HttpGet("{prodNo:int}/price-history")]
    public async Task<IActionResult> PriceHistory(int prodNo) =>
        Ok(new { success = true, data = await service.GetPriceHistoryAsync(prodNo) });
}
