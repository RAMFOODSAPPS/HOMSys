namespace HOMSys.Application.DTOs.Tasaf;

public class TasafBranchDto
{
    public int? SiteId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Site code (form options only), matched against an area's BranchCodes.</summary>
    public string? Code { get; set; }
}

public class TasafCustomerDto
{
    public string CustKey { get; set; } = string.Empty;
    public string CusName { get; set; } = string.Empty;
}

public class TasafProductDto
{
    public string CProdNo { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal? P3MoAvgCs { get; set; }
    public decimal? SplyCs { get; set; }
    public decimal PromotedVolCs { get; set; }
    public decimal IncrVolCs { get; set; }
    public decimal LspPerCase { get; set; }
    /// <summary>
    /// Server-computed price of the picked stores (TasafPriceService, read-only); null = no stores
    /// picked or they have different prices. LspPerCase ≠ this = changed by the proponent.
    /// </summary>
    public decimal? LspComputed { get; set; }
    public decimal PromotedValue { get; set; }
}

/// <summary>POST tasafs/product-prices: price these SKUs for these stores.</summary>
public class TasafPriceRequest
{
    public List<string> CProdNos { get; set; } = [];
    public List<string> CustKeys { get; set; } = [];
    /// <summary>Program start date; null = today (Manila).</summary>
    public DateOnly? AsOf { get; set; }
}

/// <summary>One SKU's price across the picked stores, grouped by price (most stores first).</summary>
public class TasafSkuPriceDto
{
    public string CProdNo { get; set; } = string.Empty;
    /// <summary>False = the product or its base price was not found.</summary>
    public bool HasPrice { get; set; }
    public List<TasafPriceGroupDto> Groups { get; set; } = [];
}

public class TasafPriceGroupDto
{
    /// <summary>LSP per case (see TasafPriceService.IncludesVat).</summary>
    public decimal Price { get; set; }
    public List<string> CustKeys { get; set; } = [];
}

public class TasafCostDto
{
    public int? CostTypeId { get; set; }
    public string? CostTypeName { get; set; }
    public decimal Amount { get; set; }
    public string Remarks { get; set; } = string.Empty;
}

/// <summary>Body of create/update. Computed fields (totals, promoted value) are ignored and recalculated.</summary>
public class SaveTasafDto
{
    public string Title { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? SalesGroupId { get; set; }
    /// <summary>Areas covered (multi-select).</summary>
    public List<int> AreaIds { get; set; } = [];
    public int? ChainId { get; set; }
    public int? ProgramTypeId { get; set; }
    public string ProgramTypeOther { get; set; } = string.Empty;
    public int? ObjectiveId { get; set; }
    public string ObjectiveOther { get; set; } = string.Empty;
    public int? TargetMarketId { get; set; }
    public string TargetMarketOther { get; set; } = string.Empty;
    /// <summary>Channels (multi-select).</summary>
    public List<int> ChannelIds { get; set; } = [];
    /// <summary>Product Categories (multi-select; "product-groups" list).</summary>
    public List<int> ProductCategoryIds { get; set; } = [];
    /// <summary>Product Sub-Categories = product master category codes ("2a"); multi-select, optional —
    /// none for a picked Product Category = the whole category.</summary>
    public List<string> SubCategoryCodes { get; set; } = [];
    public string Background { get; set; } = string.Empty;
    public string Concessions { get; set; } = string.Empty;
    public string Mechanics { get; set; } = string.Empty;
    /// <summary>Why CTS is over the program type's limit — required to submit when it is.</summary>
    public string CtsJustification { get; set; } = string.Empty;
    public decimal? PromotedValue { get; set; }
    public decimal? PrePromoSales { get; set; }
    public decimal? IncrementalSales { get; set; }
    public decimal PayInvDeductionAp { get; set; }
    public decimal PayInvDeductionSalesman { get; set; }
    public decimal PayCheckAp { get; set; }
    public decimal PayCheckSalesman { get; set; }
    public decimal PayFreeGoodsAp { get; set; }
    public decimal PayFreeGoodsSalesman { get; set; }
    public decimal PayOffInvoiceAp { get; set; }
    public decimal PayOffInvoiceSalesman { get; set; }
    public List<TasafBranchDto> Branches { get; set; } = [];
    /// <summary>Participating stores (Cust Keys). Names are re-read from the customer master on save.</summary>
    public List<TasafCustomerDto> Customers { get; set; } = [];
    public List<TasafProductDto> Products { get; set; } = [];
    public List<TasafCostDto> Costs { get; set; } = [];
}

public class TasafDto : SaveTasafDto
{
    public int Id { get; set; }
    public string? RefNo { get; set; }
    public string Status { get; set; } = string.Empty;

