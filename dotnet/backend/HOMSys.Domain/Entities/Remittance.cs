namespace HOMSys.Domain.Entities;

/// <summary>
/// Remittance Advice (NKA chains, user 2026-10-15): one customer payment (e.g. SVI check 8700129452) holding 1+ CRs.
/// Each CR is a normal Collection at Head Office whose invoices + deductions come from the chain's RA Excel
/// (one file per OR, see RemittanceLine). The Finance Verifier verifies the RA as a whole; its CRs follow its status.
/// Encoded by the Custodian Verifier (collections-ra-encode, every branch).
/// </summary>
public class Remittance
{
    public int Id { get; set; }

    /// <summary>Head Office: the CRs apply invoices of any branch; their ORs go to the HOMS Luzon / VisMin desk.</summary>
    public int SiteId { get; set; }
    public Site Site { get; set; } = null!;

    /// <summary>CHAIN NAME in the RA Excel (e.g. "SVI").</summary>
    public string ChainName { get; set; } = string.Empty;

    /// <summary>Check or BankTransfer — same rules as a CR.</summary>
    public PaymentMode PaymentMode { get; set; } = PaymentMode.Check;
    /// <summary>Drawee bank, check no. and date of the customer's check (none for a transfer).</summary>
    public string? Bank { get; set; }
    public string? CheckNo { get; set; }
    public DateOnly? CheckDate { get; set; }
    /// <summary>The whole payment; its CRs must add up to it before the RA can be submitted.</summary>
    public decimal CheckAmount { get; set; }

    /// <summary>One deposit slip for the payment; every member CR is put on it.</summary>
    public int? DepositBatchId { get; set; }
    public DepositBatch? DepositBatch { get; set; }

    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Draft;
    public string? ReturnReason { get; set; }
    public string? VoidReason { get; set; }
    public string? Remarks { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerifiedBy { get; set; }

    public ICollection<Collection> Collections { get; set; } = [];
    public ICollection<RemittanceHistory> History { get; set; } = [];
}

/// <summary>
/// One invoice block of a CR's RA Excel, as read, with what HOMSys found for it. The CR's applied invoices are rebuilt
/// from these lines: only lines that match their invoice are applied. A mismatch is corrected in the Excel and the
/// Excel uploaded again (user 2026-10-15: no accepting differences).
/// </summary>
public class RemittanceLine
{
    public int Id { get; set; }
    public int CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;

    /// <summary>Excel row of the block's first line (1-based, as the user sees it).</summary>
    public int RowNo { get; set; }
    public int DocNo { get; set; }
    /// <summary>Warehouse = the invoice no.'s first two digits (the SL file it's in).</summary>
    public int WhseNo { get; set; }
    public string CustKey { get; set; } = string.Empty;
    public DateOnly? DocDate { get; set; }
    public string? BirSeries { get; set; }
    /// <summary>The Excel gave only the BIR series (Puregold, user 2026-11-25): DocNo was looked up in HOMS's B1 files
    /// (InvoiceBirSeries), not typed.</summary>
    public bool DocNoFromBir { get; set; }

    public decimal Gross { get; set; }
    public decimal Cewt { get; set; }
    public decimal RfcBoRtv { get; set; }
    public decimal Other { get; set; }
    public decimal Net { get; set; }
    /// <summary>"With TTC" = T (autocrcs): its CEWIT is credited to A/R with the net and debited to 10507 on the OR.</summary>
    public bool Ttc { get; set; }
    /// <summary>Each deduction row of the block: [{type, amount, remark, dealCode, rfcNo, ref}] (JSON).</summary>
    public string DeductionsJson { get; set; } = "[]";

    public int? PostedInvoiceId { get; set; }
    public PostedInvoice? PostedInvoice { get; set; }
    public decimal? SystemTotal { get; set; }
    /// <summary>Returns already posted in BMS for the invoice (positive), netted against the RA's RFC/BO/RTV.</summary>
    public decimal? SystemRfc { get; set; }
    /// <summary>Invoice fetch from HO (InvoiceSyncRequest) while it isn't in HOMSys yet.</summary>
    public int? FetchRequestId { get; set; }

