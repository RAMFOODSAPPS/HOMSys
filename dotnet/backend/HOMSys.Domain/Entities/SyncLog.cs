namespace HOMSys.Domain.Entities;

/// <summary>
/// One row per legacy-monitoring section, updated by every code path that
/// actually applies a sync (CLI full-import and the watcher's delta-apply
/// alike) so "last synced" is visible from any host — unlike a
/// %ProgramData% file marker, which only the writing host can see.
/// </summary>
public class SyncLog
{
    public int Id { get; set; }
    public string Section { get; set; } = string.Empty;
    public DateTime LastSyncedUtc { get; set; }
}

public static class SyncLogSections
{
    public const string PricingMasters = "PricingMasters";

    /// <summary>Per-branch SalesOrderBridge heartbeat — written on every bridge
    /// run that reaches HOMSys, so HO can tell "branch offline" from "no activity".</summary>
    public const string SoBridgePrefix = "SoBridge:";
    public static string SoBridge(string branch) => SoBridgePrefix + branch;
}
