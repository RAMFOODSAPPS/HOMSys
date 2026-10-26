using HOMSys.Application.DTOs.Analytics;

namespace HOMSys.Infrastructure.Analytics;

public enum FieldKind { Dim, Date, Measure }

/// <summary>
/// One curated field. Sql is a trusted registry fragment, never client text.
/// Agg "custom" = Sql already contains its aggregate. Caption = display
/// expression emitted as MAX(caption); filters/cross-filters use the key.
/// </summary>
public sealed record Fld(
    string Key, string Label, FieldKind Kind, string Sql,
    string Type = "string",          // string | int | decimal | date | bool
    string? Agg = null,
    string? Format = null,           // php | int | dec | pct | days | daysSev (days + severity badge)
    string? Caption = null,
    string[]? Joins = null,
    bool Utc = false,                // UTC datetime -> bucketed/filtered in Asia/Manila
    int MinYear = 2000,              // earlier dates bucket to NULL "(invalid date)"
    string? Sort = null,             // e.g. lifecycle order for WorkflowStatus
    string? DrillTo = null,
    bool Additive = true,
    string? Desc = null);

public sealed record Join(string Alias, string Sql, params string[] DependsOn);

/// <summary>
/// One dataset. Scope is the RLS template (@scSite / @scFolder / @scWhN /
/// @scWh) ANDed into every query for a branch-scoped viewer; null only when
/// National. Heavy = large table, the builder shows a Run button.
/// </summary>
public sealed record Ds(
    string Key, string Label, string Grain, string From, Fld[] Fields,
    string? Scope,
    bool National = false,
    string? Permission = null,
    string? DefaultDate = null,
    Join[]? Joins = null,
    string? Where = null,
    string? AsOfSql = null,
    string[]? Detail = null,
    SpecFilter[]? Defaults = null,
    bool Heavy = false);

/// <summary>
/// The Data Analytics semantic layer: every dataset the report builder can
/// query, over existing HOMSysDb tables only. Adding a dataset for a future
/// SQL table = one more Ds entry here; the UI is driven by /api/analytics/meta.
///
/// Rules that neutralise the known data traps by construction:
///  - Curated fields only: BMS-owned always-NULL columns don't exist here.
///  - LEFT JOIN only on unique keys (Products.CProdNo, ProductCategories PK,
///    OosSyncLines(SoId,CProdNo), Sites.Id). Every soft join is OUTER APPLY
///    TOP 1 with a deterministic ORDER BY — Customers.CustKey is not unique
///    (SO->Customer would otherwise double 36% of orders).
///  - Conformed field keys (branch, folder, custKey, cProdNo, category, zone,
///    orderDate, workflowStatus) so cross-filtering is plain key equality.
/// </summary>
public static class AnalyticsCatalog
{
    // ── field helpers ────────────────────────────────────────────────────────
    private static Fld Dim(string key, string label, string sql, string type = "string", string? caption = null,
        string[]? joins = null, string? drill = null, string? sort = null, string? desc = null) =>
        new(key, label, FieldKind.Dim, sql, type, Caption: caption, Joins: joins, DrillTo: drill, Sort: sort, Desc: desc);

    private static Fld Flag(string key, string label, string condition, string[]? joins = null, string? desc = null) =>
        new(key, label, FieldKind.Dim, $"CASE WHEN {condition} THEN 1 ELSE 0 END", "bool", Joins: joins, Desc: desc);

    private static Fld Dt(string key, string label, string sql, bool utc = false, string[]? joins = null, string? desc = null) =>
        new(key, label, FieldKind.Date, sql, "date", Utc: utc, Joins: joins, Desc: desc);

    private static Fld Msr(string key, string label, string sql, string agg = "sum", string? format = null,
        string type = "decimal", bool additive = true, string[]? joins = null, string? desc = null) =>
        new(key, label, FieldKind.Measure, sql, type, agg, format, Joins: joins, Additive: additive, Desc: desc);

    /// <summary>A pre-aggregated measure (COUNT(*), ratios...).</summary>
    private static Fld Calc(string key, string label, string sql, string format, string type = "decimal",
        bool additive = true, string[]? joins = null, string? desc = null) =>
        new(key, label, FieldKind.Measure, sql, type, "custom", format, Joins: joins, Additive: additive, Desc: desc);

    private static readonly string[] Cu = ["cu"];
    private static readonly string[] CuZm = ["cu", "zm"];

    // ── shared joins ─────────────────────────────────────────────────────────

    /// <summary>Canonical customer row per CustKey — same rule as CustomerRepository.GetByCustKeyAsync plus an Id tiebreak. PFolder mirrors CustomerBranchResolver.CustomerBranchToPricingFolder.</summary>
    private const string CustomerApply = """
        OUTER APPLY (SELECT TOP 1 c.Branch AS Folder, c.CZone, c.WhseNo,
                            CASE WHEN c.Branch IN ('DAG','ISA','LEG','LUC','mxs','NAG') THEN 'hon' ELSE c.Branch END AS PFolder
                     FROM Customers c WHERE c.CustKey = so.CustKey
                     ORDER BY c.ImportedAt DESC, c.Id DESC) cu
        """;

    private static readonly Join[] SalesJoins =
    [
        // cu first: sw / zm read cu's columns (AnalyticsSqlBuilder also emits dependencies first).
        new("cu", CustomerApply),
        // Credited site: the Site whose Cuwhsenos holds the customer's WhseNo (a shared whse, e.g. 45,
        // prefers the processing branch); no match -> the processing branch itself.
        new("sw", """
            OUTER APPLY (SELECT TOP 1 s.Code FROM Sites s
                         WHERE ',' + REPLACE(s.Cuwhsenos, ' ', '') + ',' LIKE '%,' + CAST(cu.WhseNo AS varchar(10)) + ',%'
                         ORDER BY CASE WHEN s.Code = COALESCE(so.ForBranch, so.Branch) THEN 0 ELSE 1 END, s.Id) sw
            """, "cu"),
        new("st", "OUTER APPLY (SELECT TOP 1 s.Name FROM Sites s WHERE s.Code = COALESCE(sw.Code, so.ForBranch, so.Branch) ORDER BY s.Id) st", "sw"),
        // Posted BMS RFCs against the order's invoice, pre-aggregated to one row per order.
        new("rf", "OUTER APPLY (SELECT SUM(rl.SpAmt + rl.Tax) AS Amt FROM SalesOrderRfcs r JOIN SalesOrderRfcLines rl ON rl.RfcId = r.Id WHERE r.SoId = so.SoId) rf"),
        new("zm", "OUTER APPLY (SELECT TOP 1 z.CDesc FROM ZoneMasts z WHERE z.Branch = cu.PFolder AND z.CZone = cu.CZone ORDER BY z.Id) zm", "cu"),
    ];

    // OriginBranch too (offshore orders stay visible to the encoder), and the branch credited via the customer's warehouse:
    // the credited branch must see its own sales.
    private const string SalesScope = """
        (so.Branch = @scSite OR so.OriginBranch = @scSite OR EXISTS (SELECT 1 FROM Customers c2 JOIN Sites s2 ON s2.Code = @scSite
            WHERE c2.CustKey = so.CustKey AND ',' + REPLACE(s2.Cuwhsenos, ' ', '') + ',' LIKE '%,' + CAST(c2.WhseNo AS varchar(10)) + ',%'))
        """;

    private const string SalesAsOf =
        "SELECT MAX(v) FROM (VALUES ((SELECT MAX(CreatedAt) FROM SalesOrders)), ((SELECT MAX(UpdatedAt) FROM SalesOrders)), ((SELECT MAX(SyncedAt) FROM OosSyncLines))) t(v)";

    private const string MastersAsOf = "SELECT MAX(LastSyncedUtc) FROM SyncLogs WHERE Section = 'PricingMasters'";

    /// <summary>Lifecycle order Entered -> Downloaded -> Processed -> Deallocated -> Invoiced -> Invoiced with RFC -> Full RFC; Cancelled last.</summary>
    private const string StatusSort =
        "CASE so.WorkflowStatus WHEN 'Entered' THEN 1 WHEN 'Downloaded' THEN 2 WHEN 'Processed' THEN 3 WHEN 'Deallocated' THEN 4 WHEN 'Transferring' THEN 5 WHEN 'Transferred' THEN 6 WHEN 'Invoiced' THEN 7 WHEN 'Invoiced with RFC' THEN 8 WHEN 'Full RFC' THEN 9 WHEN 'Cancelled' THEN 10 ELSE 11 END";

