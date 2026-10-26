namespace HOMSys.Application.DTOs.Collections;

/// <summary>Balance = running balance after the latest statement line (null when the bank's export has no balance column);
/// UnmatchedCount = credits not matched to a deposit slip;
/// HomsAcctNo = HOMS cash-in-bank GL; Synced = came from the old BANKS4W sync (removed 2026-10-16; every field is editable now).</summary>
public record BankAccountDto(int Id, string BankCode, string AccountNo, string AccountName, string Label, bool IsActive,
    List<int> SiteIds, DateOnly? AsOfDate, DateTime? LastUploadAt, decimal? Balance, int LineCount, int UnmatchedCount,
    int? HomsAcctNo = null, string? BankAbbr = null, bool Synced = false, string? HomsBankAc = null, bool CanDelete = false);

/// <summary>HomsAcctNo = the HOMS cash-in-bank GL (required: deposits and HOMS ORs need it); BankAbbr = HOMS RECBANK (defaults to the bank code).</summary>
public record SaveBankAccountDto(string BankCode, string AccountNo, string AccountName, bool IsActive, List<int>? SiteIds,
    int? HomsAcctNo = null, string? BankAbbr = null);

/// <summary>Picklist entry for a custodian's deposit.</summary>
public record BankAccountOptionDto(int Id, string Label, string AccountName);

public record BankStatementUploadDto(int Id, int BankAccountId, string AccountLabel, string FileName, DateOnly? AsOfDate,
    int LineCount, int NewLineCount, DateTime UploadedAt, string UploadedBy);

public record BankUploadResultDto(BankStatementUploadDto Upload, int Credits, int Matched);

/// <summary>
/// What an import would bring in, read from the file without saving, for the user to confirm before importing.
/// Opening/Closing come from the bank's own running balances (Order = which way the export lists them);
/// BalancesAddUp = every running balance equals the previous one − debit + credit. CurrentBalance = the account's
/// balance in HOMSys before this file. All figures cover the whole file (NewLineCount = lines not imported before).
/// </summary>
public record BankUploadPreviewDto(string AccountLabel, string FileName, DateOnly FromDate, DateOnly ToDate,
    int LineCount, int NewLineCount, int CreditCount, decimal Credits, int DebitCount, decimal Debits,
    bool HasBalance, bool BalancesAddUp, string? Order, decimal? Opening, decimal? Closing, decimal? CurrentBalance);

/// <summary>MatchConfirmed = false on a matched line: an automatic match still waiting for the Finance Verifier.</summary>
public record BankStatementLineDto(int Id, int BankAccountId, string AccountLabel, DateOnly TxnDate, DateOnly? ValueDate, string? TxnTime,
    string Description, string? Reference, string? BankBranch, string? CheckNo, decimal Debit, decimal Credit, decimal? Balance,
    int? MatchedDepositBatchId, string? MatchedSlip, string? MatchedSiteCode, string? MatchMethod, DateTime? MatchedAt, string? MatchedBy,
    bool MatchConfirmed);

/// <summary>A deposit slip that could be the line (manual matching).</summary>
public record DepositCandidateDto(int Id, string SiteCode, string Bank, string SlipRefNo, DateOnly DepositDate, decimal TotalAmount,
    int CollectionCount, bool AmountMatches);

public record BankMatchDto(int DepositBatchId);

/// <summary>"Bank statement as of" — the latest statement date over all accounts, and per account.</summary>
public record BankAsOfDto(DateOnly? AsOfDate, DateTime? LastUploadAt, List<BankAccountDto> Accounts);
