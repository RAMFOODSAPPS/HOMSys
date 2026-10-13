namespace HOMSys.Application.DTOs.Customers;

/// <summary>Customers page query — server-side search, filter, sort and paging (236k rows).</summary>
public class CustomerQuery
{
    /// <summary>Whitespace-separated words, each matched anywhere in key / name / address / zone / TIN.</summary>
    public string? Term { get; set; }
    public string? Branch { get; set; }
    /// <summary>active (default) | inactive | all.</summary>
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    /// <summary>One of CustomerMasterService.SortFields; default custKey.</summary>
    public string? SortField { get; set; }
    public bool SortDesc { get; set; }
}

public class CustomerPageDto
{
    public int Total { get; set; }
    public List<CustomerMasterDto> Rows { get; set; } = [];
}

/// <summary>One cust4win row (per branch folder).</summary>
public class CustomerMasterDto
{
    public int Id { get; set; }
    public string Branch { get; set; } = string.Empty;
    public string CustKey { get; set; } = string.Empty;
    public string CusName { get; set; } = string.Empty;
    public string AddrLn1 { get; set; } = string.Empty;
    public string AddrLn2 { get; set; } = string.Empty;
    public string DelAddrLn1 { get; set; } = string.Empty;
    public string DelAddrLn2 { get; set; } = string.Empty;
    public string DelArea { get; set; } = string.Empty;
    public int WhseNo { get; set; }
    public int ServeWh { get; set; }
    public int DelWhse { get; set; }
    public int Salesman { get; set; }
    public string CsMan { get; set; } = string.Empty;
    public int Term { get; set; }
    public int TermDays { get; set; }
    public string CZone { get; set; } = string.Empty;
    public string Tin { get; set; } = string.Empty;
    public string VatId { get; set; } = string.Empty;
    public string Subd { get; set; } = string.Empty;
    public bool Tpc { get; set; }
    public bool Offshore { get; set; }
    public bool ExBranch { get; set; }
    public bool BlockInv { get; set; }
    public bool Inactive { get; set; }
    public DateOnly? FirstOrder { get; set; }
    public DateTime ImportedAt { get; set; }
}

public class CustomerBranchCountDto
{
    public string Branch { get; set; } = string.Empty;
    public int Active { get; set; }
    public int Total { get; set; }
}
