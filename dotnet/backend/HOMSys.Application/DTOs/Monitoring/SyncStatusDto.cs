namespace HOMSys.Application.DTOs.Monitoring;

public record ReferenceSyncStatusDto(DateTime? LastRunUtc, int Customers, int Products);

public record PricingFileStatusDto(string File, DateTime LastWriteUtc);

public record PricingSyncStatusDto(
    DateTime? LastRunUtc,
    List<string> AddonBranches,
    List<string> CustomerBranches,
    List<PricingFileStatusDto> Files);

public record SyncStatusDto(ReferenceSyncStatusDto ReferenceData, PricingSyncStatusDto PricingMasters);