    // Display names of the picked lookups.
    public string? SalesGroupName { get; set; }
    /// <summary>Picked area names, comma-separated.</summary>
    public string? AreaNames { get; set; }
    public string? ChainName { get; set; }
    public string? ProgramTypeName { get; set; }
    public string? ObjectiveName { get; set; }
    public string? TargetMarketName { get; set; }
    /// <summary>Picked channel names, comma-separated.</summary>
    public string? ChannelNames { get; set; }
    /// <summary>Picked Product Category / Sub-Category ("2a - Peas") names, comma-separated; Segments =
    /// the picked sub-categories' segments ("Green Peas, Garbanzos"), automatic.</summary>
    public string? ProductCategoryNames { get; set; }
    public string? SubCategoryNames { get; set; }
    public string? SegmentNames { get; set; }

    public decimal TotalCost { get; set; }
    /// <summary>PromotedValue / PrePromoSales.</summary>
    public decimal? PromotedVsPrePromo { get; set; }
    /// <summary>IncrementalSales / PrePromoSales.</summary>
    public decimal? IncrementalVsPrePromo { get; set; }
    /// <summary>Cost-to-sales: TotalCost / PromotedValue.</summary>
    public decimal? Cts { get; set; }
    /// <summary>The program type's Max CTS % (e.g. 8); null = no limit (Account Support / Convention: 8 is the guide).</summary>
    public decimal? CtsLimitPercent { get; set; }
    public decimal PaymentTotal { get; set; }
    /// <summary>TotalCost − PaymentTotal; must be 0 to submit.</summary>
    public decimal PaymentVariance { get; set; }

