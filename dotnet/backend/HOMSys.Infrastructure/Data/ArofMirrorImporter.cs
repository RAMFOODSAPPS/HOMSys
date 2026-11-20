using System.Data;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using HOMSys.Application.DTOs.Collections;
using HOMSys.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HOMSys.Infrastructure.Data;

/// <summary>
/// Keeps the AROF4HO.DBF mirror (HomsArofRows / HomsArofBlocks / HomsArofFiles) current from LegacyMasterWatcher's
/// "arof-mirror" pushes, then rebuilds PostedInvoices / PostedRfcs for the invoice numbers the changed rows name, with
/// <see cref="ArofInvoiceSyncImporter"/>'s rules (user 2026-11-13: load AROF4HO whole once, then only new and changed
/// rows; "no bulk update, only the data that needs update"). A pushed block is compared row by row with the mirror's
/// rows in its RecNo span and only rows that differ are written; a block where most rows differ (new records, or the
/// yearly purge + PACK shifting RecNos) is replaced whole. The last part of a push writes the file header and drops
/// rows past the file's record count.
/// </summary>
public class ArofMirrorImporter(AppDbContext db, ArofInvoiceSyncImporter invoices)
{
    /// <summary>Value order of a pushed row — the watcher's arof_mirror.COLUMNS.</summary>
    public static readonly string[] Columns =
    [
        "RecNo", "Deleted", "AdjId", "PaymentId", "TrDate", "WhseNo", "EmpNo", "CSman", "Salesman", "TCode", "AcctNo",
        "CKey", "CustName", "Term", "DocId", "DocDate", "DocType", "DocNo", "RefId", "RefNo", "RefDate", "RefType",
        "DcrNo", "DcrDate", "BadCqf", "BadChq", "DrAmt", "CrAmt", "Bank", "Location", "CheckNo", "CheckDate", "Remarks",
        "RefId2", "RefNo2", "RefDate2", "RefType2", "TrType", "DssNo", "Reference", "DusCKey", "DCusName", "UserName",
        "DocBal", "BatchNo", "Transfer", "SplitNo", "OrigAmt", "SysDate", "Process", "Expln1", "Expln2", "Expln3",
        "BCode", "SName", "ChkRcvd", "CustKey", "Sman4Soa", "PayGroup1", "PayGroup2", "PurgDate", "HoPosted", "RsrNo",
        "RsrDate", "CrsrNo", "CrsrDate", "ArofId", "PoNum", "PoDate",
    ];

    private static readonly PropertyInfo[] Props = Columns.Select(c =>
        typeof(HomsArofRow).GetProperty(c, BindingFlags.Public | BindingFlags.Instance)
        ?? throw new InvalidOperationException($"HomsArofRow has no {c}")).ToArray();

    private const int GroupChunk = 2000;
    /// <summary>Most gone invoice numbers a push's last part settles itself; more go to the paged rebuild.</summary>
    private const int SettleInlineMax = 5000;

