using HOMSys.Application.DTOs.Rdp;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Cell = HOMSys.Infrastructure.Rdp.RdpUbaEngine.Cell;

namespace HOMSys.Infrastructure.Rdp;

/// <summary>
/// UBA (unique buying accounts) dashboard over the imported RDP sales lines — the "COO end-state" of the
/// TMG × IT UBA Tracker deck. Rules (template "Data Fields" / "Reference" sheets):
/// <list type="bullet">
/// <item>An account is one outlet code of one distributor (RdpSalesLine.AccountKey within DistributorId).</item>
/// <item>It is a buying account in a month when its net purchases that month (after returns, credits and discounts)
///   are above zero. Free Goods and Adjustment lines are not purchases and are left out of everything.</item>
/// <item>In a split, the rule applies within the slice: an account counts under a category when its net on that
///   category is above zero, so children can add up to more than their parent.</item>
/// <item>Over a range, an account counts once if it was buying in any month of it.</item>
/// <item>Channel, province, city and salesman are the account's most common value over its lines (channel: mapped
///   label first; "No channel in source" when blank).</item>
/// <item>"vs prior period" compares the same number of months just before, like-for-like: only distributors that
///   have data in both periods, so a newly submitting distributor doesn't read as growth.</item>
/// </list>
/// The numbers come from <see cref="RdpUbaEngine"/> (the lines rolled up in memory); this class adds the filters,
/// like-for-like and labels.
/// </summary>
public class RdpUbaService(AppDbContext db)
{
    public const string NoChannel = RdpUbaEngine.NoChannel;

    private static readonly Dictionary<string, string> Dims = new()
    {
        ["distributor"] = "Distributor",
        ["area"] = "Area",
        ["rdm"] = "RDM",
        ["territory"] = "Territory",
        ["branch"] = "Servicing branch",
        ["channel"] = "Channel type",
        ["category"] = "Category",
        ["subcategory"] = "Sub-category",
        ["sku"] = "SKU",
        ["province"] = "Province",
        ["city"] = "City / municipality",
        ["salesman"] = "Salesman",
    };

    private static readonly string[] Metrics = ["accounts", "sales", "cases"];

    private static string Ym(DateOnly d) => d.ToString("yyyy-MM");
    private static DateOnly? ParseYm(string? s) =>
        DateOnly.TryParseExact((s ?? "") + "-01", "yyyy-MM-dd", out var d) ? d : null;
    private static IEnumerable<DateOnly> Months(DateOnly from, DateOnly to)
    {
        for (var m = from; m <= to; m = m.AddMonths(1)) yield return m;
    }

    private Task<RdpUbaEngine> EngineAsync() => RdpUbaEngine.GetAsync(db.Database.GetConnectionString()!);

    // ── filters ─────────────────────────────────────────────────────────────

    public async Task<UbaFiltersDto> FiltersAsync(RdpSalesService rdp)
    {
        var distributors = await rdp.ListDistributorsAsync();
        var engine = await EngineAsync();
        var all = engine.MonthsOf.Count == 0 ? [] : Months(engine.MonthsOf[0].Period, engine.MonthsOf[^1].Period).Select(Ym).ToList();
        return new UbaFiltersDto(
            distributors.Select(d => d.Area).Where(a => a != "").Distinct().Order().ToList(),
            distributors.Select(d => d.Rdm).Where(a => a != "").Distinct().Order().ToList(),
            distributors, all, Dims.Select(d => new UbaOptionDto(d.Key, d.Value)).ToList());
    }

    // ── scope ───────────────────────────────────────────────────────────────

    private record Scope(DateOnly From, DateOnly To, DateOnly PriorFrom, DateOnly PriorTo, string Metric,
        List<int> DistributorIds, List<int> WithData, List<int> LikeForLike, RdpUbaEngine Engine, List<string>? Channels);

    private async Task<Scope> ScopeAsync(UbaFilterDto f)
    {
        var metric = Metrics.Contains(f.Metric) ? f.Metric! : "accounts";
        var q = db.RdpDistributors.AsNoTracking();
        if (f.Areas is { Count: > 0 } areas) q = q.Where(d => areas.Contains(d.Area));
        if (f.Rdms is { Count: > 0 } rdms) q = q.Where(d => rdms.Contains(d.Rdm));
        if (f.DistributorIds is { Count: > 0 } ids) q = q.Where(d => ids.Contains(d.Id));
        var distributorIds = await q.Select(d => d.Id).ToListAsync();

        var engine = await EngineAsync();
        var monthsOf = engine.MonthsOf;
        var last = monthsOf.Count == 0 ? new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1) : monthsOf[^1].Period;
        var to = ParseYm(f.To) ?? last;
        var from = ParseYm(f.From) ?? new DateOnly(to.Year, 1, 1);   // default: year to date
        if (from > to) (from, to) = (to, from);
        if (to.Year * 12 + to.Month - (from.Year * 12 + from.Month) > 59) from = to.AddMonths(-59);   // 5-year cap
        var months = Months(from, to).Count();
        var priorTo = from.AddMonths(-1);
        var priorFrom = from.AddMonths(-months);

