using System.Runtime.InteropServices;
using Microsoft.Data.SqlClient;

namespace HOMSys.Infrastructure.Rdp;

/// <summary>
/// In-memory UBA store: the RDP sales lines rolled up to account × month × SKU (≈ 0.9M rows for ~1M lines), plus each
/// account's attributes. Built once per data version (uploads, channel mappings, RDP edits) — a few seconds, warmed in the
/// background after every change and at startup — after which any dashboard filter / split is computed in memory in
/// milliseconds instead of re-scanning SQL.
/// <para>Account attributes are the account's most common value over all its lines: channel (mapped label first, then
/// any label, then blank — the rule the SQL used per range), province, city and salesman.</para>
/// Free Goods and Adjustment lines are left out at load, as in every UBA figure.
/// </summary>
public sealed class RdpUbaEngine
{
    public const string NoChannel = "No channel in source";

    /// <summary>One aggregate cell: level (0 = total), keys down to that level, month (null = whole range).</summary>
    public record Cell(int Level, string K1, string K2, string K3, DateOnly? Period, decimal Value);

    public string Version { get; }

    /// <summary>Which distributor has data in which month.</summary>
    public IReadOnlyList<(int DistributorId, DateOnly Period)> MonthsOf { get; }

    // String table shared by every dimension; a dimension value is its index here.
    private readonly List<string> values = [];
    private readonly Dictionary<string, int> valueIds = [];

    // Accounts.
    private readonly int[] acctDist;
    private readonly int[] acctChan, acctProv, acctCity, acctSales;

    // Distributors (by DistributorId; sparse arrays sized to the max id).
    private readonly int[] distKey, distArea, distRdm, distTerr, distBranch;

    // SKUs.
    private readonly int[] skuKey, skuGroup, skuSub;
    private readonly bool[] skuReal;   // SkuCode <> ''

    // Facts, sorted by period.
    private readonly int[] fAcct, fSku, fPeriod;
    private readonly double[] fNet, fCases;

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static RdpUbaEngine? current;

    // ── load ────────────────────────────────────────────────────────────────