    /// <summary>Header dimensions/dates, shared by sales_orders and sales_lines (both alias SalesOrders as so).</summary>
    private static readonly Fld[] SalesHeaderFields =
    [
        Dim("soId", "SO ID", "so.SoId", "int"),
        Dim("soNo", "SO #", "so.SoNo", "int", desc: "BMS sales order number, assigned by the bridge. NULL = not yet pushed to BMS."),
        Dim("invNo", "Invoice #", "so.InvNo", "int"),
        Dim("branch", "Branch", "COALESCE(sw.Code, so.ForBranch, so.Branch)", caption: "st.Name", joins: ["st"], drill: "custKey",
            desc: "The branch credited with the sale (Sites.Code): the Site whose customer warehouses (Cuwhsenos) include the customer's WhseNo, so an offshore order encoded by HON/LKA-HO and processed by another branch is credited to the customer's branch. No matching Site = the processing branch (For Branch, else the encoding user's branch). Blank = encoded by an HO/admin user."),
        Dim("originBranch", "Encoding Branch", "so.OriginBranch",
            desc: "The encoding user's branch (Sites.Code). Never changes on an offshore hand-off."),
        Dim("forBranch", "For Branch", "so.ForBranch",
            desc: "Offshore orders only: the branch the order was encoded for. Blank = normal order."),
        Dim("custKey", "Customer", "so.CustKey", caption: "so.CusName", desc: "Customer as of encode time."),
        Dim("csMan", "Salesman", "so.CsMan"),
        Dim("termDays", "Term (days)", "so.TermDays", "int"),
        Dim("workflowStatus", "Workflow Status", "so.WorkflowStatus", sort: StatusSort, drill: "branch",
            desc: "Entered -> Downloaded -> Processed -> Deallocated -> (offshore: Transferring -> Transferred) -> Invoiced -> Invoiced with RFC / Full RFC; Cancelled = invoice voided in BMS (a1174)."),
        Flag("invoiced", "Invoiced?", $"so.InvNo IS NOT NULL AND {Live}"),
        Flag("delivered", "Delivered?", $"so.Delivered IS NOT NULL AND {Live}"),
        Flag("invCancelled", "Invoice Cancelled?", "so.WorkflowStatus = 'Cancelled'"),
        Flag("hasRfc", "Has RFC (Return)?", "EXISTS (SELECT 1 FROM SalesOrderRfcs r WHERE r.SoId = so.SoId)"),
        Flag("pendingSync", "Pending Bridge Sync?", "so.NeedsResync = 1 OR so.SoNo IS NULL"),
        Flag("resyncFailed", "Resync Failed?", "so.ResyncFailed = 1"),
        Flag("cancelled", "Has Cancel Date?", "so.CancelDate IS NOT NULL"),
        Dim("source", "Entry Source", "CASE WHEN so.SourceFileHash IS NULL THEN 'Manual' ELSE 'Import' END"),
        Dim("encoder", "Encoded By", "so.CreatedBy", desc: "Login that encoded the order (often a shared branch login)."),
        Dim("delArea", "Delivery Area", "so.DelArea"),
        Dim("whseNo", "Customer WhseNo", "so.WhseNo", "int"),
        Dim("zone", "Pricing Zone (current)", "cu.CZone", caption: "zm.CDesc", joins: CuZm, drill: "custKey",
            desc: "Customer's CURRENT zone — SalesOrders does not store the zone at order time."),
        Dim("folder", "Pricing Folder (current)", "cu.PFolder", joins: Cu),
        Dim("trucker", "Trucker", "so.Trucker"),
        Dim("driver", "Driver", "so.Driver"),
        Dim("vsNo", "VS / Trip #", "so.VsNo", "int"),
        Dim("poNum", "PO #", "so.PoNum"),
        Dim("customerName", "Customer Name", "so.CusName", desc: "Customer name as encoded (use 'contains' to search)."),
        Dim("cKey", "Customer Code (CKey)", "so.CKey"),
        Dim("cCode", "Customer Class", "so.CCode", "int"),
        Dim("term", "Term Code", "so.Term", "int"),
        Dim("salesmanNo", "Salesman No", "so.Salesman", "int"),
        Dim("shipTo", "Ship To", "so.ShipToLn1"),
        Dim("invRemarks", "Invoice Remarks", "so.InvRem"),
        Dim("remarks", "Remarks", "so.Remarks"),
        Flag("offshore", "Offshore?", "so.Offshore = 1"),
        Flag("locked", "Locked (Processed)?", "so.IsLocked = 1"),
        Dim("sourceFile", "Import File", "so.SourceFileName"),
        Dt("cancelDate", "Cancel Date", "so.CancelDate"),
        Dt("vsDate", "VS Date", "so.VsDate"),
        Dt("orderDate", "Order Date", "so.OrderDate"),
        Dt("invDate", "Invoice Date", "so.InvDate"),
        Dt("deliveredDate", "Delivered Date", "so.Delivered"),
        Dt("poDate", "PO Date", "so.PoDate"),
        Dt("createdAt", "Encoded At", "so.CreatedAt", utc: true),
    ];

    // ── sales_lines helpers ──────────────────────────────────────────────────
    private const string AllocPcs = "(ISNULL(o.AllocatedQtyCs,0)*l.Pieces + ISNULL(o.AllocatedQtyPc,0))";
    private const string OosPcs =
        $"CASE WHEN sy.Synced = 1 THEN CASE WHEN l.OrderedPcs > {AllocPcs} THEN l.OrderedPcs - {AllocPcs} ELSE 0 END END";
    private const string ListPriceVat = "(px.Base + px.Z1 + px.Z2) * (1 + COALESCE(NULLIF(l.TaxRate,0), 0.12))";

    /// <summary>Invoice cancelled in BMS (a1174 Invoice Cancellation — void). Its invoice details
    /// are kept on the order for the record, so every invoiced/delivered measure must exclude it
    /// explicitly. RFC'd invoices ("Invoiced with RFC" / "Full RFC") stay Live: they're real
    /// invoices with returns — see rfcAmt / netInvAmt.</summary>
    private const string Live = "so.WorkflowStatus <> 'Cancelled'";

    /// <summary>INV CS in pieces — the invoice-time BMS allocation snapshot (same source as the SO screen's INV CS column). A line missing from the snapshot = 0 invoiced.</summary>
    private const string InvPcs = $"CASE WHEN so.InvNo IS NOT NULL AND {Live} AND sy.Synced = 1 THEN {AllocPcs} END";

    /// <summary>Delivered pieces: BMS-reconciled received qty; until BMS reconciles, a Delivered order is assumed to have received its INV CS.</summary>
    private const string DelivPcs = $"CASE WHEN {Live} THEN COALESCE(l.ReceivedPcs, CASE WHEN so.Delivered IS NOT NULL THEN {InvPcs} END) END";
    private static readonly string[] Inv = ["o", "sy"];

    /// <summary>
    /// Order-date list price — SQL port of PriceCalculationService.GetQuoteAsync /
    /// GetBasePriceAsync + PricingRepository zone lookups, with RecNo tiebreaks.
    /// Uses the customer's CURRENT zone (SalesOrders stores none).
    /// </summary>
    private const string PriceApply = """
        OUTER APPLY (SELECT
            COALESCE(CASE WHEN p.PriceFrom <= so.OrderDate THEN p.NewPrice END,
                     (SELECT TOP 1 h.NpAfVat / 1.12 FROM PriceHistories h
                      WHERE h.ProdNo = p.ProdNo AND h.Effective IS NOT NULL AND h.Effective <= so.OrderDate
                      ORDER BY h.Effective DESC, h.RecNo DESC)) AS Base,
            ISNULL((SELECT TOP 1 z.AddOn FROM ZoneAddOns z
                    WHERE z.Branch = cu.PFolder AND z.CProdNo = l.CProdNo AND z.CZone = cu.CZone
                      AND (z.EffDate IS NULL OR z.EffDate <= so.OrderDate)
                    ORDER BY z.EffDate DESC, z.RecNo DESC), 0) AS Z1,
            ISNULL((SELECT TOP 1 z2.AddOn FROM Zone2AddOns z2
                    WHERE z2.Branch = cu.PFolder AND z2.CProdNo = l.CProdNo AND z2.CustKey IN (so.CustKey, cu.CZone)
                      AND (z2.EffDate IS NULL OR z2.EffDate <= so.OrderDate)
                    ORDER BY z2.EffDate DESC, z2.RecNo DESC), 0) AS Z2) px
        """;

    private static readonly string[] Px = ["px"];

    // ── folder datasets (customers / zone add-ons) ───────────────────────────

    /// <summary>Conformed Sites.Code for a customer folder row: independent folder -> CODE-B; hon -> the hon-priced site whose Cuwhsenos holds the WhseNo.</summary>
    private const string SiteByWhseApply = """
        OUTER APPLY (SELECT CASE WHEN COUNT(*) > 1 THEN '(multiple)' ELSE MIN(s.Code) END AS SiteCode
                     FROM Sites s
                     WHERE s.PricesOffHon = 1 AND s.IsActive = 1 AND s.Cuwhsenos <> ''
                       AND ',' + REPLACE(s.Cuwhsenos, ' ', '') + ',' LIKE '%,' + CAST(c.WhseNo AS varchar(10)) + ',%') sw
        """;

