namespace HOMSys.Application.DTOs.Pricing;

/// <summary>Display-only price quote for the Sales Order encode grid. Never persisted.</summary>
public class PriceQuoteDto
{
    public bool HasPrice { get; set; }

    /// <summary>prod4win.NewPrice (or price history) + zone.add_on + zone2.add_on, ex-VAT, per case.
    /// LP w/ VAT is derived client-side as PricePerCase * QtyCs * 1.12 so it recalculates
    /// on every qty edit without a re-quote.</summary>
    public decimal? PricePerCase { get; set; }
}
