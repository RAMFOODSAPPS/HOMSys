using HOMSys.Domain.Entities;

namespace HOMSys.Application.DTOs.Pricing;

/// <summary>
/// One PriceList=true Product joined to its (optional) ProductCategory,
/// pre-ordered for pricelist export. CategoryHeader is null when the
/// product has no matching ProductCategory row.
/// </summary>
public record PriceListProductRow(Product Product, string? CategoryHeader);