    /// <summary>Product display name always carries the pack: "RAM SALTED BLACK BEANS - SUP (48 x 100G)".</summary>
    private static string ProductName(string desc, string pieces, string pack) =>
        $"{desc} + CASE WHEN LTRIM(ISNULL({pack}, '')) <> '' THEN ' (' + CASE WHEN {pieces} > 0 THEN CAST({pieces} AS varchar(10)) + ' x ' ELSE '' END + REPLACE({pack}, ' ', '') + ')' ELSE '' END";

    private static readonly string MasterProductName = ProductName("p.ProdDesc", "p.Pieces", "p.PackSize");

    private static Fld ProductDim(string alias) =>
        Dim("cProdNo", "Product", $"{alias}.CProdNo", caption: $"ISNULL({MasterProductName}, '(not in product master)')", joins: ["p"]);

    /// <summary>Product-master attributes for datasets that reach Products as p (joined = p is an optional join).</summary>
    private static Fld[] ProductAttrs(bool joined)
    {
        string[]? j = joined ? ["p"] : null;
        return
        [
            Dim("prodNo", "Prod No", "p.ProdNo", "int", joins: j),
            Dim("productDesc", "Product Description", MasterProductName, joins: j, desc: "Description with pack size, e.g. RAM SALTED BLACK BEANS - SUP (48 x 100G)."),
            Dim("packSize", "Pack Size", "p.PackSize", joins: j),
            Dim("piecesPerCase", "Pieces per Case", "p.Pieces", "int", joins: j),
            Dim("um", "UM", "p.Um", joins: j),
            Dim("sBrand", "Sub-brand", "p.SBrand", joins: j),
            Dim("barcode", "Barcode", "p.Barcode", joins: j),
            Dim("caseBarcode", "Case Barcode", "p.CaseBarcode", joins: j),
        ];
    }

    private static readonly Fld CategoryDim = Dim("category", "Category", "p.Category", caption: "pc.GroupDesc + ' - ' + pc.SubCat", joins: ["pc"], drill: "cProdNo");
    private static readonly Fld CategoryGroupDim = Dim("categoryGroup", "Category Group", "pc.GroupNo", "int", caption: "pc.GroupDesc", joins: ["pc"], drill: "category");
    private static readonly Fld BrandDim = Dim("brand", "Brand", "p.Brand", joins: ["p"], drill: "cProdNo");

    private static Join ProductJoin(string alias) => new("p", $"LEFT JOIN Products p ON p.CProdNo = {alias}.CProdNo");
    private static readonly Join CategoryJoin = new("pc", "LEFT JOIN ProductCategories pc ON pc.CategoryCode = p.Category", "p");

    // ── collections (finance) ────────────────────────────────────────────────

    /// <summary>Finance only (verifier or manager), all branches — branch custodians work from the Collections pages.</summary>
    private const string FinancePermission = "collections-verify|collections-manage";

    /// <summary>A collection that counts: not voided. A bounced PDC still counts as a receipt but not as money collected.</summary>
    private const string ColLive = "c.VerificationStatus <> 'Voided'";
    private const string ColGood = "c.VerificationStatus <> 'Voided' AND ISNULL(c.PdcOutcome, '') <> 'Bounced'";

    private const string PdcStateSql = """
        CASE WHEN c.PaymentMode <> 'PostDatedCheck' THEN NULL WHEN c.PdcOutcome IS NOT NULL THEN c.PdcOutcome
             WHEN c.PaymentStatus = 'Deposited' THEN 'Deposited' WHEN c.CheckDate > @today THEN 'Immature' ELSE 'Matured' END
        """;

    /// <summary>Bounced PDC follow-up, same rules as CollectionService.DishonorState.</summary>
    private const string DishonorStateSql = """
        CASE WHEN ISNULL(c.PdcOutcome, '') <> 'Bounced' OR c.VerificationStatus = 'Voided' THEN NULL
             WHEN c.SettledByCollectionId IS NOT NULL THEN 'Settled' WHEN c.LegalEndorsedDate IS NOT NULL THEN 'For Legal Action'
             WHEN c.LodIssuedDate IS NULL THEN 'For LOD' WHEN c.LodDueDate IS NULL THEN 'LOD Issued'
             WHEN c.LodDueDate < @today THEN 'Overdue' ELSE 'Awaiting Settlement' END
        """;

    private const string CollectionsAsOf =
        "SELECT MAX(v) FROM (VALUES ((SELECT MAX(CreatedAt) FROM Collections)), ((SELECT MAX(UpdatedAt) FROM Collections)), ((SELECT MAX(VerifiedAt) FROM Collections)), ((SELECT MAX(MatchedAt) FROM DepositBatches))) t(v)";

    /// <summary>Cash applied + deductions per posted invoice from counting collections.</summary>
    private const string InvoicePaidApply = """
        OUTER APPLY (SELECT SUM(x.Amt) AS Paid FROM (
            SELECT ci.AmountApplied AS Amt FROM CollectionInvoices ci JOIN Collections c ON c.Id = ci.CollectionId
             WHERE ci.PostedInvoiceId = pi.Id AND c.VerificationStatus <> 'Voided' AND ISNULL(c.PdcOutcome, '') <> 'Bounced'
            UNION ALL
            SELECT d.Amount FROM CollectionDeductions d JOIN Collections c ON c.Id = d.CollectionId
             WHERE d.PostedInvoiceId = pi.Id AND c.VerificationStatus <> 'Voided' AND ISNULL(c.PdcOutcome, '') <> 'Bounced') x) pd
        """;

    private const string InvBalance = "(pi.Total + pi.RfcTotal - ISNULL(pd.Paid, 0))";
    private const string InvDaysPastDue = "DATEDIFF(day, DATEADD(day, pi.Term, pi.DocDate), @today)";
    private static readonly string[] Pd = ["pd"];