    /// <summary>The engine for the current data version, building it when the data changed.</summary>
    public static async Task<RdpUbaEngine> GetAsync(string connectionString)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        var version = await VersionAsync(conn);
        if (current?.Version == version) return current;
        await Gate.WaitAsync();
        try
        {
            if (current?.Version == version) return current;
            return current = await LoadAsync(conn, version);
        }
        finally { Gate.Release(); }
    }

    /// <summary>Builds the engine in the background (after an upload / mapping change, and at startup).</summary>
    public static void Warm(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return;
        _ = Task.Run(async () => { try { await GetAsync(connectionString); } catch { /* the next dashboard request retries */ } });
    }

    private static async Task<string> VersionAsync(SqlConnection conn)
    {
        await using var cmd = new SqlCommand("""
            SELECT CONCAT((SELECT COUNT(*) FROM RdpSalesUploads), '.', (SELECT MAX(Id) FROM RdpSalesUploads), '.',
                          (SELECT COUNT(*) FROM RdpChannelMaps), '.', CONVERT(varchar(30), (SELECT MAX(UpdatedAt) FROM RdpChannelMaps), 126), '.',
                          CONVERT(varchar(30), (SELECT MAX(UpdatedAt) FROM RdpDistributors), 126))
            """, conn);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<RdpUbaEngine> LoadAsync(SqlConnection conn, string version)
    {
        async Task Read(string sql, Action<SqlDataReader> row)
        {
            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 300 };
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) row(r);
        }

        var dists = new List<(int Id, string Area, string Rdm, string Terr, string Site)>();
        await Read("SELECT d.Id, d.Area, d.Rdm, d.Territory, COALESCE(s.Name, N'') FROM RdpDistributors d LEFT JOIN Sites s ON s.Id = d.SiteId",
            r => dists.Add((r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4))));

        var maps = new Dictionary<string, string>();
        await Read("SELECT Label, Channel FROM RdpChannelMaps", r => maps[r.GetString(0)] = r.GetString(1));

        var skuAttrs = new Dictionary<string, (string Group, string Sub)>();
        await Read("""
            SELECT p.CProdNo, COALESCE(CAST(pc.GroupNo AS nvarchar(5)), N''), COALESCE(p.Category, N'')
            FROM Products p LEFT JOIN ProductCategories pc ON pc.CategoryCode = p.Category
            """, r => skuAttrs.TryAdd(r.GetString(0), (r.GetString(1), r.GetString(2))));

        const string purchases = "TxnType NOT IN (N'Free Goods', N'Adjustment')";

        // Account attributes: line counts per account and value.
        var accounts = new Dictionary<(int, string), int>();
        var attrCounts = new List<(int Acct, string Label, string Prov, string City, string Sales, int N)>();
        await Read($"""
            SELECT DistributorId, AccountKey, ChannelLabel, Province, City, Salesman, COUNT(*)
            FROM RdpSalesLines WHERE {purchases}
            GROUP BY DistributorId, AccountKey, ChannelLabel, Province, City, Salesman
            """, r =>
        {
            var key = (r.GetInt32(0), r.GetString(1));
            if (!accounts.TryGetValue(key, out var a)) accounts[key] = a = accounts.Count;
            attrCounts.Add((a, r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetInt32(6)));
        });

        var facts = new List<(int Acct, string Sku, DateOnly Period, double Net, double Cases)>(1_000_000);
        await Read($"""
            SELECT DistributorId, AccountKey, Period, SkuCode, SUM(NetAmount), COALESCE(SUM(Cases), 0)
            FROM RdpSalesLines WHERE {purchases}
            GROUP BY DistributorId, AccountKey, Period, SkuCode
            """, r =>
        {
            if (!accounts.TryGetValue((r.GetInt32(0), r.GetString(1)), out var a)) return;
            facts.Add((a, r.GetString(3), DateOnly.FromDateTime(r.GetDateTime(2)), (double)r.GetDecimal(4), (double)r.GetDecimal(5)));
        });

        return new RdpUbaEngine(version, dists, maps, skuAttrs, accounts, attrCounts, facts);
    }

    private RdpUbaEngine(string version, List<(int Id, string Area, string Rdm, string Terr, string Site)> dists,
        Dictionary<string, string> maps, Dictionary<string, (string Group, string Sub)> skuAttrs,
        Dictionary<(int, string), int> accounts, List<(int Acct, string Label, string Prov, string City, string Sales, int N)> attrCounts,
        List<(int Acct, string Sku, DateOnly Period, double Net, double Cases)> facts)
    {
        Version = version;
        int V(string s)
        {
            if (!valueIds.TryGetValue(s, out var id)) { valueIds[s] = id = values.Count; values.Add(s); }
            return id;
        }

        var maxDist = dists.Count == 0 ? 0 : dists.Max(d => d.Id);
        distKey = new int[maxDist + 1]; distArea = new int[maxDist + 1]; distRdm = new int[maxDist + 1];
        distTerr = new int[maxDist + 1]; distBranch = new int[maxDist + 1];
        foreach (var d in dists)
        {
            distKey[d.Id] = V(d.Id.ToString()); distArea[d.Id] = V(d.Area); distRdm[d.Id] = V(d.Rdm);
            distTerr[d.Id] = V(d.Terr); distBranch[d.Id] = V(d.Site);
        }

        // Accounts and their most common attributes.
        var n = accounts.Count;
        acctDist = new int[n]; acctChan = new int[n]; acctProv = new int[n]; acctCity = new int[n]; acctSales = new int[n];
        foreach (var ((dist, _), a) in accounts) acctDist[a] = dist <= maxDist ? dist : 0;
        foreach (var g in attrCounts.GroupBy(x => x.Acct))
        {
            // Channel: mapped label first, then any label, then blank; most lines; then name.
            var chan = g.GroupBy(x => x.Label).Select(l =>
                {
                    var known = maps.TryGetValue(l.Key, out var mapped) ? 0 : l.Key != "" ? 1 : 2;
                    var name = mapped ?? (l.Key == "" ? NoChannel : "Unmapped: " + l.Key);
                    return (Known: known, N: l.Sum(x => x.N), Name: name);
                })
                .OrderBy(x => x.Known).ThenByDescending(x => x.N).ThenBy(x => x.Name, StringComparer.Ordinal).First().Name;
            acctChan[g.Key] = V(chan);
            acctProv[g.Key] = V(Mode(g.Select(x => (x.Prov, x.N))));
            acctCity[g.Key] = V(Mode(g.Select(x => (x.City, x.N))));
            acctSales[g.Key] = V(Mode(g.Select(x => (x.Sales, x.N))));
        }

        // SKUs.
        var skuIds = new Dictionary<string, int>();
        var skuList = new List<string>();
        int S(string code)
        {
            if (!skuIds.TryGetValue(code, out var id)) { skuIds[code] = id = skuList.Count; skuList.Add(code); }
            return id;
        }

        facts.Sort((x, y) => x.Period.CompareTo(y.Period));
        var m = facts.Count;
        fAcct = new int[m]; fSku = new int[m]; fPeriod = new int[m]; fNet = new double[m]; fCases = new double[m];
        var monthsOf = new HashSet<(int, DateOnly)>();
        for (var i = 0; i < m; i++)
        {
            var f = facts[i];
            fAcct[i] = f.Acct; fSku[i] = S(f.Sku); fPeriod[i] = P(f.Period); fNet[i] = f.Net; fCases[i] = f.Cases;
            monthsOf.Add((acctDist[f.Acct], f.Period));
        }
        MonthsOf = monthsOf.OrderBy(x => x.Item2).ThenBy(x => x.Item1).ToList();

        skuKey = new int[skuList.Count]; skuGroup = new int[skuList.Count]; skuSub = new int[skuList.Count]; skuReal = new bool[skuList.Count];
        for (var i = 0; i < skuList.Count; i++)
        {
            var (group, sub) = skuAttrs.GetValueOrDefault(skuList[i], ("", ""));
            skuKey[i] = V(skuList[i]); skuGroup[i] = V(group); skuSub[i] = V(sub); skuReal[i] = skuList[i] != "";
        }
    }

    /// <summary>The most common non-blank value (by line count; ties by name), else blank.</summary>
    private static string Mode(IEnumerable<(string Value, int N)> rows) =>
        rows.GroupBy(x => x.Value).Select(g => (Value: g.Key, N: g.Sum(x => x.N)))
            .OrderBy(x => x.Value == "" ? 1 : 0).ThenByDescending(x => x.N).ThenBy(x => x.Value, StringComparer.Ordinal).First().Value;

    private static int P(DateOnly d) => d.Year * 12 + d.Month - 1;
    private static DateOnly D(int p) => new(p / 12, p % 12 + 1, 1);

    // ── queries ─────────────────────────────────────────────────────────────

    /// <summary>The facts of these distributors (and, when given, accounts of these channel types) in [from, to].</summary>
    private int[] Select(DateOnly from, DateOnly to, IReadOnlyCollection<int> distributorIds, IReadOnlyCollection<string>? channels)
    {
        var ds = distributorIds.ToHashSet();
        HashSet<int>? chans = channels is { Count: > 0 }
            ? channels.Select(c => valueIds.TryGetValue(c, out var id) ? id : -2).ToHashSet() : null;
        int pf = P(from), pt = P(to);
        var start = LowerBound(pf);
        var list = new List<int>();
        for (var i = start; i < fPeriod.Length && fPeriod[i] <= pt; i++)
            if (ds.Contains(acctDist[fAcct[i]]) && (chans is null || chans.Contains(acctChan[fAcct[i]]))) list.Add(i);
        return list.ToArray();
    }

    private int LowerBound(int p)
    {
        int lo = 0, hi = fPeriod.Length;
        while (lo < hi) { var mid = (lo + hi) / 2; if (fPeriod[mid] < p) lo = mid + 1; else hi = mid; }
        return lo;
    }

    private Func<int, int> Dim(string dim) => dim switch
    {
        "distributor" => i => distKey[acctDist[fAcct[i]]],
        "area" => i => distArea[acctDist[fAcct[i]]],
        "rdm" => i => distRdm[acctDist[fAcct[i]]],
        "territory" => i => distTerr[acctDist[fAcct[i]]],
        "branch" => i => distBranch[acctDist[fAcct[i]]],
        "channel" => i => acctChan[fAcct[i]],
        "province" => i => acctProv[fAcct[i]],
        "city" => i => acctCity[fAcct[i]],
        "salesman" => i => acctSales[fAcct[i]],
        "category" => i => skuGroup[fSku[i]],
        "subcategory" => i => skuSub[fSku[i]],
        "sku" => i => skuKey[fSku[i]],
        _ => throw new ArgumentException($"Unknown dimension {dim}"),
    };

    private readonly record struct Keys(int K1, int K2, int K3);
    private readonly record struct AcctMonth(Keys Keys, int Acct, int Period);
    private readonly record struct KeyMonth(Keys Keys, int Period);
    private readonly record struct KeyAcct(Keys Keys, int Acct);

    /// <summary>
    /// Every level 0..n of the grouping, per month and for the whole range. accounts = buying accounts (net &gt; 0 in a
    /// month, counted once over the range, within the slice); sales / cases = sums.
    /// </summary>
    public List<Cell> Aggregate(DateOnly from, DateOnly to, IReadOnlyCollection<int> distributorIds, string metric, IReadOnlyList<string> groupBy,
        IReadOnlyCollection<string>? channels = null)
    {
        if (distributorIds.Count == 0) return [];
        var rows = Select(from, to, distributorIds, channels);
        var dims = groupBy.Select(Dim).ToArray();
        var cells = new List<Cell>();
        const int none = -1;

        for (var level = 0; level <= groupBy.Count; level++)
        {
            Keys KeysOf(int i) => new(level >= 1 ? dims[0](i) : none, level >= 2 ? dims[1](i) : none, level >= 3 ? dims[2](i) : none);
            string Str(int id) => id == none ? "" : values[id];

            if (metric == "accounts")
            {
                var net = new Dictionary<AcctMonth, double>();
                foreach (var i in rows)
                    CollectionsMarshal.GetValueRefOrAddDefault(net, new AcctMonth(KeysOf(i), fAcct[i], fPeriod[i]), out _) += fNet[i];
                var monthly = new Dictionary<KeyMonth, int>();
                var buyers = new HashSet<KeyAcct>();
                foreach (var (k, v) in net)
                {
                    if (v <= 0.000001) continue;
                    CollectionsMarshal.GetValueRefOrAddDefault(monthly, new KeyMonth(k.Keys, k.Period), out _)++;
                    buyers.Add(new KeyAcct(k.Keys, k.Acct));
                }
                foreach (var (k, v) in monthly) cells.Add(new Cell(level, Str(k.Keys.K1), Str(k.Keys.K2), Str(k.Keys.K3), D(k.Period), v));
                foreach (var g in buyers.GroupBy(b => b.Keys))
                    cells.Add(new Cell(level, Str(g.Key.K1), Str(g.Key.K2), Str(g.Key.K3), null, g.Count()));
            }
            else
            {
                var values2 = metric == "cases" ? fCases : fNet;
                var monthly = new Dictionary<KeyMonth, double>();
                var range = new Dictionary<Keys, double>();
                foreach (var i in rows)
                {
                    var k = KeysOf(i);
                    CollectionsMarshal.GetValueRefOrAddDefault(monthly, new KeyMonth(k, fPeriod[i]), out _) += values2[i];
                    CollectionsMarshal.GetValueRefOrAddDefault(range, k, out _) += values2[i];
                }
                foreach (var (k, v) in monthly) cells.Add(new Cell(level, Str(k.Keys.K1), Str(k.Keys.K2), Str(k.Keys.K3), D(k.Period), Money(v)));
                foreach (var (k, v) in range) cells.Add(new Cell(level, Str(k.K1), Str(k.K2), Str(k.K3), null, Money(v)));
            }
        }
        return cells;
    }

    /// <summary>Account × month × SKU lines with net above zero, and SKUs whose net over the range is above zero.</summary>
    public (decimal Lines, int Skus) LinesAndSkus(DateOnly from, DateOnly to, IReadOnlyCollection<int> distributorIds,
        IReadOnlyCollection<string>? channels = null)
    {
        var rows = Select(from, to, distributorIds, channels);
        var lines = 0;
        var bySku = new Dictionary<int, double>();
        foreach (var i in rows)
        {
            if (!skuReal[fSku[i]]) continue;
            if (fNet[i] > 0.000001) lines++;
            CollectionsMarshal.GetValueRefOrAddDefault(bySku, fSku[i], out _) += fNet[i];
        }
        return (lines, bySku.Count(x => x.Value > 0.000001));
    }

    private static decimal Money(double v) => Math.Round((decimal)v, 2);
}
