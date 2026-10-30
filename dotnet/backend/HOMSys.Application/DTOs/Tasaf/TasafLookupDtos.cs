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
    /// <summary>Sales groups: its users (KAMs / proponents).</summary>
    public List<int> UserIds { get; set; } = [];
    public string? UserNames { get; set; }
    public decimal? Amount { get; set; }
    public string? Text { get; set; }
    /// <summary>Sales groups / chains: comma-separated area names (blank = all areas).</summary>
    public string? AreaNames { get; set; }
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
    /// <summary>Sales groups: its users; null = leave unchanged.</summary>
    public List<int>? UserIds { get; set; }
    public decimal? Amount { get; set; }
    public string? Text { get; set; }
    /// <summary>Sales groups / chains: area names; null = leave unchanged.</summary>
    public List<string>? AreaNames { get; set; }
}

/// <summary>Describes one master list to the setup page (which optional fields it shows).</summary>
public class TasafMasterMetaDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? ParentKey { get; set; }
    public string? ParentLabel { get; set; }
    public string? UserLabel { get; set; }
    /// <summary>Label of the optional multi-user field (sales groups: their KAMs / proponents).</summary>
    public string? UsersLabel { get; set; }
    public string? AmountLabel { get; set; }
    /// <summary>Label of the optional text field (chains: customer-name keywords).</summary>
    public string? TextLabel { get; set; }
    /// <summary>Label of the optional areas multi-select (sales groups / chains).</summary>
    public string? AreasLabel { get; set; }
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

/// <summary>TASAF Setup › Numbering.</summary>
public class TasafNumberingDto
{
    /// <summary>The number the next submitted TASAF gets (setting, or highest used + 1 if higher).</summary>
    public int NextNumber { get; set; }
    public int MinDigits { get; set; }
    /// <summary>Highest proposal number already used (null = none yet).</summary>
    public int? LastNumber { get; set; }
    public string? LastRefNo { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class SaveTasafNumberingDto
{
    public int NextNumber { get; set; }
    public int MinDigits { get; set; }
}
