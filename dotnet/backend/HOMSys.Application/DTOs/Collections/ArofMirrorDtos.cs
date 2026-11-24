using System.Text.Json;

namespace HOMSys.Application.DTOs.Collections;

// Incremental AROF4HO.DBF -> HomsArofRows sync (LegacyMasterWatcher "arof-mirror", 2026-11-13). The watcher reads new
// records at the end of the file every pass and compares every 1,000-record block's fingerprint at set times; only
// blocks whose fingerprint moved are sent, whole, and replace the mirror's rows in their RecNo span.

/// <summary>What HOMSys holds (GET api/collections/sync/arof-mirror): the file header last applied + every block's fingerprint.</summary>
public record ArofMirrorStateDto(
    bool Loaded, int RecordCount, int RecordLength, int HeaderLength, int BlockSize, string FieldList,
    long FileSize, DateTime? FileWrittenAt, DateTime? FullScanAt, List<ArofBlockStateDto> Blocks);

public record ArofBlockStateDto(int BlockNo, int RecordCount, string Hash);

/// <summary>The DBF header the watcher read; written to HomsArofFiles with a push's last part.</summary>
public record ArofFileHeaderDto(
    string Path, int RecordCount, int RecordLength, int HeaderLength, int BlockSize, string FieldList,
    long FileSize, DateTime? FileWrittenAt);

/// <summary>One block: its fingerprint and all its records, each a value array in <see cref="ArofMirrorPush.Columns"/> order.</summary>
public record ArofBlockPush(int BlockNo, int FirstRecNo, string Hash, List<JsonElement[]> Rows);

/// <summary>
/// One part of a push. Each block is compared row by row with the mirror's rows in its RecNo span and only the rows
/// that differ are written; then the invoices those rows name are rebuilt. The last part (<see cref="Final"/>) also writes the file header and drops rows past File.RecordCount (the
/// purge shrank the file); with <see cref="FullScan"/> it stamps FullScanAt and re-checks every AROF invoice still has
/// its rows (removing ones that are gone, unless a collection uses them).
/// </summary>
/// <param name="DeferInvoices">A big push (purge / rewrite: rows shift, so a row can sit at two RecNos until the
/// push ends): the invoices are not rebuilt part by part; the watcher runs the paged rebuild once the push is whole.</param>
public record ArofMirrorPush(List<string> Columns, List<ArofBlockPush>? Blocks, ArofFileHeaderDto File, bool Final, bool FullScan,
    bool DeferInvoices = false);

/// <summary>
/// Rows are compared one by one with what the mirror holds: only rows that differ are written (updated / inserted /
/// deleted); <see cref="RowsUnchanged"/> were left alone. <see cref="BlocksReplaced"/> = blocks where most rows
/// differed (a rewrite, e.g. the purge shifted them), which are replaced whole.
/// </summary>
public record ArofMirrorPushResult(
    int Blocks, int RowsUpdated, int RowsInserted, int RowsDeleted, int RowsUnchanged, int BlocksReplaced, int InvoiceNos,
    ArofSyncResult? Invoices, bool RebuildNeeded = false)
{
    /// <summary>Marker the watcher looks for in the result text: run the paged invoice rebuild.</summary>
    public const string RebuildMarker = "REBUILD-INVOICES";

    public override string ToString() =>
        $"blocks {Blocks}: rows ~{RowsUpdated} +{RowsInserted} -{RowsDeleted} ={RowsUnchanged}"
        + (BlocksReplaced > 0 ? $" ({BlocksReplaced} replaced whole)" : "") + $"; invoice nos {InvoiceNos}"
        + (Invoices is null ? "" : $" -> {Invoices}") + (RebuildNeeded ? $"; {RebuildMarker}" : "");
}

/// <summary>One page of the invoice rebuild: the next <see cref="Take"/> invoice numbers above <see cref="AfterNo"/>.</summary>
public record ArofRebuildRequest(int AfterNo, int Take);

public record ArofRebuildResult(int LastNo, bool Done, int Numbers, string Invoices);