    public async Task<ArofMirrorStateDto> GetStateAsync()
    {
        var f = await db.HomsArofFiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == HomsArofFile.SingletonId);
        var blocks = await db.HomsArofBlocks.AsNoTracking().OrderBy(b => b.BlockNo)
            .Select(b => new ArofBlockStateDto(b.BlockNo, b.RecordCount, b.Hash)).ToListAsync();
        return f is null
            ? new ArofMirrorStateDto(false, 0, 0, 0, HomsArofFile.DefaultBlockSize, "", 0, null, null, blocks)
            : new ArofMirrorStateDto(true, f.RecordCount, f.RecordLength, f.HeaderLength, f.BlockSize, f.FieldList,
                f.FileSize, f.FileWrittenAt, f.FullScanAt, blocks);
    }

    public async Task<(ArofMirrorPushResult? Result, string? Error)> ApplyAsync(ArofMirrorPush req)
    {
        if (req.Columns is null || !req.Columns.SequenceEqual(Columns))
            return (null, "The row columns don't match this server's AROF4HO mirror (update LegacyMasterWatcher or the API).");
        var bs = req.File.BlockSize > 0 ? req.File.BlockSize : HomsArofFile.DefaultBlockSize;
        var blocks = req.Blocks ?? [];
        foreach (var b in blocks)
        {
            if (b.Rows is null || b.FirstRecNo != b.BlockNo * bs + 1 || b.Rows.Count == 0 || b.Rows.Count > bs
                || string.IsNullOrEmpty(b.Hash))
                return (null, $"Block {b.BlockNo} is malformed.");
            for (var i = 0; i < b.Rows.Count; i++)
                if (b.Rows[i] is not { } row || row.Length != Columns.Length || row[0].ValueKind != JsonValueKind.Number
                    || !row[0].TryGetInt32(out var recNo) || recNo != b.FirstRecNo + i)
                    return (null, $"Block {b.BlockNo} row {i} is malformed.");
        }
        if (req.Final && req.File.RecordCount <= 0) return (null, "Refusing an empty AROF4HO (record count 0).");

        var touched = new HashSet<int>();
        int updated = 0, inserted = 0, deleted = 0, unchanged = 0, replaced = 0;
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            var whole = new List<HomsArofRow>();
            foreach (var b in blocks)
            {
                int lo = b.FirstRecNo, hi = b.FirstRecNo + bs - 1;
                var old = await db.HomsArofRows.AsNoTracking().Where(r => r.RecNo >= lo && r.RecNo <= hi).ToDictionaryAsync(r => r.RecNo);
                var rows = b.Rows.Select(ToRow).ToList();
                var differ = rows.Where(n => !old.TryGetValue(n.RecNo, out var o) || !Same(o, n)).ToList();

                // Records the block no longer has (the file got shorter here).
                var end = lo + rows.Count;
                foreach (var o in old.Values.Where(o => o.RecNo >= end)) Touch(o, touched);
                deleted += await db.HomsArofRows.Where(r => r.RecNo >= end && r.RecNo <= hi).ExecuteDeleteAsync();

                if (differ.Count > rows.Count / 2)
                {
                    // Most of the block differs: new records, or a rewrite (the purge shifted them). Replace it whole.
                    foreach (var o in old.Values) Touch(o, touched);
                    foreach (var n in rows)
                    {
                        Touch(n, touched);
                        if (old.ContainsKey(n.RecNo)) updated++; else inserted++;
                    }
                    await db.HomsArofRows.Where(r => r.RecNo >= lo && r.RecNo < end).ExecuteDeleteAsync();
                    whole.AddRange(rows);
                    replaced++;
                    continue;
                }
                // Only the rows that differ are written; the rest of the block is left alone.
                unchanged += rows.Count - differ.Count;
                foreach (var n in differ)
                {
                    Touch(n, touched);
                    if (old.TryGetValue(n.RecNo, out var o))
                    {
                        Touch(o, touched);
                        db.HomsArofRows.Update(n);
                        updated++;
                    }
                    else
                    {
                        db.HomsArofRows.Add(n);
                        inserted++;
                    }
                }
            }
            await BulkInsertAsync(whole, tx);

            var nos = blocks.Select(b => b.BlockNo).ToList();
            var existing = await db.HomsArofBlocks.Where(x => nos.Contains(x.BlockNo)).ToDictionaryAsync(x => x.BlockNo);
            foreach (var b in blocks)
            {
                if (!existing.TryGetValue(b.BlockNo, out var e)) db.HomsArofBlocks.Add(e = new HomsArofBlock { BlockNo = b.BlockNo });
                e.FirstRecNo = b.FirstRecNo; e.RecordCount = b.Rows.Count; e.Hash = b.Hash; e.SyncedAt = DateTime.UtcNow;
            }

            if (req.Final)
            {
                var n = req.File.RecordCount;
                await CollectTouchedAsync(db.HomsArofRows.Where(r => r.RecNo > n), touched);
                deleted += await db.HomsArofRows.Where(r => r.RecNo > n).ExecuteDeleteAsync();
                var lastBlock = (n - 1) / bs;
                await db.HomsArofBlocks.Where(x => x.BlockNo > lastBlock).ExecuteDeleteAsync();

                var f = await db.HomsArofFiles.FirstOrDefaultAsync(x => x.Id == HomsArofFile.SingletonId);
                if (f is null) db.HomsArofFiles.Add(f = new HomsArofFile());
                f.Path = req.File.Path; f.RecordCount = n; f.RecordLength = req.File.RecordLength;
                f.HeaderLength = req.File.HeaderLength; f.BlockSize = bs; f.FieldList = req.File.FieldList;
                f.FileSize = req.File.FileSize; f.FileWrittenAt = req.File.FileWrittenAt; f.UpdatedAt = DateTime.UtcNow;
                if (req.FullScan) f.FullScanAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        db.ChangeTracker.Clear();

        // A big push defers the invoices to the paged rebuild (RebuildAsync): rows shift during a rewrite, so the
        // mirror can hold a row twice until the push is whole, and rebuilding part by part would double RFC totals.
        if (req.DeferInvoices)
            return (new ArofMirrorPushResult(blocks.Count, updated, inserted, deleted, unchanged, replaced, 0, null, RebuildNeeded: true), null);

        // Invoices named by the changed rows. A number with no rows left is settled by the full-scan check below.
        var groups = await GroupsFromMirrorAsync(touched, removeGone: false);
        var rebuildNeeded = false;
        if (req.Final && req.FullScan)
        {
            // Every AROF invoice whose DOCID 1 row is gone, and every number still holding AROF RFCs with no RFC row
            // left: rebuilt as RFCs-only, or removed. An invoice a collection / remittance uses is kept by the importer
            // (with its RFCs) even when HOMS dropped it, so those are left out here: otherwise they would come back on
            // every full compare, and after a purge could push every compare into the paged rebuild.
            var keptNos = db.PostedInvoices
                .Where(p => p.SourceFile == ArofInvoiceSyncImporter.Source
                            && !db.HomsArofRows.Any(r => r.DocId == 1 && !r.Deleted && r.DocNo == p.DocNo)
                            && (db.CollectionInvoices.Any(c => c.PostedInvoiceId == p.Id)
                                || db.CollectionDeductions.Any(c => c.PostedInvoiceId == p.Id)
                                || db.RemittanceLines.Any(l => l.PostedInvoiceId == p.Id)))
                .Select(p => p.DocNo);
            var noInvoiceRow = await db.PostedInvoices.AsNoTracking()
                .Where(p => p.SourceFile == ArofInvoiceSyncImporter.Source
                            && !db.HomsArofRows.Any(r => r.DocId == 1 && !r.Deleted && r.DocNo == p.DocNo)
                            && !keptNos.Contains(p.DocNo))
                .Select(p => p.DocNo).Distinct().ToListAsync();
            var orphanRfcs = await db.PostedRfcs.AsNoTracking()
                .Where(x => x.SourceFile == ArofInvoiceSyncImporter.Source
                            && !db.HomsArofRows.Any(r => r.DocId == 6 && r.RefId == 1 && !r.Deleted && r.RefNo == x.InvoiceNo && r.DocNo == x.RfcNo)
                            && !keptNos.Contains(x.InvoiceNo))
                .Select(x => x.InvoiceNo).Distinct().ToListAsync();
            HashSet<int> gone = [.. noInvoiceRow, .. orphanRfcs];
            if (gone.Count > SettleInlineMax) rebuildNeeded = true;   // too many for one request: the paged rebuild does it
            else
            {
                var settle = await GroupsFromMirrorAsync(gone, removeGone: true);
                groups = new ArofSyncRequest([.. groups.Groups!, .. settle.Groups!], settle.Removed);
            }
        }
        ArofSyncResult? result = null;
        if (groups.Groups!.Count > 0 || groups.Removed is { Count: > 0 }) result = await invoices.ApplyAsync(groups);

        return (new ArofMirrorPushResult(blocks.Count, updated, inserted, deleted, unchanged, replaced,
            groups.Groups!.Count + (groups.Removed?.Count ?? 0), result, rebuildNeeded), null);
    }

    /// <summary>
    /// One page of a whole rebuild of PostedInvoices / PostedRfcs from the mirror: the next <paramref name="take"/>
    /// invoice numbers above <paramref name="afterNo"/>, among every number the mirror or HOMSys's AROF rows name.
    /// Each is rebuilt from the mirror, or removed when the mirror no longer has it (kept when a collection uses it).
    /// The watcher pages through after a deferred push; idempotent, so it can be re-run at any time as a repair.
    /// </summary>
    public async Task<ArofRebuildResult> RebuildAsync(int afterNo, int take)
    {
        take = Math.Clamp(take, 100, 20_000);
        var a = await db.HomsArofRows.AsNoTracking().Where(r => r.DocId == 1 && !r.Deleted && r.DocNo > afterNo)
            .Select(r => r.DocNo).Distinct().OrderBy(x => x).Take(take).ToListAsync();
        var b = await db.HomsArofRows.AsNoTracking().Where(r => r.DocId == 6 && r.RefId == 1 && !r.Deleted && r.RefNo > afterNo)
            .Select(r => r.RefNo).Distinct().OrderBy(x => x).Take(take).ToListAsync();
        var c = await db.PostedInvoices.AsNoTracking().Where(p => p.SourceFile == ArofInvoiceSyncImporter.Source && p.DocNo > afterNo)
            .Select(p => p.DocNo).Distinct().OrderBy(x => x).Take(take).ToListAsync();
        var d = await db.PostedRfcs.AsNoTracking().Where(x => x.SourceFile == ArofInvoiceSyncImporter.Source && x.InvoiceNo > afterNo)
            .Select(x => x.InvoiceNo).Distinct().OrderBy(x => x).Take(take).ToListAsync();
        var nos = a.Concat(b).Concat(c).Concat(d).Distinct().OrderBy(x => x).Take(take).ToList();
        if (nos.Count == 0) return new ArofRebuildResult(afterNo, true, 0, "");
        var groups = await GroupsFromMirrorAsync([.. nos], removeGone: true);
        var result = await invoices.ApplyAsync(groups);
        return new ArofRebuildResult(nos[^1], nos.Count < take, nos.Count, result.ToString());
    }

    private static async Task CollectTouchedAsync(IQueryable<HomsArofRow> rows, HashSet<int> touched)
    {
        var refs = await rows.Where(r => r.DocId == 1 || (r.DocId == 6 && r.RefId == 1))
            .Select(r => r.DocId == 1 ? r.DocNo : r.RefNo).Distinct().ToListAsync();
        touched.UnionWith(refs);
    }

    private static void Touch(HomsArofRow r, HashSet<int> touched)
    {
        if (r.DocId == 1) touched.Add(r.DocNo);
        else if (r.DocId == 6 && r.RefId == 1) touched.Add(r.RefNo);
    }

    private static HomsArofRow ToRow(JsonElement[] values)
    {
        var row = new HomsArofRow();
        for (var i = 0; i < Props.Length; i++) Props[i].SetValue(row, Typed(values[i], Props[i].PropertyType));
        return row;
    }

    private static bool Same(HomsArofRow a, HomsArofRow b)
    {
        foreach (var p in Props)
            if (!Equals(p.GetValue(a), p.GetValue(b))) return false;
        return true;
    }

    private static object? Typed(JsonElement e, Type t)
    {
        if (t == typeof(string)) return e.ValueKind == JsonValueKind.String ? e.GetString()! : "";
        if (t == typeof(int)) return e.ValueKind == JsonValueKind.Number ? (e.TryGetInt32(out var i) ? i : (int)e.GetDouble()) : 0;
        if (t == typeof(long)) return e.ValueKind == JsonValueKind.Number ? (e.TryGetInt64(out var l) ? l : (long)e.GetDouble()) : 0L;
        if (t == typeof(decimal)) return e.ValueKind == JsonValueKind.Number ? Math.Round(e.GetDecimal(), 2) : 0m;
        if (t == typeof(bool)) return e.ValueKind == JsonValueKind.True;
        if (t == typeof(DateOnly?))
            return e.ValueKind == JsonValueKind.String
                   && DateOnly.TryParseExact(e.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d : (DateOnly?)null;
        throw new InvalidOperationException($"Unhandled column type {t}");
    }

    private async Task BulkInsertAsync(List<HomsArofRow> rows, IDbContextTransaction tx)
    {
        if (rows.Count == 0) return;
        var table = new DataTable();
        foreach (var p in Props)
            table.Columns.Add(p.Name, p.PropertyType == typeof(DateOnly?) ? typeof(DateTime) : p.PropertyType);
        foreach (var row in rows)
        {
            var values = new object[Props.Length];
            for (var i = 0; i < Props.Length; i++)
                values[i] = Props[i].GetValue(row) switch
                {
                    DateOnly d => d.ToDateTime(TimeOnly.MinValue),
                    null => DBNull.Value,
                    var v => v,
                };
            table.Rows.Add(values);
        }
        var conn = (SqlConnection)db.Database.GetDbConnection();
        using var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.CheckConstraints, (SqlTransaction)tx.GetDbTransaction())
        {
            DestinationTableName = "HomsArofRows", BatchSize = 5000, BulkCopyTimeout = 600,
        };
        foreach (DataColumn c in table.Columns) bulk.ColumnMappings.Add(c.ColumnName, c.ColumnName);
        await bulk.WriteToServerAsync(table);
    }

    /// <summary>
    /// The importer's group for each number, read from the mirror: the invoice = its DOCID 1 row (the latest date wins,
    /// ties the first row, as arof_sync did); RFCs = DOCID 6 / REFID 1 rows, summed per RFC no., date = the first
    /// non-blank in file order. A number with no rows is skipped, or listed as removed when <paramref name="removeGone"/>.
    /// </summary>
    private async Task<ArofSyncRequest> GroupsFromMirrorAsync(HashSet<int> nos, bool removeGone)
    {
        var groups = new List<ArofInvoiceGroup>();
        var removed = new List<int>();
        var floor = new DateOnly(1900, 1, 1);
        foreach (var chunk in nos.Chunk(GroupChunk))
        {
            var inv = await db.HomsArofRows.AsNoTracking()
                .Where(r => r.DocId == 1 && !r.Deleted && chunk.Contains(r.DocNo))
                .Select(r => new { r.RecNo, r.DocNo, r.DocDate, r.TrDate, r.CustKey, r.CKey, r.CustName, r.Term, r.PoNum,
                                   r.DrAmt, r.CrAmt, r.Salesman, r.EmpNo, r.WhseNo, r.DocBal })
                .ToListAsync();
            var rfc = await db.HomsArofRows.AsNoTracking()
                .Where(r => r.DocId == 6 && r.RefId == 1 && !r.Deleted && chunk.Contains(r.RefNo))
                .Select(r => new { r.RecNo, r.RefNo, r.DocNo, r.DocDate, r.TrDate, r.DrAmt, r.CrAmt })
                .ToListAsync();
            var invByNo = inv.ToLookup(r => r.DocNo);
            var rfcByNo = rfc.ToLookup(r => r.RefNo);
            foreach (var no in chunk)
            {
                var i = invByNo[no].OrderByDescending(r => r.DocDate ?? r.TrDate ?? floor).ThenBy(r => r.RecNo).FirstOrDefault();
                var rfcs = rfcByNo[no].GroupBy(r => r.DocNo).OrderBy(g => g.Key)
                    .Select(g => new ArofRfc(g.Key, g.OrderBy(r => r.RecNo).Select(r => r.DocDate ?? r.TrDate).FirstOrDefault(d => d is not null),
                                             g.Sum(r => r.DrAmt - r.CrAmt)))
                    .ToList();
                if (i is null && rfcs.Count == 0)
                {
                    if (removeGone) removed.Add(no);
                    continue;
                }
                groups.Add(new ArofInvoiceGroup(no, i is null ? null : new ArofInvoice(
                    i.DocDate ?? i.TrDate ?? floor, i.CustKey, i.CKey, i.CustName, i.Term,
                    string.IsNullOrEmpty(i.PoNum) ? null : i.PoNum, i.DrAmt - i.CrAmt, i.Salesman, i.EmpNo, i.WhseNo, i.DocBal), rfcs));
            }
        }
        return new ArofSyncRequest(groups, removed);
    }
}
