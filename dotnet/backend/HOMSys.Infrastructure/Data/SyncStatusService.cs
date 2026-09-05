using System.Text.Json;
using HOMSys.Application.DTOs.Monitoring;
using HOMSys.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Data;

/// <summary>
/// Reads the pricing-import marker file off disk for the Master Data →
/// Legacy Monitoring page, and exposes a manual "Sync Now" trigger.
/// Lives in Infrastructure (not Application) since it depends directly on
/// PricingDataImporter.
/// </summary>
public class SyncStatusService(AppDbContext db, PricingDataImporter pricingImporter)
{
    // A page refresh resets the frontend's in-flight "syncing" signal even while a
    // previous trigger is still running server-side, so a re-click can otherwise start
    // a second concurrent truncate+reload that races the first and crashes with a
    // duplicate-key DB error. Gate the importer to one run at a time.
    private static int _pricingSyncRunning;

    public async Task<SyncStatusDto> GetStatusAsync()
    {
        return new SyncStatusDto(await GetPricingStatusAsync());
    }

    private async Task<PricingSyncStatusDto> GetPricingStatusAsync()
    {
        // F:\ (or wherever PricingDataImporter.DefaultRoot points) is only reachable from an
        // on-prem machine — Azure App Service can't see it, so "Sync Now" would always 500
        // there. The frontend hides the button when this is false.
        var canTriggerSync = Directory.Exists(PricingDataImporter.DefaultRoot);

        // The file marker is only ever written by the CLI full-import path (ImportAllCoreAsync),
        // never by the watcher's real delta-apply path (PricingDeltaImporter), which is the one
        // that actually runs every 5 minutes in production. SyncLog is written by both, so it's
        // the source of truth for "last synced" — the marker is kept only for the file/branch
        // list this page displays, which the delta path has no reason to track locally.
        var dbLastSynced = (await db.SyncLogs.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Section == SyncLogSections.PricingMasters))?.LastSyncedUtc;

        var path = PricingDataImporter.MarkerPath;
        if (!File.Exists(path))
            return new PricingSyncStatusDto(dbLastSynced, [], [], [], canTriggerSync);

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;

            // The marker keys are per-branch DBF file paths (F:\...), which is filesystem
            // detail nobody looking at this page needs — group them by the SQL table each
            // one feeds and show only that table's own last-updated time.
            var tables = root.TryGetProperty("FileMtimes", out var mtimes)
                ? mtimes.EnumerateObject()
                    .GroupBy(p => GetTableName(p.Name))
                    .Select(g => new PricingTableStatusDto(g.Key, g.Max(p => p.Value.GetDateTime())))
                    .OrderBy(t => t.Table)
                    .ToList()
                : [];

            var addonBranches = ReadStringList(root, "AddonBranches");
            var customerBranches = ReadStringList(root, "CustomerBranches");

            var fileLastRunUtc = File.GetLastWriteTimeUtc(path);
            var lastRunUtc = dbLastSynced is { } d && d > fileLastRunUtc ? d : fileLastRunUtc;

            return new PricingSyncStatusDto(lastRunUtc, addonBranches, customerBranches, tables, canTriggerSync);
        }
        catch (JsonException)
        {
            // A corrupt/partial marker (e.g. an interrupted write) shouldn't 500 the
            // whole monitoring page.
            return new PricingSyncStatusDto(dbLastSynced, [], [], [], canTriggerSync);
        }
    }

    private static List<string> ReadStringList(JsonElement root, string property) =>
        root.TryGetProperty(property, out var arr)
            ? arr.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
            : [];

    private static string GetTableName(string filePath) => Path.GetFileName(filePath).ToUpperInvariant() switch
    {
        "PROD4WIN.DBF" => "Products",
        "PRCHST.DBF" => "PriceHistory",
        "PRODCAT.DBF" => "ProductCategories",
        "PRLISTX2.DBF" => "PriceListRestrictions",
        "PRLISTX.DBF" => "PriceListExclusions",
        "ZONE.DBF" => "ZoneAddOns",
        "ZONE2.DBF" => "Zone2AddOns",
        "CUST4WIN.DBF" => "CustomerZones",
        var other => other,
    };

    public async Task<PricingImportResult> TriggerPricingSyncAsync(Action<string>? log = null, string? path = null)
    {
        if (Interlocked.CompareExchange(ref _pricingSyncRunning, 1, 0) != 0)
            throw new SyncInProgressException("A pricing masters sync is already in progress. Please wait for it to finish.");

        try
        {
            var root = string.IsNullOrWhiteSpace(path) ? PricingDataImporter.DefaultRoot : path;
            return await pricingImporter.ImportAllAsync(root, log);
        }
        finally
        {
            Interlocked.Exchange(ref _pricingSyncRunning, 0);
        }
    }
}

public class SyncInProgressException(string message) : Exception(message);
