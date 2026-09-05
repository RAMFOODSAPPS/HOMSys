namespace HOMSys.Application.DTOs.Monitoring;

public record PricingTableStatusDto(string Table, DateTime LastUpdatedUtc);

public record PricingSyncStatusDto(
    DateTime? LastRunUtc,
    List<string> AddonBranches,
    List<string> CustomerBranches,
    List<PricingTableStatusDto> Tables,
    bool CanTriggerSync);

public record SyncStatusDto(PricingSyncStatusDto PricingMasters);

/// <summary>Optional body for the headless sync endpoints, letting the watcher override the server's hardcoded default DBF root/share path.</summary>
public record SyncOverrideRequest(string? Path);