        var present = monthsOf.Where(x => distributorIds.Contains(x.DistributorId) && x.Period >= priorFrom && x.Period <= to)
            .Select(x => new { x.DistributorId, Current = x.Period >= from }).Distinct().ToList();
        var withData = present.Where(p => p.Current).Select(p => p.DistributorId).Distinct().ToList();
        var lfl = withData.Intersect(present.Where(p => !p.Current).Select(p => p.DistributorId)).ToList();
        var channels = f.Channels is { Count: > 0 } ch ? ch : null;
        return new Scope(from, to, priorFrom, priorTo, metric, distributorIds, withData, lfl, engine, channels);
    }

    // ── aggregation ─────────────────────────────────────────────────────────

    private static string Key(int level, string k1, string k2, string k3) =>
        level switch { 0 => "", 1 => k1, 2 => k1 + "\u001f" + k2, _ => k1 + "\u001f" + k2 + "\u001f" + k3 };

    /// <summary>In memory; on the thread pool so a request's aggregates run side by side.</summary>
    private static Task<List<Cell>> AggregateAsync(Scope s, DateOnly from, DateOnly to, List<int> distributorIds, string metric, List<string> groupBy) =>
        Task.Run(() => s.Engine.Aggregate(from, to, distributorIds, metric, groupBy, s.Channels));
    // ── split (leaderboard / channel / split explorer) ──────────────────────

    public async Task<(UbaSplitDto? data, string? error)> SplitAsync(UbaSplitRequestDto req)
    {
        var groupBy = (req.GroupBy ?? []).Where(g => !string.IsNullOrWhiteSpace(g)).ToList();
        if (groupBy.Count > 3) return (null, "Group by at most three levels.");
        if (groupBy.FirstOrDefault(g => !Dims.ContainsKey(g)) is { } bad) return (null, $"Unknown grouping \"{bad}\".");
        if (groupBy.Distinct().Count() != groupBy.Count) return (null, "Each grouping can be used once.");

        var s = await ScopeAsync(req.Filter ?? new UbaFilterDto(null, null, null, null, null, null));
        var labels = await LabelsAsync();
        return (await SplitCoreAsync(s, labels, groupBy), null);
    }

    /// <summary>The split itself; no DbContext use, so it can run alongside other aggregates.</summary>
    private async Task<UbaSplitDto> SplitCoreAsync(Scope s, LabelSet labels, List<string> groupBy)
    {
        var lflSame = s.LikeForLike.Count == s.WithData.Count;
        var currentTask = AggregateAsync(s, s.From, s.To, s.DistributorIds, s.Metric, groupBy);
        var curLflTask = s.LikeForLike.Count == 0 || lflSame ? null : AggregateAsync(s, s.From, s.To, s.LikeForLike, s.Metric, groupBy);
        var priorTask = s.LikeForLike.Count == 0 ? null : AggregateAsync(s, s.PriorFrom, s.PriorTo, s.LikeForLike, s.Metric, groupBy);
        var current = await currentTask;
        static Dictionary<string, decimal> Ranges(List<Cell> cells) =>
            cells.Where(c => c.Period is null).GroupBy(c => Key(c.Level, c.K1, c.K2, c.K3)).ToDictionary(g => g.Key, g => g.First().Value);
        var curLfl = s.LikeForLike.Count == 0 ? [] : Ranges(curLflTask is null ? current : await curLflTask);
        var priorLfl = priorTask is null ? [] : Ranges(await priorTask);
        var periods = Months(s.From, s.To).ToList();

        var byNode = current.GroupBy(c => (c.Level, Key: Key(c.Level, c.K1, c.K2, c.K3))).ToDictionary(g => g.Key, g => g.ToList());
        var total = byNode.GetValueOrDefault((0, ""))?.FirstOrDefault(c => c.Period is null)?.Value ?? 0;

        UbaSplitRowDto RowOf(int level, List<Cell> cells)
        {
            var c0 = cells[0];
            var keys = new[] { c0.K1, c0.K2, c0.K3 }.Take(level).ToList();
            var key = Key(level, c0.K1, c0.K2, c0.K3);
            var range = cells.FirstOrDefault(c => c.Period is null)?.Value ?? 0;
            var monthly = cells.Where(c => c.Period is not null).ToDictionary(c => c.Period!.Value, c => c.Value);
            var cur = curLfl.GetValueOrDefault(key);
            var prior = priorLfl.GetValueOrDefault(key);
            string? area = null, rdm = null;
            if (level > 0 && groupBy[level - 1] == "distributor" && labels.Distributors.TryGetValue(keys[^1], out var d)) (area, rdm) = (d.Area, d.Rdm);
            return new UbaSplitRowDto(level, keys, level == 0 ? "Total" : labels.Label(groupBy[level - 1], keys[^1]), area, rdm,
                periods.Select(p => monthly.TryGetValue(p, out var v) ? v : (decimal?)null).ToList(),
                range, total == 0 ? 0 : Math.Round(range / total * 100, 2), prior > 0 ? Math.Round((cur - prior) / prior * 100, 1) : null);
        }

        // Depth-first, biggest first within each parent.
        var rows = new List<UbaSplitRowDto>();
        var children = byNode.Where(kv => kv.Key.Level > 0)
            .ToLookup(kv => (kv.Key.Level, Parent: Key(kv.Key.Level - 1, kv.Value[0].K1, kv.Value[0].K2, kv.Value[0].K3)), kv => kv.Value);
        void Children(int level, string parent)
        {
            if (level > groupBy.Count) return;
            var kids = children[(level, parent)].Select(cells => RowOf(level, cells))
                .OrderByDescending(r => r.Range).ThenBy(r => r.Label).ToList();
            foreach (var k in kids)
            {
                rows.Add(k);
                Children(level + 1, string.Join("\u001f", k.Keys));
            }
        }
        Children(1, "");
        var totalRow = byNode.TryGetValue((0, ""), out var tc) ? RowOf(0, tc)
            : new UbaSplitRowDto(0, [], "Total", null, null, periods.Select(_ => (decimal?)null).ToList(), 0, 0, null);

        var note = s.LikeForLike.Count < s.WithData.Count
            ? $"vs prior period = like-for-like: {s.LikeForLike.Count} of {s.WithData.Count} distributors have data in both {Ym(s.PriorFrom)}–{Ym(s.PriorTo)} and {Ym(s.From)}–{Ym(s.To)}."
            : null;
        return new UbaSplitDto(Ym(s.From), Ym(s.To), s.Metric, groupBy, periods.Select(Ym).ToList(), rows, totalRow, note);
    }

    // ── summary (scorecard, trend, by area) ─────────────────────────────────

    public async Task<UbaSummaryDto> SummaryAsync(UbaFilterDto f)
    {
        // DbContext work first (one at a time), then every aggregate at once.
        var s = await ScopeAsync(f);
        var labels = await LabelsAsync();
        var all = s.Engine.MonthsOf.Any(x => s.DistributorIds.Contains(x.DistributorId)) ? new { } : null;

        static decimal RangeOf(List<Cell> cells) => cells.FirstOrDefault(c => c.Period is null)?.Value ?? 0;
        async Task<UbaKpiDto> Kpi(string metric, Task<List<Cell>> curTask)
        {
            var lflSame = s.LikeForLike.Count == s.WithData.Count;
            var curLTask = s.LikeForLike.Count == 0 || lflSame ? null : AggregateAsync(s, s.From, s.To, s.LikeForLike, metric, []);
            var priorTask = s.LikeForLike.Count == 0 ? null : AggregateAsync(s, s.PriorFrom, s.PriorTo, s.LikeForLike, metric, []);
            var value = RangeOf(await curTask);
            if (priorTask is null) return new UbaKpiDto(value, null, null);
            var curL = curLTask is null ? value : RangeOf(await curLTask);
            var prior = RangeOf(await priorTask);
            return new UbaKpiDto(value, prior, prior > 0 ? Math.Round((curL - prior) / prior * 100, 1) : null);
        }

        var accountsTask = AggregateAsync(s, s.From, s.To, s.DistributorIds, "accounts", []);
        var accountsKpi = Kpi("accounts", accountsTask);
        var salesKpi = Kpi("sales", AggregateAsync(s, s.From, s.To, s.DistributorIds, "sales", []));
        var casesKpi = Kpi("cases", AggregateAsync(s, s.From, s.To, s.DistributorIds, "cases", []));
        var linesTask = LinesAndSkusAsync(s);
        var byAreaTask = SplitCoreAsync(s, labels, ["area"]);

        // Trend: the months of the picked range (From – To), in the chosen metric.
        Task<List<Cell>>? trendTask = null;
        DateOnly tFrom = s.From, tTo = s.To;
        if (all is not null) trendTask = AggregateAsync(s, tFrom, tTo, s.DistributorIds, s.Metric, []);

        var monthly = (await accountsTask).Where(c => c.Period is not null).ToList();
        var buyingAccountMonths = monthly.Sum(c => c.Value);
        var (skuLines, skusSold) = await linesTask;
        var trend = new List<UbaTrendPointDto>();
        if (trendTask is not null)
        {
            var byMonth = (await trendTask).Where(c => c.Period is not null).ToDictionary(c => c.Period!.Value, c => c.Value);
            trend = Months(tFrom, tTo).Select(m => new UbaTrendPointDto(Ym(m), byMonth.GetValueOrDefault(m), m >= s.From && m <= s.To)).ToList();
        }

        return new UbaSummaryDto(Ym(s.From), Ym(s.To), s.WithData.Count, s.LikeForLike.Count,
            await accountsKpi, await salesKpi, await casesKpi,
            monthly.Count == 0 ? 0 : Math.Round(buyingAccountMonths / monthly.Count, 0, MidpointRounding.AwayFromZero),
            buyingAccountMonths == 0 ? 0 : Math.Round(skuLines / buyingAccountMonths, 1), skusSold,
            trend, (await byAreaTask).Rows);
    }

    /// <summary>Account × month × SKU lines with net above zero (average lines per account) and SKUs sold in the range.</summary>
    private static Task<(decimal Lines, int Skus)> LinesAndSkusAsync(Scope s) =>
        s.DistributorIds.Count == 0 ? Task.FromResult((0m, 0)) : Task.Run(() => s.Engine.LinesAndSkus(s.From, s.To, s.DistributorIds, s.Channels));

    // ── labels ──────────────────────────────────────────────────────────────

    private record DistributorLabel(string Label, string Area, string Rdm);

    private record LabelSet(Dictionary<string, DistributorLabel> Distributors, Dictionary<string, string> Categories,
        Dictionary<string, string> SubCategories, Dictionary<string, string> Skus)
    {
        public string Label(string dim, string key) => dim switch
        {
            "distributor" => Distributors.TryGetValue(key, out var d) ? d.Label : key,
            "category" => key == "" ? "UNTAGGED" : Categories.GetValueOrDefault(key, key),
            "subcategory" => key == "" ? "UNTAGGED" : SubCategories.GetValueOrDefault(key, key),
            "sku" => key == "" ? "(no SKU)" : Skus.GetValueOrDefault(key, $"{key} (not in RAM SKU list)"),
            "branch" => key == "" ? "(no branch)" : key,
            "area" or "rdm" or "territory" => key == "" ? "(not tagged)" : key,
            _ => key == "" ? "(blank)" : key,
        };
    }

    private async Task<LabelSet> LabelsAsync()
    {
        var dists = await db.RdpDistributors.AsNoTracking().Include(d => d.Site).ToListAsync();
        var dupNames = dists.GroupBy(d => d.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        var distributors = dists.ToDictionary(d => d.Id.ToString(), d => new DistributorLabel(
            dupNames.Contains(d.Name) ? $"{d.Name} – {d.Site?.Name ?? d.CustKey.ToString()}" : d.Name, d.Area, d.Rdm));

        var cats = await db.ProductCategories.AsNoTracking().ToListAsync();
        var groups = await db.TasafProductGroups.AsNoTracking().ToDictionaryAsync(g => g.Code, g => g.Name);
        var categories = cats.GroupBy(c => c.GroupNo).ToDictionary(g => g.Key.ToString(),
            g => $"{g.Key:00} - {groups.GetValueOrDefault(g.Key.ToString("00")) ?? g.OrderBy(c => c.SeqNo).First().GroupDesc}");
        var subs = cats.ToDictionary(c => c.CategoryCode, c => $"{c.CategoryCode} - {c.GroupDesc}");
        var skus = await db.Products.AsNoTracking().ToDictionaryAsync(p => p.CProdNo, p => $"{p.CProdNo} - {p.ProdDesc} {p.PackSize}".Trim());
        return new LabelSet(distributors, categories, subs, skus);
    }
}