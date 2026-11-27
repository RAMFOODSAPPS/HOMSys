using HOMSys.Application.DTOs.Collections;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Collections;

/// <summary>
/// Applies the watcher's B1 rows (HOMS F:\HOMS\RAM\ARCHIVES\B103{MMYY}.DBF: each invoice's BIR series BIRNOFR..BIRNOTO,
/// the source DCSR's soachain fills the chain Excel's "BIR Series" from) to InvoiceBirSeries, per file: reset drops the
/// file's rows, then rows are upserted on (file, DOCID, DOCNO) and the removed ones deleted. Used to find the invoice of a
/// Remittance Advice line given only by its BIR series (Puregold, user 2026-11-25).
/// </summary>
public class BirSeriesImporter(AppDbContext db)
{
    public async Task<string> ImportAsync(BirSeriesSyncRequest req)
    {
        var source = (req.SourceFile ?? "").Trim().ToUpperInvariant();
        if (source.Length is 0 or > 8) throw new ArgumentException("sourceFile must be a B1 file name like B1030826.");

        var removed = 0;
        if (req.Reset) removed += await db.InvoiceBirSeries.Where(b => b.SourceFile == source).ExecuteDeleteAsync();
        foreach (var k in req.Removed ?? [])
            removed += await db.InvoiceBirSeries.Where(b => b.SourceFile == source && b.DocId == k.DocId && b.DocNo == k.DocNo)
                .ExecuteDeleteAsync();

        var rows = (req.Rows ?? []).Where(r => r.DocNo > 0 && r.BirFrom > 0)
            .GroupBy(r => (r.DocId, r.DocNo)).Select(g => g.Last()).ToList();
        var nos = rows.Select(r => r.DocNo).Distinct().ToList();
        var existing = (await db.InvoiceBirSeries.Where(b => b.SourceFile == source && nos.Contains(b.DocNo)).ToListAsync())
            .ToDictionary(b => (b.DocId, b.DocNo));
        int added = 0, changed = 0;
        foreach (var r in rows)
        {
            var to = r.BirTo >= r.BirFrom ? r.BirTo : r.BirFrom;
            if (!existing.TryGetValue((r.DocId, r.DocNo), out var b))
            {
                db.InvoiceBirSeries.Add(new InvoiceBirSeries
                {
                    SourceFile = source, WhseNo = r.WhseNo, DocId = r.DocId, DocNo = r.DocNo, DocDate = r.DocDate, BirFrom = r.BirFrom, BirTo = to,
                });
                added++;
            }
            else if (b.BirFrom != r.BirFrom || b.BirTo != to || b.DocDate != r.DocDate || b.WhseNo != r.WhseNo)
            {
                (b.BirFrom, b.BirTo, b.DocDate, b.WhseNo) = (r.BirFrom, to, r.DocDate, r.WhseNo);
                changed++;
            }
        }
        await db.SaveChangesAsync();
        return $"{source}: {added} added, {changed} changed, {removed} removed{(req.Reset ? " (reset)" : "")}";
    }
}
