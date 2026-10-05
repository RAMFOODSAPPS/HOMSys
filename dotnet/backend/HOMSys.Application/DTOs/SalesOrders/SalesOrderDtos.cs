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

    /// <summary>Offshore Encoder only — target Site.Code (see SalesOrder.ForBranch).
    /// Null/empty for a normal order.</summary>
    public string? ForBranch { get; set; }

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

    /// <summary>BMS-owned — VSDET.REC_CS/REC_PC/REC_AMT/REC_STAT, pushed by
    /// a1146F's delivery-status maintenance screen alongside the header's
    /// Delivered/Status. Can differ from QtyCs/QtyPc when this line was
    /// partially rejected on delivery. Null until the invoice is tagged.</summary>
    public int? ReceivedQtyCs { get; set; }
    public int? ReceivedQtyPc { get; set; }
    public decimal? ReceivedAmt { get; set; }
    public string? ReceivedStatus { get; set; }

    /// <summary>BMS-owned — total returned via posted RFCs for this SKU (raw CS/PC sums
    /// across RFC lines). Null when the order has no RFC for it.</summary>
    public int? RfcQtyCs { get; set; }
    public int? RfcQtyPc { get; set; }
}

/// <summary>A posted BMS RFC against the order's invoice (read-only).</summary>
public class SalesOrderRfcDto
{
    public int RfcNo { get; set; }
    public int InvNo { get; set; }
    /// <summary>The approved RSR (c1110k2) this RFC was moved from.</summary>
    public int? RsrNo { get; set; }
    public DateOnly? RfcDate { get; set; }
    public DateOnly? PostedDate { get; set; }
    public string? UserName { get; set; }
    public string? Remarks { get; set; }
    public string? Remarks2 { get; set; }
    public List<SalesOrderRfcLineDto> Lines { get; set; } = [];
}

public class SalesOrderRfcLineDto
{
    public string CProdNo { get; set; } = string.Empty;
    public int QtyCs { get; set; }
    public int QtyPc { get; set; }
    public decimal SpAmt { get; set; }
    public decimal Tax { get; set; }
    public string? RetCode { get; set; }
    public string? RsNo { get; set; }
    public string? Remarks { get; set; }
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

    /// <summary>BMS-owned — set when BMS cancels the invoice (a1174.scx only — an RFC is a return, see Rfcs). InvNo/InvDate/InvAmt are kept.</summary>
    public int? CancelledInvNo { get; set; }
    public DateOnly? InvCancelDate { get; set; }
    public string? InvCancelRemarks { get; set; }
    public string? InvCancelledBy { get; set; }

    /// <summary>True once pushed to BMS (SoNo assigned) — cleared again once BMS
    /// deallocates the order. Edits are refused while true.</summary>
    public bool IsLocked { get; set; }

    /// <summary>True while a post-deallocation HOMSys edit is waiting to be
    /// pushed into BMS's live oowkhdr/oowkdet record.</summary>
    public bool NeedsResync { get; set; }

    /// <summary>True if BMS could not find the live record to apply the last
    /// resync onto — surfaced instead of leaving NeedsResync stuck true.</summary>
    public bool ResyncFailed { get; set; }

    /// <summary>Entered / Downloaded / Processed / Deallocated / Invoiced / Invoiced with RFC / Full RFC / Cancelled. Display-only.</summary>
    public string WorkflowStatus { get; set; } = "Entered";

    /// <summary>BMS-owned — Date Cust. Rec. from VSHDR.DELIVERED, pushed by a1146F's
    /// delivery-status maintenance screen. Null until the invoice is tagged.</summary>
    public DateOnly? Delivered { get; set; }

    /// <summary>BMS-owned — VSHDR.STATUS ("1" Delivered / "2" Rejected / "3" Undelivered),
    /// pushed alongside Delivered. Distinct from WorkflowStatus. HOMSys-derived, never
    /// stored: "C" = invoice cancelled in BMS, "R" = Returned (Full RFC).</summary>
    public string? DeliveryStatus { get; set; }

    /// <summary>BMS-owned — VSHDR delivery-run fields (Search VS by Invoice#
    /// screen), pushed alongside Delivered/DeliveryStatus. See BridgeDeliveryDto
    /// for the VSHDR.EDA vs. EDA2 mapping note.</summary>
    public int? VsNo { get; set; }
    public DateOnly? VsDate { get; set; }
    public string? PlateNo { get; set; }
    public string? Trucker { get; set; }
    public string? Driver { get; set; }
    public string? Vessel { get; set; }
    public string? Voyage { get; set; }
    public string? BlNo { get; set; }
    public DateOnly? Edd { get; set; }
    public DateOnly? Eda2 { get; set; }

