namespace HOMSys.Domain.Entities;

/// <summary>
/// Sales classification of a customer, owned by HOMSys (one row per CustKey, across branch folders) —
/// first loaded from "RAM BRANCHES ACTIVE CUSTOMER LIST.xlsx" (2026-10-26). Kept apart from Customer so the
/// cust4win sync never overwrites it; the branches' own cust4win codes (NREGION / NSLGRP / NACCOUNT) stay
/// on Customer as they are and use an older coding.
/// </summary>
public class CustomerClassification
{
    public int Id { get; set; }

    /// <summary>cust4win.CUSTKEY C(7).</summary>
    public string CustKey { get; set; } = string.Empty;

    /// <summary>NCR, North Luzon, South Luzon, Visayas, West Mindanao, Central Mindanao, MT/NKA.</summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>NKA, RKA, GT, LKA.</summary>
    public string SalesGroup { get; set; } = string.Empty;

    /// <summary>The branch that services the account, e.g. "Mexico Sales", "Cebu", "ADC - Visayas".</summary>
    public string ServicingBranch { get; set; } = string.Empty;

    /// <summary>Customer / chain / group, e.g. "SVI - Main", "LKA Visayas", "RDP Mindanao".</summary>
    public string ChainGroup { get; set; } = string.Empty;

    /// <summary>Where the values came from (file name and date of the load).</summary>
    public string Source { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}