    private static readonly Ds[] CollectionDatasets =
    [
        new("collections", "Collections", "One row per encoded collection receipt (CR / PR / AR)",
            "Collections c JOIN Sites s ON s.Id = c.SiteId LEFT JOIN DepositBatches dp ON dp.Id = c.DepositBatchId",
            [
                Dim("branch", "Branch", "s.Code", caption: "s.Name", drill: "custKey"),
                Dim("custKey", "Customer", "c.CustKey", caption: "c.CustName"),
                Dim("receiptType", "Receipt Type", "c.ReceiptType", desc: "CR = Collection Receipt; PR / AR = Provisional / Acknowledgement Receipt (post-dated check)."),
                Dim("receiptNo", "Receipt #", "c.ReceiptNo"),
                Dim("paymentMode", "Payment Mode", "c.PaymentMode"),
                Dim("paymentStatus", "Payment Status", "c.PaymentStatus", desc: "Draft = incomplete; Collected = complete CR; Pending = complete PDC (check on hand, not yet collected); Deposited = bank match approved by the verifier."),
                Dim("verificationStatus", "Verification Status", "c.VerificationStatus",
                    sort: "CASE c.VerificationStatus WHEN 'Draft' THEN 1 WHEN 'Returned' THEN 2 WHEN 'ForVerification' THEN 3 WHEN 'Verified' THEN 4 ELSE 5 END"),
                Dim("pdcState", "PDC State", PdcStateSql,
                    sort: "CASE " + PdcStateSql + " WHEN 'Immature' THEN 1 WHEN 'Matured' THEN 2 WHEN 'Deposited' THEN 3 WHEN 'Succeeded' THEN 4 WHEN 'Bounced' THEN 5 ELSE 6 END",
                    desc: "Post-dated checks only: Immature -> Matured (check date) -> Deposited (bank matched) -> Succeeded / Bounced."),
                Dim("dishonorState", "Bounced PDC Follow-up", DishonorStateSql,
                    desc: "Bounced PDCs only: For LOD -> LOD Issued -> Awaiting Settlement / Overdue -> Settled (new CR) or For Legal Action."),
                Dim("depositAccount", "Deposit Account", "dp.Bank"),
                Dim("slipRefNo", "Deposit Slip #", "dp.SlipRefNo"),
                Dim("drawee", "Check Bank (drawee)", "c.Bank"),
                Dim("encoder", "Encoded By", "c.CreatedBy"),
                Dim("verifiedBy", "Verified By", "c.VerifiedBy"),
                Flag("bankMatched", "Bank Matched?", "dp.MatchConfirmedAt IS NOT NULL", desc: "Matched and approved by the verifier (a pending automatic match counts as no)."),
                Flag("onAccount", "Has On-account Amount?", "c.UnappliedAmount > 0"),
                Flag("isPdc", "Post-dated Check?", "c.PaymentMode = 'PostDatedCheck'"),
                Flag("voided", "Voided?", "c.VerificationStatus = 'Voided'"),
                Dt("receiptDate", "Receipt Date", "c.ReceiptDate"),
                Dt("depositDate", "Deposit Date", "dp.DepositDate"),
                Dt("checkDate", "Check Date", "c.CheckDate"),
                Dt("submittedAt", "Submitted At", "c.SubmittedAt", utc: true),
                Dt("verifiedAt", "Verified At", "c.VerifiedAt", utc: true),
                Dt("createdAt", "Encoded At", "c.CreatedAt", utc: true),
                Calc("collections", "Collections", $"SUM(CASE WHEN {ColLive} THEN 1 ELSE 0 END)", "int", "int", desc: "Receipts, voided excluded."),
                Msr("amount", "Amount Collected", $"CASE WHEN {ColGood} THEN c.Amount END", format: "php", desc: "Amount received; voided receipts and bounced PDCs excluded."),
                Msr("applied", "Applied to Invoices", $"CASE WHEN {ColGood} THEN c.Amount - c.UnappliedAmount END", format: "php"),
                Msr("unapplied", "On-account (Unapplied)", $"CASE WHEN {ColGood} THEN c.UnappliedAmount END", format: "php"),
                Msr("deductions", "Deductions (EWT, discounts…)", $"CASE WHEN {ColGood} THEN dd.Amt END", format: "php", joins: ["dd"]),
                Msr("depositedAmt", "Deposited (bank matched)", $"CASE WHEN {ColGood} AND c.PaymentStatus = 'Deposited' THEN c.Amount END", format: "php"),
                Msr("notDepositedAmt", "Not Yet on Bank Statement",
                    $"CASE WHEN {ColGood} AND c.PaymentStatus <> 'Deposited' AND (c.PaymentMode <> 'PostDatedCheck' OR c.CheckDate <= @today) THEN c.Amount END",
                    format: "php", desc: "Cash / checks (and matured PDCs) whose deposit isn't matched to a bank credit yet."),
                Msr("forVerificationAmt", "Waiting Verification", $"CASE WHEN c.VerificationStatus = 'ForVerification' THEN c.Amount END", format: "php"),
                Msr("verifiedAmt", "Verified Amount", $"CASE WHEN c.VerificationStatus = 'Verified' AND ISNULL(c.PdcOutcome, '') <> 'Bounced' THEN c.Amount END", format: "php"),
                Msr("immaturePdcAmt", "PDCs Not Yet Due", $"CASE WHEN {ColLive} AND c.PaymentMode = 'PostDatedCheck' AND c.CheckDate > @today AND c.PdcOutcome IS NULL THEN c.Amount END", format: "php"),
                Msr("bouncedAmt", "Bounced PDCs", "CASE WHEN c.VerificationStatus <> 'Voided' AND c.PdcOutcome = 'Bounced' THEN c.Amount END", format: "php"),
                Msr("waitingDays", "Days Waiting Verification",
                    "CASE WHEN c.VerificationStatus = 'ForVerification' THEN DATEDIFF(day, DATEADD(hour, 8, c.SubmittedAt), @today) END",
                    "max", "daysSev", "int", additive: false),
                Msr("verifyDays", "Verification Turnaround (days)",
                    "CASE WHEN c.VerifiedAt IS NOT NULL AND c.SubmittedAt IS NOT NULL THEN DATEDIFF(hour, c.SubmittedAt, c.VerifiedAt) / 24.0 END",
                    "avg", "dec", additive: false),
                Msr("depositLagDays", "Receipt to Deposit (days)",
                    $"CASE WHEN {ColLive} AND c.PaymentMode <> 'PostDatedCheck' AND dp.DepositDate >= c.ReceiptDate THEN DATEDIFF(day, c.ReceiptDate, dp.DepositDate) END",
                    "avg", "dec", additive: false),
                Msr("daysToMaturity", "Days to Maturity",
                    $"CASE WHEN {ColLive} AND c.PaymentMode = 'PostDatedCheck' AND c.PdcOutcome IS NULL THEN DATEDIFF(day, @today, c.CheckDate) END",
                    "min", "int", "int", additive: false),
            ],
            null, National: true, Permission: FinancePermission, DefaultDate: "receiptDate",
            Joins: [new("dd", "OUTER APPLY (SELECT SUM(d.Amount) AS Amt FROM CollectionDeductions d WHERE d.CollectionId = c.Id) dd")],
            AsOfSql: CollectionsAsOf,
            Detail: ["branch", "receiptType", "receiptNo", "receiptDate", "custKey", "paymentMode", "verificationStatus", "paymentStatus", "amount"]),

        new("posted_invoices", "Invoices & Open Balances", "One row per posted invoice synced into HOMSys (fetched by branches from the SL files) — not the full BMS AR",
            "PostedInvoices pi LEFT JOIN Sites s ON s.Id = pi.SiteId",
            [
                Dim("branch", "Branch", "s.Code", caption: "s.Name", drill: "custKey"),
                Dim("custKey", "Customer", "pi.CustKey", caption: "pi.CustName"),
                Dim("invNo", "Invoice #", "pi.DocNo", "int"),
                Dim("termDays", "Term (days)", "pi.Term", "int"),
                Dim("aging", "Aging",
                    $"CASE WHEN {InvBalance} <= 0 THEN 'Paid' WHEN {InvDaysPastDue} <= 0 THEN 'Current' WHEN {InvDaysPastDue} <= 30 THEN '1-30 days' WHEN {InvDaysPastDue} <= 60 THEN '31-60 days' WHEN {InvDaysPastDue} <= 90 THEN '61-90 days' ELSE 'Over 90 days' END",
                    joins: Pd,
                    sort: $"CASE WHEN {InvBalance} <= 0 THEN 6 WHEN {InvDaysPastDue} <= 0 THEN 1 WHEN {InvDaysPastDue} <= 30 THEN 2 WHEN {InvDaysPastDue} <= 60 THEN 3 WHEN {InvDaysPastDue} <= 90 THEN 4 ELSE 5 END",
                    desc: "Days past the due date (invoice date + term) of the open balance."),
                Flag("open", "Has Open Balance?", $"{InvBalance} > 0", Pd),
                Flag("overpaid", "Overpaid (RFC after payment)?", $"{InvBalance} < 0", Pd, desc: "An RFC posted after the collection pushed the balance below zero — credit for finance to resolve."),
                Flag("hasRfc", "Has RFC?", "pi.RfcTotal <> 0"),
                Dt("invDate", "Invoice Date", "pi.DocDate"),
                Dt("dueDate", "Due Date", "DATEADD(day, pi.Term, pi.DocDate)"),
                Calc("invoices", "Invoices", "COUNT(*)", "int", "int"),
                Msr("invTotal", "Invoice Amount", "pi.Total", format: "php"),
                Msr("rfcAmt", "RFC Amount", "-pi.RfcTotal", format: "php"),
                Msr("paid", "Collected + Deductions", "ISNULL(pd.Paid, 0)", format: "php", joins: Pd),
                Msr("balance", "Open Balance", $"CASE WHEN {InvBalance} > 0 THEN {InvBalance} END", format: "php", joins: Pd),
                Msr("overdueBalance", "Overdue Balance", $"CASE WHEN {InvBalance} > 0 AND {InvDaysPastDue} > 0 THEN {InvBalance} END", format: "php", joins: Pd),
                Msr("daysPastDue", "Days Past Due", $"CASE WHEN {InvBalance} > 0 AND {InvDaysPastDue} > 0 THEN {InvDaysPastDue} END",
                    "max", "daysSev", "int", additive: false, joins: Pd),
                Msr("bmsPaid", "Collected per BMS", "ISNULL(bp.Ar, 0)", format: "php", joins: ["bp"],
                    desc: "A/R credited to the invoice by BMS posted ORs (OH03/OD03 mirror, synced months only)."),
                Flag("bmsDiffers", "HOMSys ≠ BMS?", "ABS(ISNULL(pd.Paid, 0) - ISNULL(bp.Ar, 0)) >= 0.01", ["pd", "bp"],
                    desc: "Collected in HOMSys (cash + deductions) differs from what BMS has posted against the invoice."),
            ],
            null, National: true, Permission: FinancePermission, DefaultDate: "invDate",
            Joins:
            [
                new("pd", InvoicePaidApply),
                new("bp", """
                    OUTER APPLY (SELECT SUM(l.CrAmt - l.DrAmt) AS Ar FROM BmsPostedCollectionLines l
                                 JOIN BmsPostedCollections b ON b.Id = l.BmsPostedCollectionId
                                 WHERE l.RefNo = pi.DocNo AND l.RefId = 1 AND l.AcctNo = 10301 AND b.Removed = 0 AND b.Cancelled = 0) bp
                    """),
            ],
            AsOfSql: "SELECT MAX(SyncedAt) FROM PostedInvoices",
            Detail: ["branch", "invNo", "invDate", "custKey", "dueDate", "invTotal", "rfcAmt", "paid", "balance"]),

        new("bms_collections", "BMS Posted Collections", "One row per OR / RPDC posted in BMS (HO archives OH03/OD03 mirror, synced months)",
            "BmsPostedCollections b LEFT JOIN Sites s ON s.Id = b.SiteId",
            [
                Dim("branch", "Branch", "s.Code", caption: "s.Name", drill: "custKey", desc: "From the OR / invoice number prefix."),
                Dim("custKey", "Customer", "b.CustKey", caption: "ISNULL(cn.CusName, b.CustKey)", joins: ["cn"]),
                Dim("docNo", "OR #", "b.DocNo", "int"),
                Dim("postedBy", "Posted By (BMS user)", "b.UserName"),
                Dim("status", "BMS Status", "b.StatDesc"),
                Dim("depBank", "Deposit Account", "b.DepBank"),
                Dim("drawee", "Check Bank (drawee)", "b.DraweeBank"),
                Flag("bounced", "Bounced (BADCQF)?", "b.Bounced = 1"),
                Flag("hasEwt", "With 2307 / CEWT?", "b.Ewt <> 0"),
                Flag("inHomsys", "Encoded in HOMSys?", "b.CollectionId IS NOT NULL"),
                Flag("isCheck", "Check Payment?", "b.CheckNo IS NOT NULL"),
                Dt("docDate", "OR Date", "b.DocDate"),
                Dt("postedDate", "Posted Date", "b.Posted"),
                Dt("checkDate", "Check Date", "b.CheckDate"),
                Calc("receipts", "ORs", "SUM(CASE WHEN b.Cancelled = 0 THEN 1 ELSE 0 END)", "int", "int"),
                Calc("customers", "Customers", "COUNT(DISTINCT CASE WHEN b.Cancelled = 0 THEN b.CustKey END)", "int", "int", additive: false),
                Msr("received", "Collected (cash / check)", "CASE WHEN b.Cancelled = 0 AND b.Bounced = 0 THEN b.Received END", format: "php",
                    desc: "Money side of the OR (cash-in-bank debits); bounced checks excluded."),
                Msr("ewt", "CEWT (2307)", "CASE WHEN b.Cancelled = 0 THEN b.Ewt END", format: "php"),
                Msr("arApplied", "A/R Settled", "CASE WHEN b.Cancelled = 0 AND b.Bounced = 0 THEN b.ArApplied END", format: "php",
                    desc: "Collected + CEWT credited to invoices."),
                Msr("bouncedAmt", "Bounced Checks", "CASE WHEN b.Cancelled = 0 AND b.Bounced = 1 THEN b.Received END", format: "php"),
                Msr("postingLagDays", "OR to Posting (days)", "CASE WHEN b.Posted >= b.DocDate THEN DATEDIFF(day, b.DocDate, b.Posted) END",
                    "avg", "dec", additive: false),
            ],
            null, National: true, Permission: FinancePermission, DefaultDate: "docDate",
            Joins: [new("cn", "OUTER APPLY (SELECT TOP 1 c.CusName FROM Customers c WHERE c.CustKey = b.CustKey ORDER BY c.ImportedAt DESC, c.Id DESC) cn")],
            Where: "b.Removed = 0",
            AsOfSql: "SELECT MAX(SyncedAt) FROM BmsPostedCollections",
            Detail: ["docNo", "docDate", "branch", "custKey", "postedBy", "postedDate", "received", "ewt", "arApplied"]),

        new("bank_lines", "Bank Statement Lines", "One row per uploaded bank-statement transaction",
            "BankStatementLines bl JOIN BankAccounts ba ON ba.Id = bl.BankAccountId LEFT JOIN DepositBatches dp ON dp.Id = bl.MatchedDepositBatchId LEFT JOIN Sites s ON s.Id = dp.SiteId",
            [
                Dim("account", "Account", "ba.BankCode + ' ' + ba.AccountNo", caption: "ba.BankCode + ' ' + ba.AccountNo + ' ' + ba.AccountName"),
                Dim("bankCode", "Bank", "ba.BankCode", drill: "account"),
                Dim("branch", "Matched Branch", "s.Code", caption: "s.Name"),
                Dim("matchMethod", "Match Method", "ISNULL(bl.MatchMethod, 'Open')"),
                Dim("description", "Description", "bl.Description"),
                Dim("bankBranch", "Bank Branch", "bl.BankBranch"),
                Flag("isCredit", "Credit?", "bl.Credit > 0"),
                Flag("matched", "Matched to a Deposit?", "bl.MatchedDepositBatchId IS NOT NULL"),
                Dt("txnDate", "Transaction Date", "bl.TxnDate"),
                Calc("lines", "Lines", "COUNT(*)", "int", "int"),
                Msr("credit", "Credits", "bl.Credit", format: "php"),
                Msr("debit", "Debits", "bl.Debit", format: "php"),
                Msr("matchedCredit", "Matched Credits", "CASE WHEN bl.MatchedDepositBatchId IS NOT NULL THEN bl.Credit END", format: "php"),
                Msr("unmatchedCredit", "Unidentified Credits", "CASE WHEN bl.Credit > 0 AND bl.MatchedDepositBatchId IS NULL THEN bl.Credit END", format: "php",
                    desc: "Credits with no deposit slip matched — not encoded yet, or a non-collection credit (transfers, trade)."),
            ],
            null, National: true, Permission: FinancePermission, DefaultDate: "txnDate",
            AsOfSql: "SELECT MAX(UploadedAt) FROM BankStatementUploads",
            Detail: ["txnDate", "account", "description", "bankBranch", "credit", "debit", "matchMethod", "branch"]),
    ];