    /// <summary>Comma-separated RemittanceFlags; empty = matched.</summary>
    public string Flags { get; set; } = string.Empty;
}

/// <summary>Why a line doesn't match its invoice. Every flag but the <see cref="Soft"/> ones blocks submit (no accepting
/// differences): correct the RA Excel and upload it again. RfcDiff (the chain's RFC/BO/RTV vs the RFCs posted in HOMS A/R)
/// is soft since 2026-11-26: the line is applied and its return waits for its RFC like any receipt's — the OR is held from
/// HOMS A/R until the CNC Verifier links it (or it links itself), "aligned first before AR will clear" (user).</summary>
public static class RemittanceFlags
{
    /// <summary>Shown, but the line is applied and the RA can be submitted.</summary>
    public static readonly IReadOnlySet<string> Soft = new HashSet<string> { "RfcDiff" };

    /// <summary>VatExcluded: the RA gross is the invoice total without its 12% VAT (e.g. SVI's Butuan invoices) —
    /// a GrossDiff with a known cause; the VAT stays open on the invoice.</summary>
    /// <summary>BirNotFound / BirAmbiguous: a line given only by its BIR series that HOMS's B1 files map to no invoice of
    /// the customer (not there yet, or mistyped) / to several.</summary>
    public const string Fetching = "Fetching", NotFound = "NotFound", GrossDiff = "GrossDiff", VatExcluded = "VatExcluded", CustDiff = "CustDiff",
        DateDiff = "DateDiff", RfcDiff = "RfcDiff", OverBalance = "OverBalance", NetMismatch = "NetMismatch", Duplicate = "Duplicate",
        BirNotFound = "BirNotFound", BirAmbiguous = "BirAmbiguous";

    /// <summary>Chains whose remittances show the invoices without VAT (user 2026-11-25: Puregold — chain "PGOLD" in
    /// soachain's chain Excel): a VAT-excluded gross is their normal, so it's a match, not a VatExcluded flag.</summary>
    public static bool ReportsWithoutVat(string? chain) => IsPuregold(chain);

    /// <summary>Puregold goes by "PGOLD" in soachain's chain Excel; custodians may type "Puregold" (user 2026-11-26: same chain).</summary>
    public static bool IsPuregold(string? chain)
    {
        var n = new string((chain ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        return n.StartsWith("PGOLD", StringComparison.Ordinal) || n.Contains("PUREGOLD", StringComparison.Ordinal);
    }

    /// <summary>The RA's chain vs the chain named in its Excel: same name (case-insensitive), or both Puregold ("Puregold" = "PGOLD").</summary>
    public static bool SameChain(string? a, string? b) =>
        string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase) || (IsPuregold(a) && IsPuregold(b));
}

/// <summary>
/// HOMS's BIR series of an invoice: the printed sales-invoice serial(s) BIRNOFR..BIRNOTO, read from
/// F:\HOMS\RAM\ARCHIVES\B103{MMYY}.DBF (the files DCSR's soachain fills the chain Excel's "BIR Series" from) by the
/// LegacyMasterWatcher "bir-series" job. A chain that lists invoices by BIR series (Puregold) is matched through it.
/// </summary>
public class InvoiceBirSeries
{
    public int Id { get; set; }
    /// <summary>The B1 file it came from, e.g. "B1030826".</summary>
    public string SourceFile { get; set; } = string.Empty;
    /// <summary>BCODE: the branch (the invoice no.'s first two digits).</summary>
    public int WhseNo { get; set; }
    public int DocId { get; set; }
    public int DocNo { get; set; }
    public DateOnly? DocDate { get; set; }
    public int BirFrom { get; set; }
    public int BirTo { get; set; }
}

public class RemittanceHistory
{
    public int Id { get; set; }
    public int RemittanceId { get; set; }
    public Remittance Remittance { get; set; } = null!;
    public string Action { get; set; } = string.Empty;
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public string? Reason { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string By { get; set; } = string.Empty;
}
