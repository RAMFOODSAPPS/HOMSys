namespace HOMSys.Domain.Entities;

/// <summary>A company bank account branches deposit into — synced from HOMS F:\HOMS\RAM\BANKS4W.DBF (the complete list).</summary>
public class BankAccount
{
    public int Id { get; set; }
    /// <summary>BDO, BPI, MBTC, PBCOM, CBC, SBC, LBP (Land Bank) — banks whose statements can be imported (other banks: their HOMS abbreviation).</summary>
    public string BankCode { get; set; } = string.Empty;
    /// <summary>The number HOMSys shows and checks statements against: BANKS4W.BANKAC, unless corrected in HOMSys
    /// (e.g. to the number on the bank's own statement). HomsBankAc keeps BANKS4W's for HOMS.</summary>
    public string AccountNo { get; set; } = string.Empty;
    /// <summary>BANKS4W.BANKAC as last synced — what HOMS writes in OD/GD DEPBANK. When AccountNo differs, it was
    /// corrected in HOMSys.</summary>
    public string? HomsBankAc { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>HOMS cash-in-bank GL account (BANKS4W.ACCTNO, e.g. 10212 BDO) — debited on the OR / JV HOMSys posts.</summary>
    public int? HomsAcctNo { get; set; }
    /// <summary>BANKS4W.BANKABBR ("CHINABANK", "SBTC") — HOMS RECBANK.</summary>
    public string? BankAbbr { get; set; }
    /// <summary>BANKS4W.CURRENT when it differs from BANKAC (BDO 5308004094).</summary>
    public string? AltAccountNo { get; set; }
    /// <summary>Came from the BANKS4W sync (removed 2026-10-16: accounts are kept in HOMSys; every field is editable).</summary>
    public bool Synced { get; set; }
    /// <summary>Deleted in HOMSys: hidden everywhere (the row keeps its GL and number). Only an account with
    /// no deposits and no statement uploads can be deleted (user 2026-10-16).</summary>
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Branches that deposit into this account (their custodians see it in the picklist).</summary>
    public ICollection<BankAccountSite> Sites { get; set; } = [];

    /// <summary>"BDO 0012-3456-78" — what a deposit slip shows.</summary>
    public string Label => $"{BankCode} {AccountNo}";
}

public class BankAccountSite
{
    public int BankAccountId { get; set; }
    public BankAccount BankAccount { get; set; } = null!;
    public int SiteId { get; set; }
    public Site Site { get; set; } = null!;
}

/// <summary>One uploaded statement file (bank portal Excel / CSV export) for one account.</summary>
public class BankStatementUpload
{
    public int Id { get; set; }
    public int BankAccountId { get; set; }
    public BankAccount BankAccount { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public string BlobPath { get; set; } = string.Empty;
    /// <summary>Latest transaction date in the file (the "as of" of the statement).</summary>
    public DateOnly? AsOfDate { get; set; }
    public int LineCount { get; set; }
    public int NewLineCount { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string UploadedBy { get; set; } = string.Empty;
}

/// <summary>
/// One transaction line. Re-downloads overlap ("Current Day" several times a day), so a line is
/// stored once per account by Fingerprint. Credits are matched to deposit slips (DepositBatch).
/// </summary>
public class BankStatementLine
{
    public int Id { get; set; }
    public int BankAccountId { get; set; }
    public BankAccount BankAccount { get; set; } = null!;
    public int UploadId { get; set; }
    public BankStatementUpload Upload { get; set; } = null!;

    public DateOnly TxnDate { get; set; }
    public DateOnly? ValueDate { get; set; }
    public string? TxnTime { get; set; }
    public string Description { get; set; } = string.Empty;
    /// <summary>The bank's own references (reference / sequence no., narrative) joined.</summary>
    public string? Reference { get; set; }
    public string? BankBranch { get; set; }
    public string? CheckNo { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal? Balance { get; set; }
    public string Fingerprint { get; set; } = string.Empty;

    public int? MatchedDepositBatchId { get; set; }
    public DepositBatch? MatchedDepositBatch { get; set; }
    /// <summary>Reference (slip ref found in the line), Amount (only candidate), Manual.</summary>
    public string? MatchMethod { get; set; }
    public DateTime? MatchedAt { get; set; }
    public string? MatchedBy { get; set; }
}