    public int ProponentUserId { get; set; }
    public string ProponentName { get; set; } = string.Empty;
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    /// <summary>Admin: send an Approved / Rejected / For Approval TASAF back to the proponent.</summary>
    public bool CanRevert { get; set; }
    /// <summary>Proponent or Admin: withdraw it (any status but Approved / Cancelled).</summary>
    public bool CanCancel { get; set; }
    /// <summary>"approve" when the current step is waiting on the caller, else null.</summary>
    public string? MyAction { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public int? CurrentStepSeq { get; set; }
    /// <summary>Approval matrix used at submit, e.g. "Promo / Near Ex Moveout".</summary>
    public string ApprovalTypeName { get; set; } = string.Empty;
    public string? CurrentRoleCode { get; set; }
    /// <summary>The approval chain, in signing order (vacant roles show as Skipped).</summary>
    public List<TasafStepDto> Steps { get; set; } = [];
    public List<TasafActionDto> Actions { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class TasafStepDto
{
    public int Seq { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    /// <summary>Pending, Approved, Returned, Rejected, Skipped.</summary>
    public string Status { get; set; } = string.Empty;
    public DateTime? ActedAt { get; set; }
    public string Comment { get; set; } = string.Empty;
}

public class TasafActionDto
{
    public string Action { get; set; } = string.Empty;
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string ByName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public DateTime At { get; set; }
}

/// <summary>GKAM review or final approval. Decision: approve | return | reject.</summary>
public class TasafDecisionDto
{
    public string Decision { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}

/// <summary>Revert / cancel — the reason is required.</summary>
public class TasafCommentDto
{
    public string Comment { get; set; } = string.Empty;
}

/// <summary>List-page row.</summary>
public class TasafListItemDto
{
    public int Id { get; set; }
    public string? RefNo { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? SalesGroupName { get; set; }
    public string? ChainName { get; set; }
    /// <summary>First store name, "+N more" when several (list display).</summary>
    public string? CustomerName { get; set; }
    public string? ProgramTypeName { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal TotalCost { get; set; }
    public string ProponentName { get; set; } = string.Empty;
    /// <summary>Role code / approver of the step it is waiting on (For Approval only).</summary>
    public string? CurrentRoleCode { get; set; }
    public string? CurrentApproverName { get; set; }
    /// <summary>Non-skipped steps in the chain, and how many have signed.</summary>
    public int StepCount { get; set; }
    public int StepsDone { get; set; }
    public bool IsMine { get; set; }
    /// <summary>Waiting on the caller as the current step's approver.</summary>
    public bool AwaitingMe { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class TasafOptionDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    /// <summary>Program types: Max CTS %.</summary>
    public decimal? Amount { get; set; }
    /// <summary>Areas: comma-separated Site codes (blank = all branches).</summary>
    public string? BranchCodes { get; set; }
    /// <summary>Sales groups / chains: comma-separated area names they're offered for (blank = all areas).</summary>
    public string? AreaNames { get; set; }
}

/// <summary>Every dropdown the form needs, in one call. Keys match TasafMasterService.Masters.</summary>
public class TasafFormOptionsDto
{
    public Dictionary<string, List<TasafOptionDto>> Lists { get; set; } = [];
    /// <summary>Active Sites, then the "ALL ..." scopes (Id null).</summary>
    public List<TasafBranchDto> Branches { get; set; } = [];
    /// <summary>Product Sub-Categories from the product master, for categories in the Product Categories list.</summary>
    public List<TasafSubCategoryOptionDto> SubCategories { get; set; } = [];
    /// <summary>
    /// The user's own sales groups (TASAF Setup › Sales Groups › Users) when the Sales Group dropdown is
    /// limited to them; empty = every group (admins, View All, or a user in no group).
    /// </summary>
    public List<int> MySalesGroupIds { get; set; } = [];
}

/// <summary>A product master category as a TASAF Sub-Category option: "2a" · Peas · Green Peas.</summary>
public class TasafSubCategoryOptionDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Segment { get; set; } = string.Empty;
    /// <summary>The Product Categories row (code "02" = GroupNo 2).</summary>
    public int ProductCategoryId { get; set; }
}

public class TasafProductLookupDto
{
    public string CProdNo { get; set; } = string.Empty;
    public string ProdDesc { get; set; } = string.Empty;
    public string PackSize { get; set; } = string.Empty;
    public int Pieces { get; set; }
    /// <summary>prod4win.NEWPRICE (ex-VAT) — prefilled as LSP/Case, editable.</summary>
    public decimal? LspPerCase { get; set; }
}

/// <summary>Participating SKU dropdown option (active products of the picked group/category).</summary>
public class TasafProductOptionDto
{
    public string CProdNo { get; set; } = string.Empty;
    public string ProdDesc { get; set; } = string.Empty;
    public string PackSize { get; set; } = string.Empty;
    /// <summary>Product category code (e.g. "1a") and its name.</summary>
    public string Category { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    /// <summary>prod4win.NEWPRICE (ex-VAT) — prefilled as LSP/Case.</summary>
    public decimal? LspPerCase { get; set; }
}

/// <summary>The no-login decision page opened from an approval email's Approve / Reject button.</summary>
public class TasafEmailDecisionDto
{
    public string? RefNo { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ApproverName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    /// <summary>False once decided / no longer this approver's turn / expired — Message says why.</summary>
    public bool CanDecide { get; set; }
    public string? Message { get; set; }
    /// <summary>The whole TASAF, same layout as the email (server-rendered, every value HTML-encoded).</summary>
    public string Html { get; set; } = string.Empty;
}