    /// <summary>
    /// Encode-time estimate, computed from current price quotes — NOT the
    /// BMS-owned invoiced amount. Sum of PricePerCase * QtyCs * 1.12 across
    /// lines, same formula the encode grid's running total uses.
    /// </summary>
    public decimal EstAmt { get; set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Owning branch now (flips to ForBranch on an offshore hand-off),
    /// the encoder's branch, and the offshore target (null for a normal order).</summary>
    public string? Branch { get; set; }
    public string? OriginBranch { get; set; }
    public string? ForBranch { get; set; }
    public DateTime? OffshoreUploadedAt { get; set; }
    public DateTime? OffshoreReceivedAt { get; set; }
    public string? OffshoreError { get; set; }

    /// <summary>Set once a branch bridge PC has reserved the order for download (see SalesOrder.BridgeClaimedAt).</summary>
    public DateTime? BridgeClaimedAt { get; set; }
    public string? BridgeClaimedBy { get; set; }

    public List<SalesOrderLineDto> Lines { get; set; } = [];

    /// <summary>Posted BMS RFCs against this order's invoice.</summary>
    public List<SalesOrderRfcDto> Rfcs { get; set; } = [];

    /// <summary>Number of posted RFCs, and their total returned value, VAT-inclusive (Σ imtr_det SPAMT + TAX; null when none).</summary>
    public int RfcCount { get; set; }
    public decimal? RfcAmt { get; set; }
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

    /// <summary>Offshore order target (null = normal order). The bridge writes
    /// oowkhdr.OFFSHORE = .T. for these, as a11102 does for every order encoded
    /// at HON/LKA-HO — keeps it out of the origin's Print Picklist.</summary>
    public string? ForBranch { get; set; }

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

/// <summary>"For Branch" picker option — a Site that accepts offshore orders.</summary>
public class OffshoreBranchOptionDto
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// Body of POST bridge/by-sono/{soNo}/offshore-upload — the origin BMS rows of
/// an offshore order that met the invoice.optn_init5 (Print Picklist)
/// condition. Raw DBF field -> text values, replayed as-is at ForBranch.
/// </summary>
public class BridgeOffshoreUploadDto
{
    public Dictionary<string, string> Header { get; set; } = [];
    public List<Dictionary<string, string>> Lines { get; set; } = [];
    public List<Dictionary<string, string>> Discounts { get; set; } = [];
}

/// <summary>One order of GET bridge/offshore-inbound — uploaded by its origin,
/// not yet appended into this (ForBranch) BMS.</summary>
public class BridgeOffshoreInboundDto
{
    public int SoId { get; set; }
    public int SoNo { get; set; }
    public string? OriginBranch { get; set; }
    public Dictionary<string, string> Header { get; set; } = [];
    public List<Dictionary<string, string>> Lines { get; set; } = [];
    public List<Dictionary<string, string>> Discounts { get; set; } = [];
}

/// <summary>Body of POST bridge/by-sono/{soNo}/offshore-received.</summary>
public class BridgeOffshoreReceivedDto
{
    /// <summary>"appended", or "duplicate" when the DOCNO already exists in the target BMS.</summary>
    public string Result { get; set; } = string.Empty;
    public string? Message { get; set; }
}

/// <summary>Body of POST /api/salesorders/bridge/{soId}/claim.</summary>
public class BridgeClaimDto
{
    /// <summary>The claiming bridge PC (its computer name).</summary>
    public string ClaimedBy { get; set; } = string.Empty;
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

/// <summary>One row of GET /api/salesorders/bridge/reconcile-candidates — HOMSys's
/// current view of an order the bridge's reconciliation sweep compares against
/// BMS (oowkhdr/oocuhdr, VSHDR, DOCCANCEL, IMTR_HDR RFCs).</summary>
public class BridgeReconcileCandidateDto
{
    public int SoNo { get; set; }
    public int? InvNo { get; set; }
    public int? CancelledInvNo { get; set; }
    public string WorkflowStatus { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public bool NeedsResync { get; set; }
    public DateOnly? Delivered { get; set; }
    public string? DeliveryStatus { get; set; }

    /// <summary>RFC numbers HOMSys already holds for this order — the sweep re-posts
    /// the invoice's RFC snapshot when BMS's posted set differs.</summary>
    public List<int> RfcNos { get; set; } = [];
}

/// <summary>Body of POST /api/salesorders/bridge/bms-date — the branch's sysparam.transdate.</summary>
public class BridgeBmsDateDto
{
    public DateOnly TransDate { get; set; }
}

/// <summary>Body of POST /api/salesorders/bridge/invoice-rfcs — posted BMS RFCs
/// grouped per invoice (a batch, so one scan = one request).</summary>
public class BridgeRfcSyncDto
{
    public List<BridgeRfcInvoiceDto> Invoices { get; set; } = [];
}

/// <summary>Posted RFCs for one invoice that the bridge hasn't uploaded before — merged
/// into the order's RFCs, never replacing them. SoNo is 0/null when unresolved.</summary>
public class BridgeRfcInvoiceDto
{
    public int? SoNo { get; set; }
    public int InvNo { get; set; }
    public List<BridgeRfcDto> Rfcs { get; set; } = [];
}

public class BridgeRfcDto
{
    public int RfcNo { get; set; }
    public int? RsrNo { get; set; }
    public DateOnly? RfcDate { get; set; }
    public DateOnly? PostedDate { get; set; }
    public string? UserName { get; set; }
    public string? Remarks { get; set; }
    public string? Remarks2 { get; set; }
    public List<BridgeRfcLineDto> Lines { get; set; } = [];
}

public class BridgeRfcLineDto
{
    public string CProdNo { get; set; } = string.Empty;
    public int QtyCs { get; set; }
    public int QtyPc { get; set; }
    public int Pieces { get; set; }
    public decimal SpAmt { get; set; }
    public decimal Amt { get; set; }
    public decimal Tax { get; set; }
    public decimal DiscAmt1 { get; set; }
    public decimal DiscAmt2 { get; set; }
    public string? RetCode { get; set; }
    public string? RsNo { get; set; }
    public string? Remarks { get; set; }
}

/// <summary>GET /api/salesorders/bridge-status — per-branch bridge heartbeat for the UI.</summary>
public class BridgeStatusDto
{
    public string Branch { get; set; } = string.Empty;
    public DateTime LastSyncedUtc { get; set; }
    public bool Stale { get; set; }
}

/// <summary>Body of POST /api/salesorders/bridge/invoice-cancel. Read by the
/// bridge off DOCCANCEL.DBF (a1174.scx cancellation) or IMTR_HDR (c1110bb.scx
/// RFC-from-invoice). SoNo is 0/null when the form couldn't resolve it — the
/// order is then matched on InvNo instead.</summary>
public class BridgeInvoiceCancelDto
{
    public int? SoNo { get; set; }
    public int InvNo { get; set; }
    public DateOnly? CancelDate { get; set; }
    public string? Remarks { get; set; }
    public string? CancelledBy { get; set; }

    /// <summary>Invoice date/amount as BMS recorded them at cancel time (DOCCANCEL
    /// DOCDATE/AMOUNT, or the RFC's REFDATE1) — only used to fill details HOMSys
    /// never received via the invoice sync; existing values are never overwritten.</summary>
    public DateOnly? InvDate { get; set; }
    public decimal? InvAmt { get; set; }
}

/// <summary>Body of POST /api/salesorders/bridge/by-sono/{soNo}/delivery.
/// Mirrors VSHDR's own DELIVERED/STATUS fields plus every matching VSDET line,
/// pushed by a1146F's delivery-status maintenance screen.</summary>
public class BridgeDeliveryDto
{
    public DateOnly? Delivered { get; set; }
    public string? Status { get; set; }

    /// <summary>VSHDR.DOCNO for this row — the invoice number, cross-checked
    /// against the matched SalesOrder's own InvNo when both are known, as a
    /// belt-and-suspenders check on top of the SoNo+branch match.</summary>
    public int? InvNo { get; set; }
    public List<BridgeDeliveryLineDto> Lines { get; set; } = [];

    // ── VSHDR delivery-run fields (Search VS by Invoice# screen), pushed
    // alongside Delivered/Status/Lines. Maps onto SalesOrder's existing
    // oowkhdr-mirrored columns of the same name (Vessel/Voyage/Edd/BlNo are
    // shared with oowkhdr's own identical field group; Eda2 is VSHDR.EDA --
    // VSHDR's own EDA is grouped with VESSEL/VOYAGE/BLNO/EDD, same as
    // oowkhdr's Eda2, not VSHDR's separate top-level EDA/ETA pair). ────────
    public int? VsNo { get; set; }
    public DateOnly? VsDate { get; set; }
    public string? PlateNo { get; set; }
    public string? Trucker { get; set; }
    public string? Driver { get; set; }
    public string? Vessel { get; set; }
    public string? Voyage { get; set; }
    public string? BlNo { get; set; }
    public DateOnly? Edd { get; set; }
    public DateOnly? Eda2 { get; set; }
}

/// <summary>One VSDET row for the invoice being tagged — REC_CS/REC_PC/REC_AMT
/// can differ from the line's original QtyCs/QtyPc/NetAmt when partially
/// rejected on delivery.</summary>
public class BridgeDeliveryLineDto
{
    public string CProdNo { get; set; } = string.Empty;
    public int? ReceivedQtyCs { get; set; }
    public int? ReceivedQtyPc { get; set; }
    public decimal? ReceivedAmt { get; set; }
    public string? ReceivedStatus { get; set; }
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
