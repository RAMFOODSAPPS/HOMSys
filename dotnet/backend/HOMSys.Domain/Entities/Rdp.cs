namespace HOMSys.Domain.Entities;

/// <summary>
/// One RAM Distributor Partner (RDP) code, seeded from the "RDP List" sheet of
/// "RAM RDP Sales Raw Data Template.xlsx". One CustKey per distributor and servicing branch,
/// so a distributor with two branches has two rows (e.g. CERSUM at DAVAO and GEN. SANTOS).
/// </summary>
public class RdpDistributor
{
    public int Id { get; set; }

    /// <summary>RAM's distributor code (the RDP CUSTKEY column of the template).</summary>
    public int CustKey { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Regional distributor manager accountable for this distributor.</summary>
    public string Rdm { get; set; } = string.Empty;

    /// <summary>Area tagging, e.g. "NCL GT", "MIN GT".</summary>
    public string Area { get; set; } = string.Empty;

    /// <summary>Luzon / Visayas / Mindanao.</summary>
    public string Territory { get; set; } = string.Empty;

    /// <summary>The RAM site that services this distributor.</summary>
    public int? SiteId { get; set; }
    public Site? Site { get; set; }

    /// <summary>
    /// What the RDP's own files call it in a distributor column, ";"-separated (GT MIN's "DSDI", "CERSUM South Cotabato").
    /// A file naming the distributor resolves by these, the full name, or a first word only one RDP has ("BERTAHAN").
    /// </summary>
    public string FileNames { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Maps a channel label as it appears in distributor files to RAM's standard channel type
/// ("Reference" sheet). Resolved at query time, so a new mapping re-labels past uploads too.
/// </summary>
public class RdpChannelMap
{
    public int Id { get; set; }

    /// <summary>Normalised label: trimmed, upper case, single spaces.</summary>
    public string Label { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>One imported distributor file. Its lines replace that distributor's months it contains.</summary>
public class RdpSalesUpload
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;

    /// <summary>Comma-separated CustKeys found in the file.</summary>
    public string CustKeys { get; set; } = string.Empty;

    /// <summary>Comma-separated yyyy-MM periods found in the file.</summary>
    public string Periods { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public decimal NetAmount { get; set; }

    /// <summary>Lines of earlier uploads that this one replaced.</summary>
    public int ReplacedLines { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string UploadedBy { get; set; } = string.Empty;
}

/// <summary>One row of a distributor's sales file (template columns), after normalisation.</summary>
public class RdpSalesLine
{
    public long Id { get; set; }
    public int UploadId { get; set; }
    public int DistributorId { get; set; }

    /// <summary>First day of the reporting month.</summary>
    public DateOnly Period { get; set; }
    public DateOnly? InvoiceDate { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;

    /// <summary>Invoice / Credit Memo / Return / Discount / Free Goods / Adjustment, or blank when the file has none.</summary>
    public string TxnType { get; set; } = string.Empty;

    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;

    /// <summary>Identity of the outlet within its distributor: the account code, else "name|city" (see RdpSalesParser.AccountKey).</summary>
    public string AccountKey { get; set; } = string.Empty;

    /// <summary>The channel label as sent (normalised); mapped through RdpChannelMap when reporting.</summary>
    public string ChannelLabel { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Salesman { get; set; } = string.Empty;

    /// <summary>RAM SKU code (Products.CProdNo).</summary>
    public string SkuCode { get; set; } = string.Empty;
    public string ItemDesc { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = string.Empty;

    /// <summary>Quantity in cases (pieces ÷ pack size); null when the unit or SKU is unknown.</summary>
    public decimal? Cases { get; set; }
    public decimal NetAmount { get; set; }
}
