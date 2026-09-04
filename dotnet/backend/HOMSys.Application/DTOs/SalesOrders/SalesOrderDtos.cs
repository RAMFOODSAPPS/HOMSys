namespace HOMSys.Application.DTOs.SalesOrders;

public class CreateSalesOrderLineDto
{
    public string CProdNo { get; set; } = string.Empty;
    public int QtyCs { get; set; }
    public int QtyPc { get; set; }
    public bool FreeGoods { get; set; }
}

public class CreateSalesOrderDto
{
    public string CustKey { get; set; } = string.Empty;
    public string PoNum { get; set; } = string.Empty;
    public DateOnly? PoDate { get; set; }

    /// <summary>HOMSys-only field — no legacy oowkhdr column.</summary>
    public DateOnly? CancelDate { get; set; }

    public string InvRem { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;

    // O.R. details — only meaningful for cash customers (Term = 0).
    public int? OrNo { get; set; }
    public DateOnly? ChkDate { get; set; }
    public decimal? OrAmt { get; set; }

    /// <summary>Document Classification code (docclass.DBF). Not term-gated.</summary>
    public string? DocClass { get; set; }

    /// <summary>Set by the client when the encoding session started, for the speed metric.</summary>
    public DateTime? SoTymStart { get; set; }

    /// <summary>SHA-256 hash of the source import file, set only when this order
    /// originated from the import wizard. Null for manually encoded orders.</summary>
    public string? SourceFileHash { get; set; }

    /// <summary>Original filename of the import source. Null for manually encoded orders.</summary>
    public string? SourceFileName { get; set; }

    public List<CreateSalesOrderLineDto> Lines { get; set; } = [];
}

public class SalesOrderLineDto
{
    public int Id { get; set; }
    public int LineNo { get; set; }
    public string CProdNo { get; set; } = string.Empty;
    public int ProdNo { get; set; }
    public string ProdDesc { get; set; } = string.Empty;
    public string PackSize { get; set; } = string.Empty;
    public int QtyCs { get; set; }
    public int QtyPc { get; set; }
    public int Pieces { get; set; }
    public string Um { get; set; } = string.Empty;
    public bool PriceList { get; set; }
    public decimal TaxRate { get; set; }
    public bool FreeGoods { get; set; }

    /// <summary>BMS-owned — last oowkdet.QTYCS/QTYPC read by the oos-status bridge
    /// sync, taken right before allocate() would delete a full-OOS row. Null
    /// until the first sync; 0 means fully out of stock.</summary>
    public int? AllocatedQtyCs { get; set; }
    public int? AllocatedQtyPc { get; set; }
    public int? StkFlag { get; set; }

    /// <summary>BMS-owned — last oowkdet.NETAMT read by the oos-status bridge
    /// sync, same snapshot as AllocatedQtyCs. Null until the first sync; 0
    /// means fully out of stock.</summary>
    public decimal? InvNetAmt { get; set; }
}

public class SalesOrderDto
{
    public int SoId { get; set; }

    /// <summary>Null until the Python bridge pushes the order into BMS.</summary>
    public int? SoNo { get; set; }

    public string CustKey { get; set; } = string.Empty;
    public string CusName { get; set; } = string.Empty;
    public DateOnly OrderDate { get; set; }
    public string PoNum { get; set; } = string.Empty;
    public DateOnly? PoDate { get; set; }

    /// <summary>HOMSys-only field — no legacy oowkhdr column.</summary>
    public DateOnly? CancelDate { get; set; }

