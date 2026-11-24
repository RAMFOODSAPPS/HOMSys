namespace HOMSys.Domain.Entities;

/// <summary>
/// The deduction selections an encoder picks (finance's list, user 2026-11-21): each is one of HOMSys's DeductionTypes and
/// one of HOMS's ordeduc2 deduction codes (DEDCODE / DEDTYPE, the BGATR selection) — HOMS's own code where HOMS has one
/// (Short Payment 12, Listing Fee 3, Mass Display 2, Freight 9). Remark = the short text put first in ordeduc2 REMARKS (50),
/// before the Ref. EWT (2307) is the existing selection: an OR line (10507 with the 2307, 10508 without), never ordeduc2.
/// </summary>
public static class DeductionReasons
{
    public sealed record Reason(string Code, string Label, DeductionType Type, int DedCode, string DedType, string Remark);

    public const string Ewt = "EWT", Others = "OTHERS", ServiceInvoice = "SERVICE_INVOICE";

    private const string Bo = "BO RETURNS", Disc = "UNREFLECTED DISCOUNT", Pen = "SERVICE PENALTY", Oth = "OTHERS";

    public static readonly IReadOnlyList<Reason> All =
    [
        new(Ewt, "EWT (2307)", DeductionType.Ewt2307, 14, "CEWIT", "CEWIT"),
        new("RETURN_CUSTOMER", "Return from customer (RUD/RSR/RFC)", DeductionType.RfcBoRtv, 11, Bo, "RETURN"),
        new("RFC_AMOUNT_DIFF", "RFC amount difference", DeductionType.RfcBoRtv, 11, Bo, "RFC AMT DIFF"),
        new("UNREFLECTED_DISCOUNT", "Unreflected discount (regular, VIP, special, etc.)", DeductionType.Discount, 1, Disc, "UNREF DISC"),
        new("REBATES", "Rebates (price roll back, in-store bundling, etc.)", DeductionType.Discount, 1, Disc, "REBATES"),
        new("MERCH_PENALTY", "Merchandisers penalty – charge to APEX", DeductionType.Penalty, 16, Pen, "MERCH PENALTY-APEX"),
        new("CUSTOMER_PENALTY", "Customer penalty (confiscated items, late deliveries, etc.)", DeductionType.Penalty, 16, Pen, "CUSTOMER PENALTY"),
        new("SALESMAN_PENALTY", "Penalty to salesman (no unserved letter, price list, etc.)", DeductionType.Penalty, 16, Pen, "SALESMAN PENALTY"),
        new("SHORT_PAYMENT", "Short payment", DeductionType.Other, 12, "SHORT PAYMENT", "SHORT PAYMENT"),
        new("LISTING_FEE", "Listing fee", DeductionType.Other, 3, "LISTING FEE", "LISTING FEE"),
        new("MASS_DISPLAY", "Mass display", DeductionType.Other, 2, "MASS DISPLAY", "MASS DISPLAY"),
        new("FREIGHT", "Freight charges / allowance", DeductionType.Other, 9, "FREIGHT CHARGES", "FREIGHT"),
        new("DISPLAY_RENTAL", "Display rental allowance", DeductionType.DisplayAllowance, 15, Oth, "DISPLAY RENTAL"),
        new("PARKING_FEE", "Parking fee", DeductionType.Other, 15, Oth, "PARKING FEE"),
        new("ADDBACK", "Addback – unauthorized deductions", DeductionType.Other, 15, Oth, "ADDBACK-UNAUTH DED"),
        new("SALARY_WAGE", "Salary wage allowance", DeductionType.Other, 15, Oth, "SALARY WAGE ALLOW"),
        new("TSAC", "TSAC (AFCES to customer / government customer)", DeductionType.Other, 15, Oth, "TSAC"),
        new("PRICE_DISCREPANCY", "Price discrepancy", DeductionType.Other, 15, Oth, "PRICE DISCREPANCY"),
        new("VENDORS_FEE", "Vendors fee", DeductionType.Other, 15, Oth, "VENDORS FEE"),
        new("SUPPORT", "All kinds of support (opening, promo, anniv., Christmas, etc.)", DeductionType.Other, 15, Oth, "SUPPORT"),
        new("PROCESSING_FEE", "Processing fee", DeductionType.Other, 15, Oth, "PROCESSING FEE"),
        new("LABOR_CHARGES", "Labor charges", DeductionType.Other, 15, Oth, "LABOR CHARGES"),
        new(ServiceInvoice, "Service Invoice (scanned copy required)", DeductionType.ServiceInvoice, 15, Oth, "SERVICE INV"),
        new(Others, "Others (unspecified) – describe", DeductionType.Other, 15, Oth, ""),
    ];

    private static readonly Dictionary<string, Reason> ByCode = All.ToDictionary(r => r.Code, StringComparer.OrdinalIgnoreCase);

    public static Reason? Find(string? code) => string.IsNullOrWhiteSpace(code) ? null : ByCode.GetValueOrDefault(code.Trim());

    /// <summary>The selection a deduction saved before the list existed stands for (by its type).</summary>
    public static Reason ForType(DeductionType t) => ByCode[t switch
    {
        DeductionType.Ewt2307 => Ewt,
        DeductionType.Discount => "UNREFLECTED_DISCOUNT",
        DeductionType.Penalty => "CUSTOMER_PENALTY",
        DeductionType.RfcBoRtv => "RETURN_CUSTOMER",
        DeductionType.DisplayAllowance => "DISPLAY_RENTAL",
        DeductionType.ServiceInvoice => ServiceInvoice,
        _ => Others,
    }];

    public static Reason Of(CollectionDeduction d) => Find(d.Reason) ?? ForType(d.Type);
}
