namespace HOMSys.Application.DTOs.Tasaf;

public class TasafLookupDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public int? ParentId { get; set; }
    public string? ParentName { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public decimal? Amount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class SaveTasafLookupDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public int? ParentId { get; set; }
    public int? UserId { get; set; }
    public decimal? Amount { get; set; }
}

/// <summary>Describes one master list to the setup page (which optional fields it shows).</summary>
public class TasafMasterMetaDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? ParentKey { get; set; }
    public string? ParentLabel { get; set; }
    public string? UserLabel { get; set; }
    public string? AmountLabel { get; set; }
}

/// <summary>Approval rules: approval types × roles → not required / always / over amount.</summary>
public class TasafApprovalRulesDto
{
    public List<TasafLookupDto> Types { get; set; } = [];
    public List<TasafLookupDto> Roles { get; set; } = [];
    public List<TasafApprovalRuleCellDto> Rules { get; set; } = [];
}

public class TasafApprovalRuleCellDto
{
    public int ApprovalTypeId { get; set; }
    public int RoleId { get; set; }
    /// <summary>False = the role does not sign this approval type.</summary>
    public bool Required { get; set; }
    /// <summary>Signs only when the total is MORE THAN this; null = always.</summary>
    public decimal? OverAmount { get; set; }
}

/// <summary>Approval matrix: sales groups × approval roles → user (null = vacant).</summary>
public class TasafApprovalMatrixDto
{
    public List<TasafLookupDto> SalesGroups { get; set; } = [];
    public List<TasafLookupDto> Roles { get; set; } = [];
    public List<TasafApproverCellDto> Cells { get; set; } = [];
}

public class TasafApproverCellDto
{
    public int SalesGroupId { get; set; }
    public int RoleId { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
}

public class TasafUserOptionDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}