    public string InvRem { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;

    public string ShipToLn1 { get; set; } = string.Empty;
    public string ShipToLn2 { get; set; } = string.Empty;
    public int Term { get; set; }
    public int Salesman { get; set; }
    public string CsMan { get; set; } = string.Empty;

    public int? OrNo { get; set; }
    public DateOnly? ChkDate { get; set; }
    public decimal? OrAmt { get; set; }

    public string? DocClass { get; set; }

    /// <summary>BMS-owned — set by the Python bridge once the order is invoiced.</summary>
    public int? InvNo { get; set; }
    public DateOnly? InvDate { get; set; }
    public decimal? InvAmt { get; set; }

    /// <summary>True once pushed to BMS (SoNo assigned) — cleared again once BMS
    /// deallocates the order. Edits are refused while true.</summary>
    public bool IsLocked { get; set; }

    /// <summary>True while a post-deallocation HOMSys edit is waiting to be
    /// pushed into BMS's live oowkhdr/oowkdet record.</summary>
    public bool NeedsResync { get; set; }

    /// <summary>True if BMS could not find the live record to apply the last
    /// resync onto — surfaced instead of leaving NeedsResync stuck true.</summary>
    public bool ResyncFailed { get; set; }

    /// <summary>Entered / Downloaded / Processed / Deallocated / Invoiced. Display-only.</summary>
    public string WorkflowStatus { get; set; } = "Entered";

    /// <summary>
    /// Encode-time estimate, computed from current price quotes — NOT the
    /// BMS-owned invoiced amount. Sum of PricePerCase * QtyCs * 1.12 across
    /// lines, same formula the encode grid's running total uses.
    /// </summary>
    public decimal EstAmt { get; set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;

    public List<SalesOrderLineDto> Lines { get; set; } = [];
}

/// <summary>Customer context returned when the operator keys a customer key.</summary>
public class CustomerLookupDto
{
    public string CustKey { get; set; } = string.Empty;
    public string CusName { get; set; } = string.Empty;
    public string CKey { get; set; } = string.Empty;
    public int WhseNo { get; set; }
    public int CustWhse { get; set; }
    public int Term { get; set; }
    public int TermDays { get; set; }
    public int Salesman { get; set; }
    public string CsMan { get; set; } = string.Empty;
    public string ShipToLn1 { get; set; } = string.Empty;
    public string ShipToLn2 { get; set; } = string.Empty;
    public string DelArea { get; set; } = string.Empty;
    public string VatId { get; set; } = string.Empty;
    public bool Tpc { get; set; }
    public bool Offshore { get; set; }
    public bool ExBranch { get; set; }
    public int CCode { get; set; }

    /// <summary>True when Term = 0 — the form enables the O.R. fields.</summary>
    public bool IsCash { get; set; }
}

public class ProductLookupDto
{
    public string CProdNo { get; set; } = string.Empty;
    public int ProdNo { get; set; }
    public string ProdDesc { get; set; } = string.Empty;
    public string PackSize { get; set; } = string.Empty;
    public int Pieces { get; set; }
    public int QtyPerPc { get; set; }
    public string Um { get; set; } = string.Empty;
    public bool PriceList { get; set; }
    public decimal TaxRate { get; set; }
    public int Supplier { get; set; }
}

/// <summary>Typeahead suggestion for the Customer Key field.</summary>
public class CustomerSuggestionDto
{
    public string CustKey { get; set; } = string.Empty;
    public string CusName { get; set; } = string.Empty;
}

/// <summary>Typeahead suggestion for the Prodno field.</summary>
public class ProductSuggestionDto
{
    public string CProdNo { get; set; } = string.Empty;
    public string ProdDesc { get; set; } = string.Empty;
    public string PackSize { get; set; } = string.Empty;
    public int Pieces { get; set; }
}

/// <summary>Document Classification combo option (docclass.DBF).</summary>
public class DocClassDto
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Result of the PO duplicate check. This is a warning only — the legacy form
/// shows an OK-only messagebox and keeps the value, so the client must not
/// treat AlreadyEncoded as a blocking error.
/// </summary>
public class PoCheckDto
{
    public string PoNum { get; set; } = string.Empty;
    public bool AlreadyEncoded { get; set; }
    public int? PreviousSoNo { get; set; }
    public DateOnly? PreviousOrderDate { get; set; }
    public string PreviousCustKey { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// One order awaiting write-back, as returned by
/// GET /api/salesorders/bridge/pending. Carries everything the Python bridge
/// needs to build oowkhdr/oowkdet + POFILES rows without a second lookup.
/// </summary>
public class BridgePendingOrderDto
{
    public int SoId { get; set; }

    /// <summary>Set only on a resync payload (GET .../bridge/resync-pending) —
    /// null for a brand-new order, since BMS hasn't assigned one yet.</summary>
    public int? SoNo { get; set; }

    /// <summary>Set only on a resync payload — see SoNo.</summary>
    public int? DocNo { get; set; }
    public string CustKey { get; set; } = string.Empty;
    public string CusName { get; set; } = string.Empty;
    public string CKey { get; set; } = string.Empty;
    public DateOnly OrderDate { get; set; }
    public string PoNum { get; set; } = string.Empty;
    public DateOnly? PoDate { get; set; }
    public string InvRem { get; set; } = string.Empty;

    public int CCode { get; set; }
    public int WhseNo { get; set; }
    public string ShipToLn1 { get; set; } = string.Empty;
    public string ShipToLn2 { get; set; } = string.Empty;
    public int Term { get; set; }
    public int TermDays { get; set; }
    public int Salesman { get; set; }
    public string CsMan { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>cust4win.SERVEWH, looked up by CustKey — falls back to WhseNo when 0.</summary>
    public int ServeWh { get; set; }

    /// <summary>cust4win.DELWHSE, looked up by CustKey.</summary>
    public int DelWhse { get; set; }

    public List<BridgePendingLineDto> Lines { get; set; } = [];
}

public class BridgePendingLineDto
{
    public string CProdNo { get; set; } = string.Empty;
    public int ProdNo { get; set; }
    public string ProdDesc { get; set; } = string.Empty;
    public string PackSize { get; set; } = string.Empty;
    public int QtyCs { get; set; }
    public int QtyPc { get; set; }
    public int Pieces { get; set; }
    public string Um { get; set; } = string.Empty;
    public int Supplier { get; set; }
    public string CSupplier { get; set; } = string.Empty;
    public bool PriceList { get; set; }
    public decimal TaxRate { get; set; }
}

/// <summary>Body of POST /api/salesorders/bridge/{soId}/confirm.</summary>
public class BridgeConfirmDto
{
    public int SoNo { get; set; }
    public int DocNo { get; set; }
}

/// <summary>Body of POST /api/salesorders/bridge/{soId}/resync-confirm.</summary>
public class BridgeResyncConfirmDto
{
    public bool Ok { get; set; }
}

/// <summary>Body of POST /api/salesorders/bridge/{soId}/invoice.</summary>
public class BridgeInvoiceDto
{
    public int InvNo { get; set; }
    public DateOnly InvDate { get; set; }
    public decimal InvAmt { get; set; }
}

/// <summary>One SKU line's live oowkdet state, as read by the bridge right after
/// allocate() computes it and before a full-OOS row would be deleted from
/// oowkdet.</summary>
public class BridgeOosLineDto
{
    public string CProdNo { get; set; } = string.Empty;
    public int QtyCs { get; set; }
    public int QtyPc { get; set; }
    public int? StkFlag { get; set; }
    public decimal? NetAmt { get; set; }
}

/// <summary>Body of POST /api/salesorders/bridge/{soId}/oos-status. A full
/// snapshot of every oowkdet line still present for this SO at sync time —
/// any SalesOrderLine not included is treated as fully out of stock.</summary>
public class BridgeOosStatusDto
{
    public List<BridgeOosLineDto> Lines { get; set; } = [];
}

/// <summary>
/// A known Customer Identifier -> CustKey mapping, returned to pre-fill the
/// "Import by Customer Name" mapping dialog for identifiers seen before.
/// </summary>
public class CustomerIdentifierMapDto
{
    public string Identifier { get; set; } = string.Empty;
    public string CustKey { get; set; } = string.Empty;
    public string CusName { get; set; } = string.Empty;
}

/// <summary>One row of the mapping dialog's Save payload.</summary>
public class SaveCustomerIdentifierMapDto
{
    public string Identifier { get; set; } = string.Empty;
    public string CustKey { get; set; } = string.Empty;
}

/// <summary>One Customer Key + PO Number pair to check for an existing Sales Order.</summary>
public class ImportCheckRowDto
{
    public string CustKey { get; set; } = string.Empty;
    public string PoNum { get; set; } = string.Empty;
}

/// <summary>
/// Result of the import file-hash check. Unlike <see cref="PoCheckDto"/>, this
/// IS a hard block — the client must stop the import wizard when AlreadyProcessed.
/// </summary>
public class FileImportCheckResultDto
{
    public bool AlreadyProcessed { get; set; }
    public DateTime? FirstProcessedAt { get; set; }
    public string? FirstProcessedBy { get; set; }
}

/// <summary>
/// Result of the fallback Customer+PO duplicate check, run when the file hash
/// didn't match (e.g. the file was re-saved). Warning only — does not block.
/// </summary>
public class RowDuplicateCheckResultDto
{
    public List<ImportCheckRowDto> DuplicateRows { get; set; } = [];
}

/// <summary>
/// One PO Number that already exists as a real, saved Sales Order — returned
/// by the early PO-only import check.
/// </summary>
public class PoImportMatchDto
{
    public string PoNum { get; set; } = string.Empty;
    public string CustKey { get; set; } = string.Empty;
    public string CusName { get; set; } = string.Empty;
    public DateOnly OrderDate { get; set; }
    public string EncodedBy { get; set; } = string.Empty;
}

/// <summary>
/// Result of the early, PO-Number-only import check run right after the
/// wizard's Next button, before column mapping/customer resolution. This IS
/// a hard block, like <see cref="FileImportCheckResultDto"/> — it exists to
/// catch a batch that's already been saved even when the file's bytes (and
/// thus its hash) changed, e.g. a renamed worksheet tab. Matched purely on
/// PO Number since the "Import by Customer Name" flow doesn't know CustKey
/// yet at this point in the wizard.
/// </summary>
public class PoImportCheckResultDto
{
    public List<PoImportMatchDto> Matches { get; set; } = [];
}
