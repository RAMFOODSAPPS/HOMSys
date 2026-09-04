namespace HOMSys.Domain.Entities;

/// <summary>
/// Sales order header. Mirrors the BMS <c>oowkhdr.DBF</c> (138 fields).
///
/// Columns are split into two regions:
///
///   ENCODE-OWNED — written by this module when an order is saved.
///   BMS-OWNED    — downstream workflow state. Nullable, never written by
///                  HOMSys. They exist so the Python bridge maps 1:1 and so
///                  BMS state can be read back later.
///
/// Do not write to a BMS-owned column from HOMSys code.
/// </summary>
public class SalesOrder
{
    // ── HOMSys identity ──────────────────────────────────────────────────────

    /// <summary>HOMSys internal key. Not the BMS sales order number.</summary>
    public int SoId { get; set; }

    /// <summary>
    /// The BMS sales order number. Stays NULL in HOMSys.
    ///
    /// BMS assigns this at save via docnum("SO",0,"GETSAVE"), a spin-locked
    /// counter in docnum.dbf. HOMSys deliberately does not participate in that
    /// lock — the Python bridge takes the number at push time and writes it back
    /// here, along with DocNo.
    /// </summary>
    public int? SoNo { get; set; }

    /// <summary>oowkhdr.DOCNO — same value as SoNo. Set by the bridge, not here.</summary>
    public int? DocNo { get; set; }

    // ── ENCODE-OWNED ─────────────────────────────────────────────────────────

    /// <summary>Branch this order belongs to — stamped from the encoding user's
    /// own BranchCode at creation, not derived from the customer. Determines
    /// which branch's bridge instance is allowed to pull/push this order.</summary>
    public string? Branch { get; set; }

    public string CustKey { get; set; } = string.Empty;
    public string CusName { get; set; } = string.Empty;
    public string CKey { get; set; } = string.Empty;

    public DateOnly OrderDate { get; set; }

    public int CCode { get; set; }
    public int WhseNo { get; set; }
    public int CustWhse { get; set; }

    public string ShipToLn1 { get; set; } = string.Empty;
    public string ShipToLn2 { get; set; } = string.Empty;
    public string DelArea { get; set; } = string.Empty;

    public int Term { get; set; }
    public int TermDays { get; set; }
    public int Salesman { get; set; }
    public string CsMan { get; set; } = string.Empty;

    public string PoNum { get; set; } = string.Empty;
    public DateOnly? PoDate { get; set; }

    /// <summary>HOMSys-only field — no legacy oowkhdr column. Not written back by the Python bridge.</summary>
    public DateOnly? CancelDate { get; set; }

    /// <summary>oowkhdr.INVREM C(100) — the form's "Remarks" box.</summary>
    public string InvRem { get; set; } = string.Empty;

    /// <summary>oowkhdr.REMARKS C(60) — separate, shorter remarks column.</summary>
    public string Remarks { get; set; } = string.Empty;

    public bool Tpc { get; set; }
    public bool Offshore { get; set; }
    public bool ExBranch { get; set; }
    public string VatId { get; set; } = string.Empty;

    /// <summary>Legacy save sets this false explicitly.</summary>
    public bool ExpectDel { get; set; }

    public string UserName { get; set; } = string.Empty;
    public DateOnly? SysDate { get; set; }

    // O.R. details — only captured when Term = 0 (cash customer).
    public int? OrNo { get; set; }
    public DateOnly? ChkDate { get; set; }
    public decimal? OrAmt { get; set; }

    /// <summary>oowkhdr.DOCCLASS — code into DocClass (docclass.DBF). Not term-gated.</summary>
    public string? DocClass { get; set; }

    // Encoding-speed instrumentation the legacy form records.
    public DateTime? SoTymStart { get; set; }
    public DateTime? SoTymEnd { get; set; }
    public string SoElapsed { get; set; } = string.Empty;

    // ── HOMSys audit ─────────────────────────────────────────────────────────

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>SHA-256 hash of the source Excel/CSV file, set only when this
    /// order was created via the import wizard. Used to hard-block re-importing
    /// the exact same file. Null for manually encoded orders.</summary>
    public string? SourceFileHash { get; set; }

    /// <summary>Original filename of the import source, for display in the
    /// "already processed" message. Null for manually encoded orders.</summary>
    public string? SourceFileName { get; set; }

    /// <summary>True once this order has been pushed to BMS (SoNo assigned) —
    /// editing is refused in HOMSys until BMS deallocates it. Set true in
    /// SalesOrderBridgeService.ConfirmAsync, set false in DeallocateAsync, and
    /// set true again by an edit that sets NeedsResync (re-locked until BMS
    /// confirms that edit landed).</summary>
    public bool IsLocked { get; set; }

    /// <summary>True when this order was edited in HOMSys after already having
    /// a BMS SoNo (i.e. deallocated, then edited again) — the edit still needs
    /// to be pushed into BMS's live oowkhdr/oowkdet record. Cleared by
    /// SalesOrderBridgeService.ConfirmResyncAsync once BMS applies it.</summary>
    public bool NeedsResync { get; set; }

    /// <summary>True when BMS could not find the live oowkhdr record to apply
    /// a resync onto (e.g. DOCNO vanished/renumbered) — surfaced to the user
    /// instead of leaving NeedsResync stuck true forever.</summary>
    public bool ResyncFailed { get; set; }

