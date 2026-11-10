namespace HOMSys.Application.DTOs.Rdp;

// ── RDP list / channel mapping ──────────────────────────────────────────────

public record RdpDistributorDto(int Id, int CustKey, string Name, string Rdm, string Area, string Territory,
    int? SiteId, string? SiteName, bool IsActive, int LineCount, string? FirstPeriod, string? LastPeriod, string FileNames);

public record SaveRdpDistributorDto(int CustKey, string Name, string Rdm, string Area, string Territory, int? SiteId, bool IsActive,
    string? FileNames);

public record RdpChannelMapDto(int Id, string Label, string Channel, int LineCount);

/// <summary>A label found in uploaded lines that has no mapping yet.</summary>
public record RdpUnmappedLabelDto(string Label, int LineCount, int DistributorCount);

public record RdpChannelsDto(List<string> Channels, List<RdpChannelMapDto> Maps, List<RdpUnmappedLabelDto> Unmapped);

public record SaveRdpChannelMapDto(string Label, string Channel);

// ── uploads ─────────────────────────────────────────────────────────────────

public record RdpPreviewDistributorDto(int DistributorId, int CustKey, string Name, List<string> Periods,
    int Lines, decimal NetAmount, int Accounts, int ExistingLines);

public record RdpCodeCountDto(string Code, string Description, int Lines);

/// <summary>What a file would import. Errors block the import; warnings don't.</summary>
public record RdpUploadPreviewDto(
    string FileName, int Lines, int BlankRowsSkipped, decimal NetAmount, int Accounts,
    List<RdpPreviewDistributorDto> Distributors,
    List<string> Errors, int ErrorCount,
    List<RdpCodeCountDto> UnknownSkus, List<RdpCodeCountDto> UnmappedChannels,
    int NoChannelLines, int NoAccountCodeLines, int NoCasesLines, int ReplacesLines, List<string> Notes);

public record RdpUploadDto(int Id, string FileName, string Distributors, string Periods, int LineCount,
    decimal NetAmount, int ReplacedLines, int CurrentLines, DateTime UploadedAt, string UploadedBy);

/// <summary>One imported line in the template's column order (RDP CUSTKEY … NET AMOUNT).</summary>
public record RdpLineDto(long Id, int CustKey, string DistributorName, DateOnly Period, DateOnly? InvoiceDate, string InvoiceNo,
    string TxnType, string AccountCode, string AccountName, string ChannelLabel, string Province, string City, string Salesman,
    string SkuCode, string ItemDesc, decimal Quantity, string Uom, decimal NetAmount);

/// <summary>A page of lines plus the totals of everything the filter matches.</summary>
public record RdpLinesDto(int Total, decimal Quantity, decimal NetAmount, List<RdpLineDto> Rows);

// ── UBA dashboard ───────────────────────────────────────────────────────────

/// <summary>
/// Scope of every dashboard query. Periods are "yyyy-MM"; metric is accounts | sales | cases. Channels = channel-type
/// names as the dashboard shows them (an account's channel), e.g. from clicking a row of the channel table.
/// </summary>
public record UbaFilterDto(string? From, string? To, List<string>? Areas, List<string>? Rdms,
    List<int>? DistributorIds, string? Metric, List<string>? Channels = null);

public record UbaSplitRequestDto(UbaFilterDto Filter, List<string> GroupBy);

public record UbaOptionDto(string Value, string Label);

public record UbaFiltersDto(List<string> Areas, List<string> Rdms, List<RdpDistributorDto> Distributors,
    List<string> Periods, List<UbaOptionDto> Dimensions);

public record UbaKpiDto(decimal Value, decimal? Prior, decimal? ChangePct);

public record UbaSummaryDto(
    string From, string To, int Distributors, int LikeForLikeDistributors,
    UbaKpiDto BuyingAccounts, UbaKpiDto NetSales, UbaKpiDto Cases,
    decimal AvgAccountsPerMonth, decimal AvgLinesPerAccount, int SkusSold,
    List<UbaTrendPointDto> Trend, List<UbaSplitRowDto> ByArea);

public record UbaTrendPointDto(string Period, decimal Value, bool InRange);

/// <summary>
/// One node of a split. Keys has one entry per level down to this node (the total row has none);
/// Months is aligned with UbaSplitDto.Periods. ChangePct compares like-for-like distributors with the prior period.
/// </summary>
public record UbaSplitRowDto(int Level, List<string> Keys, string Label, string? Area, string? Rdm,
    List<decimal?> Months, decimal Range, decimal Share, decimal? ChangePct);

public record UbaSplitDto(string From, string To, string Metric, List<string> GroupBy, List<string> Periods,
    List<UbaSplitRowDto> Rows, UbaSplitRowDto Total, string? Note);
