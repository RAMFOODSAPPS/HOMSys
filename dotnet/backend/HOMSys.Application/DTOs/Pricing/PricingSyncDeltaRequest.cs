namespace HOMSys.Application.DTOs.Pricing;

/// <summary>
/// Body of POST /api/masters/sync — LegacyMasterWatcher.exe parses F:\ DBFs
/// itself and diffs against its own last-synced snapshot, so this carries
/// only rows that actually changed since the previous run. Every section is
/// optional and omitted when empty; an empty body means nothing changed
/// anywhere.
/// </summary>
public record PricingSyncDeltaRequest(
    List<ProductPriceDelta>? ProductPrices,
    ProductCategoryDeltaSection? ProductCategories,
    PrlistX2RestrictionDeltaSection? PrlistX2Restrictions,
    PrlistXRestrictionDeltaSection? PrlistXRestrictions,
    PriceHistoryDeltaSection? PriceHistory,
    Dictionary<string, BranchDelta>? Branches);

/// <summary>Product pricing fields, matched by ProdNo — upsert only, never deleted.</summary>
public record ProductPriceDelta(
    int ProdNo,
    decimal? NewPrice,
    DateOnly? PriceFrom,
    decimal? OldPrice1,
    decimal? Srp,
    string? Category,
    string? Barcode,
    string? CaseBarcode);

public record ProductCategoryDelta(string CategoryCode, int GroupNo, string? GroupDesc, string? SubCat, int SeqNo);

public record ProductCategoryDeltaSection(List<ProductCategoryDelta> Upserts, List<string> Deletes);

public record PrlistX2RestrictionKey(string CProdNo, string Zone);

public record PrlistX2RestrictionDeltaSection(List<PrlistX2RestrictionKey> Adds, List<PrlistX2RestrictionKey> Removes);

public record PrlistXRestrictionDeltaSection(List<string> Adds, List<string> Removes);

public record PriceHistoryDelta(int RecNo, int ProdNo, DateOnly? Effective, decimal NpAfVat);

public record PriceHistoryDeltaSection(List<PriceHistoryDelta> Upserts, List<int> Deletes);

public record ZoneAddOnDelta(int RecNo, string CProdNo, string CZone, DateOnly? EffDate, decimal AddOn, decimal Rate, decimal FixAmt);

public record ZoneAddOnDeltaSection(List<ZoneAddOnDelta> Upserts, List<int> Deletes);

public record Zone2AddOnDelta(int RecNo, string CustKey, string CProdNo, DateOnly? EffDate, decimal AddOn, decimal Rate, decimal FixAmt);

public record Zone2AddOnDeltaSection(List<Zone2AddOnDelta> Upserts, List<int> Deletes);

public record CustomerBranchZoneDelta(int RecNo, string CustKey, string CZone);

public record CustomerBranchZoneDeltaSection(List<CustomerBranchZoneDelta> Upserts, List<int> Deletes);

/// <summary>Per-branch section, keyed by the ADDON/CUSTOMER folder name (e.g. "hon").</summary>
public record BranchDelta(
    ZoneAddOnDeltaSection? ZoneAddOns,
    Zone2AddOnDeltaSection? Zone2AddOns,
    CustomerBranchZoneDeltaSection? CustomerBranchZones);