    private static readonly Ds[] Base =
    [
        // ── D1 Sales Orders ──────────────────────────────────────────────────
        new("sales_orders", "Sales Orders", "One row per HOMSys-encoded sales order",
            "SalesOrders so",
            [
                .. SalesHeaderFields,
                Calc("orders", "Orders", "COUNT(*)", "int", "int"),
                Msr("invAmt", "Invoiced Amount", $"CASE WHEN {Live} THEN so.InvAmt END", format: "php", desc: "Real BMS invoice amount, gross of returns (invoiced orders; cancelled invoices excluded)."),
                Msr("rfcAmt", "RFC Amount", $"CASE WHEN {Live} THEN rf.Amt END", format: "php", joins: ["rf"],
                    desc: "Returned value from posted BMS RFCs against the invoice, VAT-inclusive (imtr_det SPAMT + TAX) — comparable with Invoiced Amount."),
                Msr("netInvAmt", "Net Invoiced", $"CASE WHEN {Live} THEN so.InvAmt - ISNULL(rf.Amt, 0) END", format: "php", joins: ["rf"],
                    desc: "Invoiced Amount less RFC Amount."),
                Calc("invoicedOrders", "Invoiced Orders", $"SUM(CASE WHEN so.InvNo IS NOT NULL AND {Live} THEN 1 ELSE 0 END)", "int", "int"),
                Calc("deliveredOrders", "Delivered Orders", $"SUM(CASE WHEN so.Delivered IS NOT NULL AND {Live} THEN 1 ELSE 0 END)", "int", "int"),
                Msr("daysOutstanding", "Days Since Invoice (undelivered)",
                    $"CASE WHEN so.InvNo IS NOT NULL AND so.Delivered IS NULL AND {Live} THEN DATEDIFF(day, so.InvDate, @today) END",
                    "max", "daysSev", "int", additive: false, desc: "Days an invoiced order has waited for delivery (Manila today). 4+ days = serious, 8+ = critical."),
                Msr("orderToInvoiceDays", "Order to Invoice Days",
                    "CASE WHEN so.InvDate >= so.OrderDate THEN DATEDIFF(day, so.OrderDate, so.InvDate) END",
                    "avg", "dec", additive: false, desc: "Rows with an invoice date before the order date are ignored."),
                Msr("invoiceToDeliveryDays", "Invoice to Delivery Days",
                    "CASE WHEN so.Delivered >= so.InvDate THEN DATEDIFF(day, so.InvDate, so.Delivered) END",
                    "avg", "dec", additive: false),
                Msr("encodeMinutes", "Encode Minutes",
                    "CASE WHEN so.SoTymEnd >= so.SoTymStart AND DATEDIFF(minute, so.SoTymStart, so.SoTymEnd) < 480 THEN DATEDIFF(second, so.SoTymStart, so.SoTymEnd) / 60.0 END",
                    "avg", "dec", additive: false, desc: "Time spent encoding the order (sessions over 8 hours ignored)."),
                Msr("orAmt", "Cash O.R. Amount", "so.OrAmt", format: "php"),
            ],
            SalesScope, DefaultDate: "orderDate", Joins: SalesJoins, AsOfSql: SalesAsOf,
            Detail: ["soId", "soNo", "orderDate", "custKey", "branch", "workflowStatus", "invNo", "invDate", "deliveredDate", "invAmt"]),

        // ── D2 Sales Lines & Fulfilment ──────────────────────────────────────
        new("sales_lines", "Sales Lines & Fulfilment",
            "One row per (sales order, product) — duplicate lines are pre-aggregated",
            """
            (SELECT SoId, CProdNo, MAX(ProdDesc) AS ProdDesc, MAX(Pieces) AS Pieces, MAX(TaxRate) AS TaxRate,
                    MAX(ProdNo) AS ProdNo, MAX(PackSize) AS PackSize, MAX(Um) AS Um, MAX(CSupplier) AS CSupplier,
                    MAX(CAST(FreeGoods AS int)) AS FreeGoods, COUNT(*) AS LineCount,
                    SUM(QtyCs * Pieces + QtyPc) AS OrderedPcs,
                    SUM(CASE WHEN ReceivedQtyCs IS NULL AND ReceivedQtyPc IS NULL THEN NULL
                             ELSE ISNULL(ReceivedQtyCs, 0) * Pieces + ISNULL(ReceivedQtyPc, 0) END) AS ReceivedPcs,
                    SUM(ReceivedAmt) AS ReceivedAmt
               FROM SalesOrderLines GROUP BY SoId, CProdNo) l
            JOIN SalesOrders so ON so.SoId = l.SoId
            """,
            [
                .. SalesHeaderFields,
                Dim("cProdNo", "Product", "l.CProdNo", caption: ProductName("COALESCE(p.ProdDesc, l.ProdDesc)", "COALESCE(p.Pieces, l.Pieces)", "COALESCE(p.PackSize, l.PackSize)"), joins: ["p"]),
                Dim("prodNo", "Prod No", "l.ProdNo", "int"),
                Dim("productDesc", "Product Description", ProductName("l.ProdDesc", "l.Pieces", "l.PackSize"), desc: "Description with pack size as encoded on the line."),
                Dim("packSize", "Pack Size", "l.PackSize"),
                Dim("piecesPerCase", "Pieces per Case", "l.Pieces", "int"),
                Dim("um", "UM", "l.Um"),
                Dim("supplier", "Supplier", "l.CSupplier"),
                Dim("sBrand", "Sub-brand", "p.SBrand", joins: ["p"]),
                Dim("barcode", "Barcode", "p.Barcode", joins: ["p"]),
                CategoryDim, CategoryGroupDim, BrandDim,
                Flag("freeGoods", "Free Goods?", "l.FreeGoods = 1"),
                Flag("oosSynced", "OOS Synced?", "sy.Synced = 1", ["sy"],
                    "True once the bridge has snapshotted this order's BMS allocation."),
                Calc("orders", "Orders", "COUNT(DISTINCT so.SoId)", "int", "int", additive: false),
                Calc("lines", "Lines", "SUM(l.LineCount)", "int", "int"),
                Msr("orderedCases", "Ordered Cases", "l.OrderedPcs * 1.0 / NULLIF(l.Pieces, 0)", format: "dec",
                    desc: "Cases including loose pieces (pieces / pieces-per-case)."),
                Msr("orderedPcs", "Ordered Pieces", "l.OrderedPcs", format: "int", type: "int"),
                Msr("allocatedCases", "Allocated Cases (synced)",
                    $"CASE WHEN sy.Synced = 1 THEN {AllocPcs} * 1.0 / NULLIF(l.Pieces, 0) END", format: "dec", joins: ["o", "sy"],
                    desc: "Only orders the bridge has synced; unsynced orders count as unknown."),
                Msr("oosCases", "OOS Cases (synced)", $"{OosPcs} * 1.0 / NULLIF(l.Pieces, 0)", format: "dec", joins: ["o", "sy"],
                    desc: "Ordered minus allocated, synced orders only. A line missing from the BMS snapshot is fully OOS."),
                Calc("fillRate", "Allocation Fill Rate %",
                    $"SUM(CASE WHEN sy.Synced = 1 THEN CASE WHEN {AllocPcs} < l.OrderedPcs THEN {AllocPcs} ELSE l.OrderedPcs END END) * 1.0 / NULLIF(SUM(CASE WHEN sy.Synced = 1 THEN l.OrderedPcs END), 0)",
                    "pct", additive: false, joins: ["o", "sy"],
                    desc: "Allocated ÷ ordered at BMS PROCESS time (OosSyncLines), synced orders only. Not delivery."),
                Calc("deliveryFillRate", "Delivery Fill Rate %",
                    $"SUM(CASE WHEN {DelivPcs} IS NULL THEN NULL WHEN {DelivPcs} < l.OrderedPcs THEN {DelivPcs} ELSE l.OrderedPcs END) * 1.0 / NULLIF(SUM(CASE WHEN {DelivPcs} IS NOT NULL THEN l.OrderedPcs END), 0)",
                    "pct", additive: false, joins: Inv,
                    desc: "Delivered ÷ ordered cases (in pieces), delivered orders only. Delivered = BMS-reconciled received qty, else INV CS."),
                Msr("invLineAmt", "Invoiced Amount", "o.NetAmt", format: "php", joins: ["o"],
                    desc: "BMS NETAMT per line — only filled for orders synced since 2026-09-01."),
                Msr("invoicedCases", "Invoiced Cases (INV CS)", $"{InvPcs} * 1.0 / NULLIF(l.Pieces, 0)", format: "dec", joins: Inv,
                    desc: "INV CS — the invoice-time BMS allocation, same as the Sales Order screen. Invoiced orders only; a line cut from the invoice = 0."),
                Msr("receivedCases", "Delivered Cases", $"{DelivPcs} * 1.0 / NULLIF(l.Pieces, 0)", format: "dec", joins: Inv,
                    desc: "Delivered orders only: BMS-reconciled received cases (VSDET), else INV CS (assumed fully received until BMS reconciles)."),
                Msr("receivedAmt", "Delivered Amount", $"CASE WHEN {Live} THEN COALESCE(l.ReceivedAmt, CASE WHEN so.Delivered IS NOT NULL THEN o.NetAmt END) END", format: "php", joins: ["o"],
                    desc: "BMS-reconciled received amount, else the invoiced line amount (NETAMT) for delivered orders."),
                Msr("undeliveredCases", "Undelivered CS",
                    $"CASE WHEN {DelivPcs} IS NOT NULL THEN CASE WHEN l.OrderedPcs > {DelivPcs} THEN l.OrderedPcs - {DelivPcs} ELSE 0 END * 1.0 / NULLIF(l.Pieces, 0) END",
                    format: "dec", joins: Inv,
                    desc: "Ordered minus delivered cases, delivered orders only. Equals OOS Cases until BMS reconciles receipts; afterwards it also includes delivery rejections."),
                Dim("deliveredBasis", "Delivered Basis",
                    "CASE WHEN l.ReceivedPcs IS NOT NULL THEN 'Received (reconciled)' WHEN so.Delivered IS NOT NULL THEN 'INV CS (assumed)' END",
                    desc: "Where Delivered Cases comes from: BMS-reconciled receipts, or INV CS while BMS hasn't reconciled. Blank = not delivered."),
                Flag("receiptReconciled", "Receipt Reconciled?", "l.ReceivedPcs IS NOT NULL",
                    desc: "True once BMS has reconciled this line's delivery (received quantities are known)."),
                Msr("estAmt", "Est. Value @ Order-Date List Price",
                    $"CASE WHEN l.FreeGoods = 1 THEN 0 ELSE l.OrderedPcs * 1.0 / NULLIF(l.Pieces, 0) * {ListPriceVat} END",
                    format: "php", joins: Px,
                    desc: "ESTIMATE: ordered cases x list price in force on the order date (base + zone + special add-on) x VAT. Not invoiced revenue."),
                Msr("estOosAmt", "Est. OOS Value",
                    $"CASE WHEN l.FreeGoods = 1 THEN 0 ELSE {OosPcs} * 1.0 / NULLIF(l.Pieces, 0) * {ListPriceVat} END",
                    format: "php", joins: ["o", "sy", "px"], desc: "ESTIMATE: OOS cases x order-date list price x VAT."),
            ],
            SalesScope, DefaultDate: "orderDate",
            Joins:
            [
                ProductJoin("l"), CategoryJoin,
                new("o", "LEFT JOIN OosSyncLines o ON o.SoId = l.SoId AND o.CProdNo = l.CProdNo"),
                new("sy", "OUTER APPLY (SELECT CASE WHEN EXISTS (SELECT 1 FROM OosSyncLines x WHERE x.SoId = l.SoId) THEN 1 ELSE 0 END AS Synced) sy"),
                .. SalesJoins,
                new("px", PriceApply, "p", "cu"),
            ],
            AsOfSql: SalesAsOf,
            Detail: ["soId", "soNo", "orderDate", "custKey", "cProdNo", "orderedCases", "allocatedCases", "oosCases", "estAmt"]),

        // ── D3 Customer PO Log ───────────────────────────────────────────────
        new("po_logs", "Customer PO Log", "One row per logged customer PO (includes a 2011+ BMS history seed)",
            "PoLogs pl",
            [
                Dim("poNum", "PO #", "pl.PoNum"),
                Dim("custKey", "Customer", "pl.CustKey", caption: "pl.CusName"),
                Dim("source", "Source", "CASE WHEN pl.IsSeeded = 1 THEN 'BMS history' ELSE 'HOMSys' END"),
                Dim("bmsPrefix", "BMS Branch Prefix", "LEFT(CAST(pl.SoNo AS varchar(10)), 2)",
                    desc: "First 2 digits of the BMS SO number = BMS install code."),
                Dim("branch", "Encoding Branch", "so.Branch", joins: ["so"]),
                Dim("soNo", "SO #", "pl.SoNo", "int"),
                Dt("orderDate", "Order Date", "pl.OrderDate"),
                Dt("poDate", "PO Date", "pl.PoDate"),
                Dt("createdAt", "Logged At", "pl.CreatedAt", utc: true),
                Calc("pos", "POs", "COUNT(*)", "int", "int"),
                Calc("duplicateUses", "Duplicate PO Uses", "COUNT(*) - COUNT(DISTINCT pl.PoNum)", "int", "int", additive: false,
                    desc: "Times a PO number was re-used within the group."),
            ],
            "pl.SoId IN (SELECT s.SoId FROM SalesOrders s WHERE s.Branch = @scSite)",
            DefaultDate: "orderDate",
            Joins: [new("so", "LEFT JOIN SalesOrders so ON so.SoId = pl.SoId")],
            AsOfSql: "SELECT MAX(CreatedAt) FROM PoLogs",
            Detail: ["poNum", "poDate", "orderDate", "custKey", "soNo", "source"]),

        // ── D4 Customer Master ───────────────────────────────────────────────
        new("customers", "Customer Master", "One row per customer record per customer folder",
            "Customers c",
            [
                Dim("folder", "Customer Folder", "c.Branch", drill: "zone"),
                Dim("branch", "Branch", "CASE WHEN c.Branch = 'hon' THEN ISNULL(sw.SiteCode, '(unmapped)') ELSE UPPER(c.Branch) + '-B' END",
                    joins: ["sw"], drill: "zone", desc: "Sites.Code. hon customers map through Sites.Cuwhsenos (WhseNo); ambiguous = (multiple)."),
                Dim("zone", "Zone", "c.CZone", caption: "zm.CDesc", joins: ["zm"], drill: "custKey"),
                Dim("custKey", "Customer", "c.CustKey", caption: "c.CusName"),
                Dim("whseNo", "WhseNo", "c.WhseNo", "int"),
                Dim("csMan", "Salesman", "c.CsMan"),
                Dim("termDays", "Term (days)", "c.TermDays", "int"),
                Dim("cCode", "Customer Class", "c.CCode", "int"),
                Dim("delArea", "Delivery Area", "c.DelArea"),
                Dim("customerName", "Customer Name", "c.CusName", desc: "Use 'contains' to search by name."),
                Dim("cKey", "Customer Code (CKey)", "c.CKey"),
                Dim("address", "Address", "c.AddrLn1 + CASE WHEN LTRIM(ISNULL(c.AddrLn2, '')) <> '' THEN ', ' + c.AddrLn2 ELSE '' END"),
                Dim("deliveryAddress", "Delivery Address", "c.DelAddrLn1 + CASE WHEN LTRIM(ISNULL(c.DelAddrLn2, '')) <> '' THEN ', ' + c.DelAddrLn2 ELSE '' END"),
                Dim("salesmanNo", "Salesman No", "c.Salesman", "int"),
                Dim("term", "Term Code", "c.Term", "int"),
                Dim("oldCCode", "Old Customer Class", "c.OldCCode", "int"),
                Dim("tin", "TIN", "c.Tin"),
                Dim("vatId", "VAT ID", "c.VatId"),
                Dim("custWhse", "Customer Whse", "c.CustWhse", "int"),
                Dim("delWhse", "Delivery Whse", "c.DelWhse", "int"),
                Dim("serveWh", "Serving Whse", "c.ServeWh", "int"),
                Dim("aliasKey", "Alias Key", "c.AliasKey"),
                Flag("exBranch", "Ex-branch?", "c.ExBranch = 1"),
                Flag("inactive", "Inactive?", "c.Inactive = 1"),
                Flag("blockInv", "Invoice Blocked?", "c.BlockInv = 1"),
                Flag("tpc", "TPC?", "c.Tpc = 1"),
                Flag("offshore", "Offshore?", "c.Offshore = 1"),
                Flag("hasZoneLabel", "Zone Has Label?", "zm.CDesc IS NOT NULL", ["zm"]),
                Dt("firstOrder", "First Order", "c.FirstOrder"),
                Dt("iEffDate", "Class Effective Date", "c.IEffDate"),
                Dt("importedAt", "Imported At", "c.ImportedAt", utc: true),
                Calc("customers", "Customers", "COUNT(DISTINCT c.CustKey)", "int", "int", additive: false,
                    desc: "Distinct CustKeys (a key can exist in more than one folder)."),
                Calc("records", "Records", "COUNT(*)", "int", "int"),
                Msr("avgTermDays", "Avg Term (days)", "c.TermDays", "avg", "dec", additive: false),
            ],
            "c.Branch = @scFolder AND (@scWhN = 0 OR c.WhseNo IN @scWh)",
            Joins:
            [
                new("zm", "OUTER APPLY (SELECT TOP 1 z.CDesc FROM ZoneMasts z WHERE z.Branch = CASE WHEN c.Branch IN ('DAG','ISA','LEG','LUC','mxs','NAG') THEN 'hon' ELSE c.Branch END AND z.CZone = c.CZone ORDER BY z.Id) zm"),
                new("sw", SiteByWhseApply),
            ],
            AsOfSql: MastersAsOf,
            Detail: ["custKey", "folder", "branch", "zone", "whseNo", "csMan", "termDays", "inactive"],
            Defaults: [new SpecFilter("inactive", "eq", ["false"])],
            Heavy: true),

        // ── D5 Products (national) ───────────────────────────────────────────
        new("products", "Products", "One row per SKU (national product master)",
            "Products p",
            [
                Dim("cProdNo", "Product", "p.CProdNo", caption: MasterProductName),
                .. ProductAttrs(false),
                CategoryDim, CategoryGroupDim,
                Dim("brand", "Brand", "p.Brand", drill: "cProdNo"),
                Flag("onPricelist", "Pricelist Flag?", "p.PriceList = 1"),
                Flag("phOut", "Phased Out?", "p.PhOut = 1"),
                Flag("pricelistEligible", "Pricelist Eligible?",
                    "p.PriceList = 1 AND p.Brand + p.SBrand <> '' AND p.Brand <> 'INACTIVE' AND p.Pieces > 0 AND p.PhOut = 0 AND UPPER(p.Category) <> '99X'",
                    desc: "Same gates as the Pricelist Export."),
                Flag("excludedFromPricelist", "Excluded (PRLISTX)?", "xr.Excl = 1", ["xr"]),
                Flag("zoneRestricted", "Zone-restricted (PRLISTX2)?", "xr.ZoneRestr = 1", ["xr"]),
                Dt("priceFrom", "Price Effective From", "p.PriceFrom"),
                Calc("skus", "SKUs", "COUNT(*)", "int", "int"),
                Msr("newPrice", "Base Price (ex-VAT)", "p.NewPrice", "avg", "php", additive: false),
                Msr("srp", "SRP", "p.Srp", "avg", "php", additive: false),
                Msr("oldPrice", "Previous Base Price", "p.OldPrice1", "avg", "php", additive: false),
                Msr("taxRatePct", "Tax Rate %", "p.TaxRate * 100", "avg", "dec", additive: false),
                Calc("priceChangePct", "Last Price Change %", "AVG(CASE WHEN p.OldPrice1 > 0 THEN p.NewPrice / p.OldPrice1 - 1 END)", "pct", additive: false),
            ],
            null, National: true,
            Joins:
            [
                CategoryJoin with { DependsOn = [] },
                new("xr", "OUTER APPLY (SELECT CASE WHEN EXISTS (SELECT 1 FROM PrlistXRestrictions x WHERE x.CProdNo = p.CProdNo) THEN 1 ELSE 0 END AS Excl, CASE WHEN EXISTS (SELECT 1 FROM PrlistX2Restrictions y WHERE y.CProdNo = p.CProdNo) THEN 1 ELSE 0 END AS ZoneRestr) xr"),
            ],
            AsOfSql: MastersAsOf,
            Detail: ["cProdNo", "category", "brand", "packSize", "priceFrom", "newPrice", "srp"]),

        // ── D6 Base Price History (national) ─────────────────────────────────
        new("price_history", "Base Price History", "One row per national base-price change (PRCHST)",
            "PriceHistories h",
            [
                Dim("prodNo", "Prod No", "h.ProdNo", "int"),
                Dim("cProdNo", "Product", "p.CProdNo", caption: MasterProductName, joins: ["p"]),
                Dim("productDesc", "Product Description", MasterProductName, joins: ["p"]),
                Dim("packSize", "Pack Size", "p.PackSize", joins: ["p"]),
                CategoryDim,
                Dim("brand", "Brand", "p.Brand", joins: ["p"], drill: "cProdNo"),
                Flag("inMaster", "In Product Master?", "p.CProdNo IS NOT NULL", ["p"]),
                Dt("effective", "Effective Date", "h.Effective"),
                Calc("priceEvents", "Price Changes", "COUNT(*)", "int", "int"),
                Msr("npAfVat", "Base Price (VAT incl.)", "h.NpAfVat", "avg", "php", additive: false),
            ],
            null, National: true, DefaultDate: "effective",
            Joins:
            [
                new("p", "OUTER APPLY (SELECT TOP 1 * FROM Products p0 WHERE p0.ProdNo = h.ProdNo ORDER BY p0.Id) p"),
                CategoryJoin,
            ],
            AsOfSql: MastersAsOf,
            Detail: ["prodNo", "cProdNo", "effective", "npAfVat"],
            Defaults: [new SpecFilter("npAfVat", "gt", ["0"])]),

        // ── D7 Zone Add-ons in force ─────────────────────────────────────────
        new("zone_addons", "Zone Add-ons (in force)", "One row per (pricing folder, product, zone) — the add-on effective today",
            """
            (SELECT z.Branch, z.CProdNo, z.CZone, z.EffDate, z.AddOn, z.Rate, z.FixAmt,
                    ROW_NUMBER() OVER (PARTITION BY z.Branch, z.CProdNo, z.CZone ORDER BY z.EffDate DESC, z.RecNo DESC) AS rn
               FROM ZoneAddOns z WHERE z.EffDate IS NULL OR z.EffDate <= @today) za
            """,
            [
                Dim("folder", "Pricing Folder", "za.Branch", drill: "zone"),
                Dim("zone", "Zone", "za.CZone", caption: "zm.CDesc", joins: ["zm"], drill: "cProdNo"),
                ProductDim("za"), .. ProductAttrs(true), CategoryDim, BrandDim,
                Flag("inMaster", "In Product Master?", "p.CProdNo IS NOT NULL", ["p"]),
                Flag("zeroAddOn", "Zero Add-on?", "za.AddOn = 0"),
                Dt("effDate", "Effective Date", "za.EffDate"),
                Calc("rows", "Add-on Rows", "COUNT(*)", "int", "int"),
                Msr("addOn", "Add-on (per case)", "za.AddOn", "avg", "php", additive: false),
                Msr("ratePct", "Rate %", "za.Rate * 100", "avg", "dec", additive: false),
                Msr("fixAmt", "Fixed Amount", "za.FixAmt", "avg", "php", additive: false),
            ],
            "za.Branch = @scFolder AND (@scWhN = 0 OR za.CZone IN (SELECT c.CZone FROM Customers c WHERE c.Branch = @scFolder AND c.WhseNo IN @scWh))",
            Where: "za.rn = 1",
            Joins:
            [
                ProductJoin("za"), CategoryJoin,
                new("zm", "OUTER APPLY (SELECT TOP 1 m.CDesc FROM ZoneMasts m WHERE m.Branch = za.Branch AND m.CZone = za.CZone ORDER BY m.Id) zm"),
            ],
            AsOfSql: MastersAsOf,
            Detail: ["folder", "zone", "cProdNo", "effDate", "addOn", "ratePct", "fixAmt"],
            Heavy: true),

        // ── D8 Special (Zone2) Add-ons in force ──────────────────────────────
        new("zone2_addons", "Special Add-ons (in force)", "One row per (pricing folder, customer or chain key, product) — effective today",
            """
            (SELECT z.Branch, z.CustKey, z.CProdNo, z.EffDate, z.AddOn, z.Rate, z.FixAmt,
                    ROW_NUMBER() OVER (PARTITION BY z.Branch, z.CProdNo, z.CustKey ORDER BY z.EffDate DESC, z.RecNo DESC) AS rn
               FROM Zone2AddOns z WHERE z.EffDate IS NULL OR z.EffDate <= @today) z2
            """,
            [
                Dim("folder", "Pricing Folder", "z2.Branch", drill: "custKey"),
                Dim("keyType", "Key Type", "CASE WHEN LEN(z2.CustKey) <= 4 THEN 'Zone/Chain' ELSE 'Customer' END",
                    desc: "ZONE2 keys are either a customer key or a chain/zone code."),
                Dim("custKey", "Customer / Chain Key", "z2.CustKey", caption: "cn.CusName", joins: ["cn"]),
                ProductDim("z2"), .. ProductAttrs(true), CategoryDim, BrandDim,
                Dt("effDate", "Effective Date", "z2.EffDate"),
                Calc("rows", "Add-on Rows", "COUNT(*)", "int", "int"),
                Msr("addOn", "Add-on (per case)", "z2.AddOn", "avg", "php", additive: false),
                Msr("ratePct", "Rate %", "z2.Rate * 100", "avg", "dec", additive: false),
            ],
            "z2.Branch = @scFolder AND (@scWhN = 0 OR z2.CustKey IN (SELECT c.CustKey FROM Customers c WHERE c.Branch = @scFolder AND c.WhseNo IN @scWh) OR z2.CustKey IN (SELECT c.CZone FROM Customers c WHERE c.Branch = @scFolder AND c.WhseNo IN @scWh))",
            Where: "z2.rn = 1",
            Joins:
            [
                ProductJoin("z2"), CategoryJoin,
                new("cn", "OUTER APPLY (SELECT TOP 1 c.CusName FROM Customers c WHERE c.CustKey = z2.CustKey ORDER BY c.ImportedAt DESC, c.Id DESC) cn"),
            ],
            AsOfSql: MastersAsOf,
            Detail: ["folder", "keyType", "custKey", "cProdNo", "effDate", "addOn"]),

        // ── D9 Users ─────────────────────────────────────────────────────────
        new("users", "Users", "One row per HOMSys login",
            "Users u",
            [
                Dim("username", "User", "u.Username", caption: "u.FirstName + ' ' + u.LastName"),
                Dim("branchCode", "Branch Code", "u.BranchCode"),
                Dim("site", "Site", "u.SiteId", "int", caption: "s.Name", joins: ["s"]),
                Dim("roles", "Roles", "r.Roles", joins: ["r"]),
                Flag("isActive", "Active?", "u.IsActive = 1"),
                Dt("lastLoginAt", "Last Login", "u.LastLoginAt", utc: true),
                Dt("createdAt", "Created At", "u.CreatedAt", utc: true),
                Calc("users", "Users", "COUNT(*)", "int", "int"),
            ],
            "u.BranchCode = @scSite", Permission: "users",
            Joins:
            [
                new("s", "LEFT JOIN Sites s ON s.Id = u.SiteId"),
                new("r", "OUTER APPLY (SELECT STRING_AGG(ro.Name, ', ') WITHIN GROUP (ORDER BY ro.Name) AS Roles FROM UserRoles ur JOIN Roles ro ON ro.Id = ur.RoleId WHERE ur.UserId = u.Id) r"),
            ],
            AsOfSql: "SELECT MAX(LastLoginAt) FROM Users",
            Detail: ["username", "branchCode", "site", "roles", "isActive", "lastLoginAt"]),

        // ── D10 Login Activity ───────────────────────────────────────────────
        new("user_sessions", "Login Activity", "One row per issued refresh token (≈ one per login or session refresh)",
            "RefreshTokens rt JOIN Users u ON u.Id = rt.UserId",
            [
                Dim("username", "User", "u.Username", caption: "u.FirstName + ' ' + u.LastName"),
                Dim("branchCode", "Branch Code", "u.BranchCode"),
                Flag("revoked", "Revoked?", "rt.IsRevoked = 1"),
                Dt("createdAt", "Issued At", "rt.CreatedAt", utc: true),
                Calc("tokens", "Session Refreshes", "COUNT(*)", "int", "int"),
                Calc("activeUsers", "Active Users", "COUNT(DISTINCT rt.UserId)", "int", "int", additive: false),
            ],
            "u.BranchCode = @scSite", Permission: "users", DefaultDate: "createdAt",
            AsOfSql: "SELECT MAX(CreatedAt) FROM RefreshTokens",
            Detail: ["username", "branchCode", "createdAt", "revoked"]),

        // ── D11–D13 Collections, invoices & bank (finance) ───────────────────
        .. CollectionDatasets,
    ];

    /// <summary>Sales &amp; pricing datasets: only roles on the sales side (or analytics admins) — not finance-only roles.</summary>
    private const string SalesPermission = "sales-orders|oos-report|pricelist-export|data-analytics-admin";
    private static readonly string[] SalesSide =
        ["sales_orders", "sales_lines", "po_logs", "customers", "products", "price_history", "zone_addons", "zone2_addons"];

    public static readonly Ds[] All =
        Base.Select(d => d.Permission is null && SalesSide.Contains(d.Key) ? d with { Permission = SalesPermission } : d).ToArray();

    public static readonly IReadOnlyDictionary<string, Ds> ByKey =
        All.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);

    static AnalyticsCatalog()
    {
        // A dataset that forgets its RLS rule must never register.
        foreach (var d in All)
        {
            if (d.National == (d.Scope is not null))
                throw new InvalidOperationException($"Analytics dataset '{d.Key}' must be National or declare a Scope.");
            var dupe = d.Fields.GroupBy(f => f.Key, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
            if (dupe is not null)
                throw new InvalidOperationException($"Analytics dataset '{d.Key}' has duplicate field '{dupe.Key}'.");
        }
    }

    public static Fld? Field(this Ds ds, string key) =>
        ds.Fields.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));
}
