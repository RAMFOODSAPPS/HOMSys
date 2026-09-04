using System.Text.Json;
using HOMSys.Application.DTOs.Monitoring;

namespace HOMSys.Infrastructure.Data;

/// <summary>
/// Reads both legacy-importer marker files off disk for the Master Data →
/// Legacy Monitoring page, and exposes manual "Sync Now" triggers for each.
/// Lives in Infrastructure (not Application) since it depends directly on
/// ReferenceDataImporter/PricingDataImporter.
/// </summary>
public class SyncStatusService(ReferenceDataImporter referenceImporter, PricingDataImporter pricingImporter)
{
    // A page refresh resets the frontend's in-flight "syncing" signal even while a
    // previous trigger is still running server-side, so a re-click can otherwise start
    // a second concurrent truncate+reload that races the first and crashes with a
    // duplicate-key DB error. Gate each importer to one run at a time.
    private static int _referenceSyncRunning;
    private static int _pricingSyncRunning;

    public async Task<SyncStatusDto> GetStatusAsync()
    {
        return new SyncStatusDto(GetReferenceStatus(), await GetPricingStatusAsync());
    }

    private static ReferenceSyncStatusDto GetReferenceStatus()
    {
        var marker = ReferenceDataImporter.LoadMarker();
        return new ReferenceSyncStatusDto(marker?.LastRunUtc, marker?.Customers ?? 0, marker?.Products ?? 0);
    }

    private static Task<PricingSyncStatusDto> GetPricingStatusAsync()
    {
        var path = PricingDataImporter.MarkerPath;
        if (!File.Exists(path))
            return Task.FromResult(new PricingSyncStatusDto(null, [], [], []));

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        var files = root.TryGetProperty("FileMtimes", out var mtimes)
            ? mtimes.EnumerateObject()
                .Select(p => new PricingFileStatusDto(p.Name, p.Value.GetDateTime()))
                .OrderBy(f => f.File)
                .ToList()
            : [];

        var addonBranches = ReadStringList(root, "AddonBranches");
        var customerBranches = ReadStringList(root, "CustomerBranches");

        return Task.FromResult(new PricingSyncStatusDto(File.GetLastWriteTimeUtc(path), addonBranches, customerBranches, files));
    }

    private static List<string> ReadStringList(JsonElement root, string property) =>
        root.TryGetProperty(property, out var arr)
            ? arr.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
            : [];

    public async Task<ImportResult> TriggerReferenceSyncAsync(Action<string>? log = null)
    {
        if (Interlocked.CompareExchange(ref _referenceSyncRunning, 1, 0) != 0)
            throw new SyncInProgressException("A reference data sync is already in progress. Please wait for it to finish.");

        try
        {
            return await referenceImporter.ImportAllAsync(ReferenceDataImporter.DefaultDbfPath, log);
        }
        finally
        {
            Interlocked.Exchange(ref _referenceSyncRunning, 0);
        }
    }

    public async Task<PricingImportResult> TriggerPricingSyncAsync(Action<string>? log = null)
    {
        if (Interlocked.CompareExchange(ref _pricingSyncRunning, 1, 0) != 0)
            throw new SyncInProgressException("A pricing masters sync is already in progress. Please wait for it to finish.");

        try
        {
            return await pricingImporter.ImportAllAsync(PricingDataImporter.DefaultRoot, log);
        }
        finally
        {
            Interlocked.Exchange(ref _pricingSyncRunning, 0);
        }
    }
}

public class SyncInProgressException(string message) : Exception(message);