    /// <summary>
    /// One of Entered / Downloaded / Processed / Deallocated / Invoiced. Set at
    /// each corresponding transition: default on create, ConfirmAsync (SoNo
    /// assigned), LockAsync (cmdproc.Click processed it), DeallocateAsync
    /// (a1112.scx reset it), ConfirmInvoiceAsync (INVNO recorded). Display-only
    /// — IsLocked/InvNo remain the source of truth for edit-blocking behavior.
    /// </summary>
    public string WorkflowStatus { get; set; } = "Entered";

    public ICollection<SalesOrderLine> Lines { get; set; } = new List<SalesOrderLine>();

    /// <summary>Latest bridge-synced oowkdet snapshot, per CProdNo. See OosSyncLine.</summary>
    public ICollection<OosSyncLine> OosSyncLines { get; set; } = new List<OosSyncLine>();

    // ── BMS-OWNED — downstream workflow state. Never written by HOMSys. ───────

    public int? InvNo { get; set; }
    public DateOnly? InvDate { get; set; }
    public decimal? InvAmt { get; set; }
    public decimal? InvTax { get; set; }
    public int? TransInv { get; set; }

    public int? RsrNo { get; set; }
    public DateOnly? RsrDate { get; set; }
    public int? OrigSo { get; set; }

    public bool? RFlag { get; set; }
    public int? DcsNo { get; set; }
    public int? MjReqNo { get; set; }
    public bool? Promodel { get; set; }
    public bool? PrintFccor { get; set; }

    public int? DrNo { get; set; }
    public int? PoNo { get; set; }
    public int? GrNo { get; set; }
    public int? BirNo { get; set; }
    public int? DpiNo { get; set; }
    public DateOnly? DpiDate { get; set; }

    public string? Depot { get; set; }
    public string? TransTag { get; set; }
    public int? RTotCs { get; set; }
    public int? RTotPc { get; set; }

    public string? Status { get; set; }
    public string? CrAuthor { get; set; }
    public string? StatDesc { get; set; }
    public string? CauseFail { get; set; }

    public DateOnly? Transmit { get; set; }
    public DateOnly? PickListed { get; set; }
    public int? PickNo { get; set; }
    public DateOnly? PickTDate { get; set; }
    public DateOnly? ServeDate { get; set; }
    public DateOnly? FccrDate { get; set; }
    public DateOnly? PassDate { get; set; }
    public DateOnly? Delivered { get; set; }

    public int? RrNo { get; set; }
    public DateOnly? RrDate { get; set; }

    public string? PlateNo { get; set; }
    public string? Trucker { get; set; }
    public string? Driver { get; set; }
    public int? VsNo { get; set; }
    public DateOnly? VsDate { get; set; }
    public string? VsRemarks { get; set; }

    public int? BirFrom { get; set; }
    public int? BirTo { get; set; }
    public decimal? Cewit { get; set; }

    public string? OldCsMan { get; set; }
    public int? OldWhseNo { get; set; }
    public int? OldSMan { get; set; }

    public int? TrType { get; set; }
    public int? ServeWh { get; set; }

    public string? DssNo { get; set; }
    public decimal? DssAmt { get; set; }
    public DateOnly? DssDate { get; set; }
    public bool? Allocate { get; set; }

    public int? FccosNo { get; set; }
    public decimal? FccosAmt { get; set; }

    public decimal? Cl { get; set; }
    public decimal? CurSlAmt { get; set; }
    public decimal? CurrentSo { get; set; }
    public decimal? CurrPay { get; set; }
    public decimal? PendingSo { get; set; }

    public int? OrNo1 { get; set; }
    public DateOnly? OrDate1 { get; set; }
    public decimal? OrAmt1 { get; set; }
    public string? CheckNo1 { get; set; }
    public DateOnly? CheckDate1 { get; set; }
    public int? Invoice1 { get; set; }

    public int? ChainCl { get; set; }
    public string? ChainCode { get; set; }

    public string? Reason { get; set; }
    public int? ReasonCode { get; set; }
    public bool? WithVat { get; set; }

    public int? SubdSMan { get; set; }
    public string? SubdName { get; set; }

    public int? RefRgwNo { get; set; }
    public DateOnly? RefRgwDate { get; set; }
    public decimal? RefRgwAmt { get; set; }
    public int? Rfc4Rgw { get; set; }

    public int? JobberWhse { get; set; }
    public string? JobberCKey { get; set; }

    public string? Rem1 { get; set; }
    public string? Rem2 { get; set; }
    public string? Rem3 { get; set; }

    public string? VhType { get; set; }
    public int? ChargeType { get; set; }
    public decimal? Rate { get; set; }
    public int? Drops { get; set; }

    public int? RefSoReq { get; set; }
    public bool? VisMin { get; set; }

    public string? Vessel { get; set; }
    public string? WaybillNo { get; set; }
    public string? Voyage { get; set; }
    public string? Van { get; set; }
    public string? BlNo { get; set; }
    public DateOnly? Edd { get; set; }
    public DateOnly? Eda2 { get; set; }

    public int? DelWhse { get; set; }
    public bool? Moe { get; set; }

    public DateTime? TimeStart { get; set; }
    public DateTime? TimeEnd { get; set; }
    public string? Elapsed { get; set; }

    public bool? IsPsuedoSo { get; set; }
    public bool? ExtWhse { get; set; }
    public string? DrpCust { get; set; }
}
