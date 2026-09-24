using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HOMSys.Application.DTOs.Analytics;
using Microsoft.Data.SqlClient;

namespace HOMSys.Infrastructure.Analytics;

/// <summary>A spec the catalog rejects — message is safe to show the user.</summary>
public sealed class SpecException(string message) : Exception(message);

public readonly record struct DateRange(DateOnly From, DateOnly To);

/// <param name="Subtotals">True when a 2-dim shape also groups per-d0/per-d1 subtotals (pivot, 2-dim charts).</param>
public sealed record BuiltQuery(string Sql, List<SqlParameter> Params, List<ColumnDto> Columns, int DimCount,
    bool AllAdditive, DateRange? Dim0Range, bool Subtotals = false);

/// <summary>
/// Turns a ReportSpec into one parameterised SELECT … GROUP BY GROUPING SETS.
/// Safety model: every dataset/field/agg/op/bucket/sort key is looked up in
/// AnalyticsCatalog or a fixed switch; every value is a typed SqlParameter.
/// The generated SQL is only registry fragments, generated aliases and
/// parameter names — client text is never concatenated. Branch RLS is not
/// part of the spec: Build() injects it from the viewer's scope.
/// </summary>
public static partial class AnalyticsSqlBuilder
{
    public const int MaxMeasures = 8, MaxFilters = 20, MaxInValues = 500, MaxValueLength = 200, MaxFillBuckets = 400;

    private static readonly HashSet<string> Visuals = ["table", "pivot", "kpi", "bar", "line", "pie", "doughnut"];
    private static readonly HashSet<string> BucketNames = ["day", "week", "month", "quarter", "year"];

    public static readonly string[] Presets = ["today", "yesterday", "last7", "last30", "mtd", "lastMonth", "qtd", "ytd", "last12m"];

    // ── context: parameters + joins collected while building ─────────────────
    private sealed class Ctx(Ds ds)
    {
        public readonly List<SqlParameter> P = [];
        public readonly HashSet<string> Joins = new(StringComparer.OrdinalIgnoreCase);
        private int _n;

        public string Add(SqlDbType type, object? value)
        {
            var name = "@p" + _n++;
            P.Add(NewParam(name, type, value));
            return name;
        }

        public void Need(string[]? aliases)
        {
            if (aliases is null) return;
            foreach (var a in aliases)
            {
                var join = ds.Joins?.FirstOrDefault(j => string.Equals(j.Alias, a, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"Analytics dataset '{ds.Key}' has no join '{a}'.");
                if (Joins.Add(join.Alias)) Need(join.DependsOn);
            }
        }

        public string JoinSql() =>
            string.Join("\n", (ds.Joins ?? []).Where(j => Joins.Contains(j.Alias)).Select(j => j.Sql));
    }

    private static SqlParameter NewParam(string name, SqlDbType type, object? value)
    {
        var p = new SqlParameter(name, type) { Value = value ?? DBNull.Value };
        if (type == SqlDbType.NVarChar) p.Size = MaxValueLength + 2;
        if (type == SqlDbType.Decimal) { p.Precision = 18; p.Scale = 4; }
        return p;
    }

    // ── public entry points ──────────────────────────────────────────────────

    public static BuiltQuery Build(Ds ds, ReportSpecDto spec, BranchScope? scope, DateOnly today, int cap)
    {
        var type = (spec.Type ?? "table").Trim();
        if (!Visuals.Contains(type)) throw new SpecException($"Unknown visual '{spec.Type}'.");
        ValidateShape(type, spec);

        var c = new Ctx(ds);
        var select = new List<string>();
        var dimExprs = new List<string>();
        var columns = new List<ColumnDto>();
        var dimFields = new List<(Fld f, string? bucket)>();

        // Dimensions
        for (var i = 0; i < spec.Dimensions.Count; i++)
        {
            var d = spec.Dimensions[i];
            var f = RequireField(ds, d.Field);
            if (f.Kind == FieldKind.Measure) throw new SpecException($"'{f.Label}' is a measure and can't be used as a dimension.");
            string? bucket = null;
            string expr;
            if (f.Kind == FieldKind.Date)
            {
                bucket = string.IsNullOrWhiteSpace(d.Bucket) ? "day" : d.Bucket.Trim();
                if (!BucketNames.Contains(bucket)) throw new SpecException($"Unknown date bucket '{d.Bucket}'.");
                expr = BucketExpr(f, bucket);
            }
            else expr = f.Sql;

            if (dimExprs.Contains(expr)) throw new SpecException($"'{f.Label}' is used twice.");
            c.Need(f.Joins);
            dimExprs.Add(expr);
            dimFields.Add((f, bucket));
            select.Add($"{expr} AS d{i}");
            columns.Add(new ColumnDto($"d{i}", f.Key, f.Label, f.Kind == FieldKind.Date ? "date" : f.Type, "dim", Bucket: bucket));
            if (f.Caption is not null)
            {
                select.Add($"MAX({f.Caption}) AS d{i}c");
                columns.Add(new ColumnDto($"d{i}c", f.Key, f.Label, "string", "caption", Of: $"d{i}"));
            }
        }

        // Measures
        var allAdditive = true;
        for (var j = 0; j < spec.Measures.Count; j++)
        {
            var m = spec.Measures[j];
            var f = RequireField(ds, m.Field);
            var (expr, additive, mType, format, agg) = MeasureExpr(f, m.Agg);
            c.Need(f.Joins);
            allAdditive &= additive;
            select.Add($"{expr} AS m{j}");
            var label = string.IsNullOrWhiteSpace(m.Label) ? DefaultMeasureLabel(f, agg) : Truncate(m.Label.Trim(), 100);
            columns.Add(new ColumnDto($"m{j}", f.Key, label, mType, "measure", format, additive));
        }

        var n = dimExprs.Count;
        if (n > 0) select.Add($"GROUPING_ID({string.Join(", ", dimExprs)}) AS g");

        // WHERE: dataset fixed filter, RLS scope, user filters — every clause parenthesised and ANDed
        var where = new List<string>();
        if (ds.Where is not null) where.Add(ds.Where);
        var scopeSql = ScopeSql(ds, scope, c);
        if (scopeSql is not null) where.Add(scopeSql);
        DateRange? dim0Range = null;
        foreach (var flt in spec.Filters)
        {
            var (sql, range, fieldKey) = FilterSql(ds, flt, c, today);
            if (sql is not null) where.Add(sql);
            if (range is not null && n > 0 && string.Equals(fieldKey, dimFields[0].f.Key, StringComparison.OrdinalIgnoreCase))
                dim0Range = range;
        }

        c.P.Add(NewParam("@today", SqlDbType.Date, today.ToDateTime(TimeOnly.MinValue)));
        c.P.Add(NewParam("@cap", SqlDbType.Int, cap + 1));

        var sb = new StringBuilder();
        sb.Append("SELECT TOP (@cap) ").AppendJoin(",\n       ", select).Append('\n');
        sb.Append("FROM ").Append(ds.From).Append('\n');
        var joins = c.JoinSql();
        if (joins.Length > 0) sb.Append(joins).Append('\n');
        if (where.Count > 0) sb.Append("WHERE ").AppendJoin("\n  AND ", where.Select(w => $"({w})")).Append('\n');
        // Per-d0/per-d1 subtotals only where they're consumed (pivot margins,
        // 2-dim chart top-N ranking). Tables get detail + grand total only, so
        // subtotal rows (sorted first) can never crowd detail rows out of the cap.
        var subtotals = n == 2 && type != "table";
        if (subtotals) sb.Append($"GROUP BY GROUPING SETS (({dimExprs[0]}, {dimExprs[1]}), ({dimExprs[0]}), ({dimExprs[1]}), ())\n");
        else if (n > 0) sb.Append($"GROUP BY GROUPING SETS (({string.Join(", ", dimExprs)}), ())\n");
        if (n > 0) sb.Append("ORDER BY g DESC, ").Append(OrderBy(spec, dimFields, spec.Measures.Count));

        return new BuiltQuery(sb.ToString(), c.P, columns, n, allAdditive, dim0Range, subtotals);
    }

    /// <summary>Distinct values (+caption) of one dimension for filter pickers, RLS applied.</summary>
    public static BuiltQuery BuildValues(Ds ds, string fieldKey, string? q, int take, BranchScope? scope, DateOnly today)
    {
        var f = RequireField(ds, fieldKey);
        if (f.Kind != FieldKind.Dim) throw new SpecException($"'{f.Label}' has no value list.");
        var c = new Ctx(ds);
        c.Need(f.Joins);

        var where = new List<string>();
        if (ds.Where is not null) where.Add(ds.Where);
        var scopeSql = ScopeSql(ds, scope, c);
        if (scopeSql is not null) where.Add(scopeSql);

        var caption = f.Caption ?? "NULL";
        var having = "";
        if (!string.IsNullOrWhiteSpace(q))
        {
            var p = c.Add(SqlDbType.NVarChar, "%" + EscapeLike(Truncate(q.Trim(), MaxValueLength)) + "%");
            having = $"HAVING CAST({f.Sql} AS nvarchar(200)) LIKE {p} OR MAX({caption}) LIKE {p}\n";
        }
        c.P.Add(NewParam("@today", SqlDbType.Date, today.ToDateTime(TimeOnly.MinValue)));
        c.P.Add(NewParam("@cap", SqlDbType.Int, Math.Clamp(take, 1, 200)));

        var sb = new StringBuilder();
        sb.Append($"SELECT TOP (@cap) {f.Sql} AS v, MAX({caption}) AS c\nFROM {ds.From}\n");
        var joins = c.JoinSql();
        if (joins.Length > 0) sb.Append(joins).Append('\n');
        if (where.Count > 0) sb.Append("WHERE ").AppendJoin("\n  AND ", where.Select(w => $"({w})")).Append('\n');
        sb.Append($"GROUP BY {f.Sql}\n").Append(having);
        sb.Append(f.Sort is not null ? $"ORDER BY MIN({f.Sort}), v" : "ORDER BY v");

        var cols = new List<ColumnDto> { new("v", f.Key, f.Label, f.Type, "dim"), new("c", f.Key, f.Label, "string", "caption") };
        return new BuiltQuery(sb.ToString(), c.P, cols, 0, true, null);
    }

    /// <summary>The same spec over the prior window of its first bounded date filter (KPI "vs previous period").</summary>
    public static (ReportSpecDto Spec, string Label)? PreviousPeriod(Ds ds, ReportSpecDto spec, DateOnly today)
    {
        for (var i = 0; i < spec.Filters.Count; i++)
        {
            var flt = spec.Filters[i];
            var f = ds.Field(flt.Field);
            if (f is null || f.Kind != FieldKind.Date) continue;
            DateRange? r = flt.Op switch
            {
                "preset" when flt.Values is [var p, ..] && p is not null => ResolvePreset(p, today),
                "between" when flt.Values is [var a, var b, ..] && a is not null && b is not null => new DateRange(ParseDate(a), ParseDate(b)),
                _ => null,
            };
            if (r is not { } range) continue;

            var preset = flt.Op == "preset" ? flt.Values![0] : null;
            var (pa, pb) = preset switch
            {
                "mtd" or "lastMonth" => (range.From.AddMonths(-1), preset == "lastMonth"
                    ? EndOfMonth(range.From.AddMonths(-1)) : range.To.AddMonths(-1)),
                "qtd" => (range.From.AddMonths(-3), range.To.AddMonths(-3)),
                "ytd" or "last12m" => (range.From.AddYears(-1), range.To.AddYears(-1)),
                _ => ShiftBack(range),
            };

            var filters = spec.Filters.ToList();
            filters[i] = new SpecFilter(flt.Field, "between", [pa.ToString("yyyy-MM-dd"), pb.ToString("yyyy-MM-dd")]);
            var prev = Clone(spec);
            prev.Filters = filters;
            prev.Compare = null;
            return (prev, $"vs {pa:MMM d} – {pb:MMM d, yyyy}");
        }
        return null;

        static (DateOnly, DateOnly) ShiftBack(DateRange r)
        {
            var len = r.To.DayNumber - r.From.DayNumber + 1;
            return (r.From.AddDays(-len), r.To.AddDays(-len));
        }
    }

    /// <summary>Human-readable filter summary for export headers, e.g. "Order Date = Month to date (2026-09-01 – 2026-09-24)".</summary>
    public static string Describe(Ds ds, ReportSpecDto spec, DateOnly today)
    {
        var parts = new List<string>();
        foreach (var flt in spec.Filters)
        {
            var f = ds.Field(flt.Field);
            if (f is null || flt.Op == "any") continue;
            var vals = flt.Values ?? [];
            var text = flt.Op switch
            {
                "preset" when vals is [var p, ..] && p is not null && Presets.Contains(p) =>
                    $"{PresetLabel(p)} ({ResolvePreset(p, today).From:yyyy-MM-dd} – {ResolvePreset(p, today).To:yyyy-MM-dd})",
                "between" => $"{vals.ElementAtOrDefault(0)} – {vals.ElementAtOrDefault(1)}",
                "isNull" => "(blank)",
                "notNull" => "(not blank)",
                "in" or "eq" => string.Join(", ", vals.Select(v => v ?? "(blank)")),
                "notIn" or "neq" => "not " + string.Join(", ", vals.Select(v => v ?? "(blank)")),
                _ => $"{flt.Op} {string.Join(", ", vals)}",
            };
            parts.Add($"{f.Label} = {text}");
        }
        return parts.Count == 0 ? "No filters" : string.Join("; ", parts);
    }

    // ── validation ───────────────────────────────────────────────────────────

    private static void ValidateShape(string type, ReportSpecDto spec)
    {
        int d = spec.Dimensions.Count, m = spec.Measures.Count;
        if (m == 0) throw new SpecException("Add at least one value (measure).");
        if (m > MaxMeasures) throw new SpecException($"At most {MaxMeasures} values are allowed.");
        if (spec.Filters.Count > MaxFilters) throw new SpecException($"At most {MaxFilters} filters are allowed.");
        var ok = type switch
        {
            "kpi" => d == 0 && m <= 4,
            "pie" or "doughnut" => d == 1 && m == 1,
            "bar" or "line" => d is 1 or 2 && (d == 1 || m == 1),
            "pivot" => d == 2,
            _ => d <= 10,
        };
        if (!ok)
            throw new SpecException(type switch
            {
                "kpi" => "A KPI card takes no rows and up to 4 values.",
                "pie" or "doughnut" => "A pie/doughnut takes exactly 1 row field and 1 value.",
                "bar" or "line" => "A chart takes 1 axis field (+1 optional legend field with a single value).",
                "pivot" => "A matrix takes exactly 1 row field and 1 column field.",
                _ => "A table takes at most 10 row fields.",
            });
        if (spec.Limit is < 1 or > 1000) throw new SpecException("Top N must be between 1 and 1,000.");
    }

    private static Fld RequireField(Ds ds, string? key) =>
        (key is null ? null : ds.Field(key)) ?? throw new SpecException($"Unknown field '{key}'.");

    // ── expressions ──────────────────────────────────────────────────────────

    /// <summary>UTC -> Philippine wall clock. PH has had no DST since 1978, so a fixed +8h is exact.</summary>
    private static string Local(Fld f) => f.Utc ? $"DATEADD(hour, 8, {f.Sql})" : f.Sql;

    private static string DateExpr(Fld f) => f.Utc ? $"CAST({Local(f)} AS date)" : f.Sql;

    private static string BucketExpr(Fld f, string bucket)
    {
        var x = Local(f);
        var b = bucket switch
        {
            "day" => $"CAST({x} AS date)",
            // Monday-start week, independent of SET DATEFIRST (1900-01-01 was a Monday)
            "week" => $"DATEADD(day, -(DATEDIFF(day, '19000101', {x}) % 7), CAST({x} AS date))",
            "month" => $"DATEFROMPARTS(YEAR({x}), MONTH({x}), 1)",
            "quarter" => $"DATEFROMPARTS(YEAR({x}), (DATEPART(quarter, {x}) - 1) * 3 + 1, 1)",
            _ => $"DATEFROMPARTS(YEAR({x}), 1, 1)",
        };
        // Bogus legacy years (0022, 1922…) bucket to NULL = "(invalid date)"
        return $"CASE WHEN {x} >= '{f.MinYear:D4}0101' THEN {b} END";
    }

    public static IReadOnlyList<string> AllowedAggs(Fld f) => f switch
    {
        { Kind: FieldKind.Measure, Agg: "custom" } => ["custom"],
        { Kind: FieldKind.Measure } => ["sum", "avg", "min", "max", "count"],
        { Kind: FieldKind.Date } => ["count", "countDistinct", "min", "max"],
        { Type: "int" } => ["count", "countDistinct", "min", "max"],
        _ => ["count", "countDistinct"],
    };

    public static string DefaultAgg(Fld f) => f.Kind == FieldKind.Measure ? f.Agg ?? "sum" : "countDistinct";

    private static (string Expr, bool Additive, string Type, string? Format, string Agg) MeasureExpr(Fld f, string? requested)
    {
        var agg = string.IsNullOrWhiteSpace(requested) ? DefaultAgg(f) : requested.Trim();
        if (!AllowedAggs(f).Contains(agg))
            throw new SpecException($"'{agg}' is not available for '{f.Label}'.");
        var src = f.Kind == FieldKind.Date ? Local(f) : f.Sql;
        return agg switch
        {
            "custom" => (f.Sql, f.Additive, f.Type, f.Format, agg),
            "sum" => ($"SUM({src})", true, f.Type, f.Format, agg),
            "avg" => ($"AVG(CAST({src} AS decimal(18,4)))", false, "decimal", f.Format is "int" or null ? "dec" : f.Format, agg),
            "min" => ($"MIN({src})", false, f.Type, f.Format, agg),
            "max" => ($"MAX({src})", false, f.Type, f.Format, agg),
            "count" => ($"COUNT({src})", true, "int", "int", agg),
            _ => ($"COUNT(DISTINCT {src})", false, "int", "int", agg),
        };
    }

    private static string DefaultMeasureLabel(Fld f, string agg) =>
        agg == "custom" || (f.Kind == FieldKind.Measure && agg == (f.Agg ?? "sum"))
            ? f.Label
            : agg switch
            {
                "sum" => $"Total {f.Label}",
                "avg" => $"Avg {f.Label}",
                "min" => $"Min {f.Label}",
                "max" => $"Max {f.Label}",
                "count" => $"Count of {f.Label}",
                _ => $"Distinct {f.Label}",
            };

    private static string OrderBy(ReportSpecDto spec, List<(Fld f, string? bucket)> dims, int measureCount)
    {
        string DimOrder(int i, string dir) =>
            dims[i].f.Sort is { } s ? $"MIN({s}) {dir}, d{i} {dir}" : $"d{i} {dir}";

        var parts = new List<string>();
        if (spec.Sort is { } sort)
        {
            var dir = sort.Dir?.Trim().ToLowerInvariant() switch
            {
                "asc" => "ASC",
                "desc" or null => "DESC",
                _ => throw new SpecException("Sort direction must be asc or desc."),
            };
            var m = SortKey().Match(sort.By ?? "");
            if (!m.Success) throw new SpecException($"Unknown sort '{sort.By}'.");
            var idx = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            var isCaption = m.Groups[3].Success;
            switch (m.Groups[1].Value)
            {
                case "m" when idx < measureCount && !isCaption: parts.Add($"m{idx} {dir}"); break;
                case "d" when idx < dims.Count && isCaption && dims[idx].f.Caption is not null: parts.Add($"d{idx}c {dir}"); break;
                case "d" when idx < dims.Count && !isCaption: parts.Add(DimOrder(idx, dir)); break;
                default: throw new SpecException($"Unknown sort '{sort.By}'.");
            }
        }
        else if (dims[0].f.Kind == FieldKind.Date || dims[0].f.Sort is not null) parts.Add(DimOrder(0, "ASC"));
        else parts.Add("m0 DESC");

        // Deterministic tiebreak
        for (var i = 0; i < dims.Count; i++) parts.Add(DimOrder(i, "ASC"));
        return string.Join(", ", parts.Distinct());
    }

    [GeneratedRegex(@"^(d|m)(\d)(c)?$")]
    private static partial Regex SortKey();

    // ── RLS ──────────────────────────────────────────────────────────────────

    private static string? ScopeSql(Ds ds, BranchScope? scope, Ctx c)
    {
        if (scope is null || ds.National) return null;
        var t = ds.Scope ?? throw new InvalidOperationException($"Analytics dataset '{ds.Key}' has no scope rule.");
        if (t.Contains("@scFolder") && scope.Folder is null)
            throw new SpecException("Your branch is not mapped to a site, so this dataset is not available to you.");

        c.P.Add(NewParam("@scSite", SqlDbType.NVarChar, scope.SiteCode));
        c.P.Add(NewParam("@scFolder", SqlDbType.NVarChar, scope.Folder ?? ""));
        c.P.Add(NewParam("@scWhN", SqlDbType.Int, scope.WhseNos.Count));
        var wh = new List<string>();
        for (var i = 0; i < scope.WhseNos.Count; i++)
        {
            c.P.Add(NewParam($"@scWh{i}", SqlDbType.Int, scope.WhseNos[i]));
            wh.Add($"@scWh{i}");
        }
        var list = wh.Count == 0 ? "(NULL)" : $"({string.Join(",", wh)})";
        return ScWhToken().Replace(t, list);
    }

    [GeneratedRegex(@"@scWh\b")]
    private static partial Regex ScWhToken();

    // ── filters ──────────────────────────────────────────────────────────────

    private static (string? Sql, DateRange? Range, string FieldKey) FilterSql(Ds ds, SpecFilter flt, Ctx c, DateOnly today)
    {
        var f = RequireField(ds, flt.Field);
        if (f is { Kind: FieldKind.Measure, Agg: "custom" })
            throw new SpecException($"'{f.Label}' is a calculated value and can't be filtered.");
        var op = flt.Op?.Trim() ?? "";
        var vals = flt.Values ?? [];
        if (vals.Count > MaxInValues) throw new SpecException($"At most {MaxInValues} filter values are allowed.");
        if (op == "any") return (null, null, f.Key);

        c.Need(f.Joins);
        var e = f.Kind == FieldKind.Date ? DateExpr(f) : f.Sql;
        var t = f.Kind == FieldKind.Date ? "date" : f.Type;

        string P(string? v) => c.Add(DbType(t), ParseValue(v!, t, f.Label));
        string One()
        {
            if (vals is not [var v] || v is null) throw new SpecException($"Filter on '{f.Label}' needs one value.");
            return P(v);
        }

        switch (op)
        {
            case "eq":
                if (vals is [null]) return ($"{e} IS NULL", null, f.Key);
                return ($"{e} = {One()}", null, f.Key);
            case "neq":
                if (vals is [null]) return ($"{e} IS NOT NULL", null, f.Key);
                return ($"{e} <> {One()} OR {e} IS NULL", null, f.Key);
            case "in":
            case "notIn":
            {
                if (vals.Count == 0) return (null, null, f.Key);   // empty list = no filter (e.g. "all branches")
                var hasNull = vals.Any(v => v is null);
                var names = vals.Where(v => v is not null).Select(P).ToList();
                var inList = names.Count > 0 ? $"{e} IN ({string.Join(", ", names)})" : null;
                if (op == "in")
                    return (hasNull ? (inList is null ? $"{e} IS NULL" : $"{inList} OR {e} IS NULL") : inList, null, f.Key);
                var notIn = names.Count > 0 ? $"{e} NOT IN ({string.Join(", ", names)})" : null;
                return (hasNull
                    ? (notIn is null ? $"{e} IS NOT NULL" : $"{e} IS NOT NULL AND {notIn}")
                    : $"{e} IS NULL OR {notIn}", null, f.Key);
            }
            case "gt": return ($"{e} > {One()}", null, f.Key);
            case "gte": return ($"{e} >= {One()}", null, f.Key);
            case "lt": return ($"{e} < {One()}", null, f.Key);
            case "lte": return ($"{e} <= {One()}", null, f.Key);
            case "between":
            {
                if (vals is not [var a, var b] || a is null || b is null)
                    throw new SpecException($"'Between' on '{f.Label}' needs two values.");
                DateRange? range = t == "date" ? new DateRange(ParseDate(a), ParseDate(b)) : null;
                return ($"{e} BETWEEN {P(a)} AND {P(b)}", range, f.Key);
            }
            case "contains":
            case "startsWith":
            {
                if (t != "string") throw new SpecException($"'{op}' only works on text fields.");
                if (vals is not [var v] || string.IsNullOrEmpty(v)) throw new SpecException($"Filter on '{f.Label}' needs a value.");
                var pattern = (op == "contains" ? "%" : "") + EscapeLike(Truncate(v, MaxValueLength)) + "%";
                return ($"{e} LIKE {c.Add(SqlDbType.NVarChar, pattern)}", null, f.Key);
            }
            case "isNull": return ($"{e} IS NULL", null, f.Key);
            case "notNull": return ($"{e} IS NOT NULL", null, f.Key);
            case "preset":
            {
                if (t != "date") throw new SpecException($"Date presets only work on date fields.");
                if (vals is not [var p] || p is null || !Presets.Contains(p)) throw new SpecException($"Unknown date preset.");
                var r = ResolvePreset(p, today);
                return ($"{e} BETWEEN {c.Add(SqlDbType.Date, r.From.ToDateTime(TimeOnly.MinValue))} AND {c.Add(SqlDbType.Date, r.To.ToDateTime(TimeOnly.MinValue))}", r, f.Key);
            }
            default:
                throw new SpecException($"Unknown filter operator '{flt.Op}'.");
        }
    }

    private static SqlDbType DbType(string t) => t switch
    {
        "int" => SqlDbType.Int,
        "decimal" => SqlDbType.Decimal,
        "date" => SqlDbType.Date,
        "bool" => SqlDbType.Bit,
        _ => SqlDbType.NVarChar,
    };

    private static object ParseValue(string v, string type, string label)
    {
        try
        {
            return type switch
            {
                "int" => int.Parse(v, NumberStyles.Integer, CultureInfo.InvariantCulture),
                "decimal" => decimal.Parse(v, NumberStyles.Number, CultureInfo.InvariantCulture),
                "date" => ParseDate(v).ToDateTime(TimeOnly.MinValue),
                "bool" => v.Trim().ToLowerInvariant() switch
                {
                    "true" or "1" or "yes" => true,
                    "false" or "0" or "no" => false,
                    _ => throw new FormatException(),
                },
                _ => v.Length <= MaxValueLength ? v : throw new SpecException($"Filter value for '{label}' is too long."),
            };
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new SpecException($"'{Truncate(v, 30)}' is not a valid value for '{label}'.");
        }
    }

    private static DateOnly ParseDate(string v)
    {
        if (!DateOnly.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) || d.Year < 1900)
            throw new SpecException($"'{Truncate(v, 30)}' is not a valid date (yyyy-MM-dd).");
        return d;
    }

    private static string EscapeLike(string v) => v.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");

    private static string Truncate(string v, int max) => v.Length <= max ? v : v[..max];

    // ── dates ────────────────────────────────────────────────────────────────

    public static DateRange ResolvePreset(string preset, DateOnly t)
    {
        var som = new DateOnly(t.Year, t.Month, 1);
        return preset switch
        {
            "today" => new(t, t),
            "yesterday" => new(t.AddDays(-1), t.AddDays(-1)),
            "last7" => new(t.AddDays(-6), t),
            "last30" => new(t.AddDays(-29), t),
            "mtd" => new(som, t),
            "lastMonth" => new(som.AddMonths(-1), som.AddDays(-1)),
            "qtd" => new(new DateOnly(t.Year, (t.Month - 1) / 3 * 3 + 1, 1), t),
            "ytd" => new(new DateOnly(t.Year, 1, 1), t),
            "last12m" => new(som.AddMonths(-11), t),
            _ => throw new SpecException("Unknown date preset."),
        };
    }

    private static string PresetLabel(string p) => p switch
    {
        "today" => "Today",
        "yesterday" => "Yesterday",
        "last7" => "Last 7 days",
        "last30" => "Last 30 days",
        "mtd" => "Month to date",
        "lastMonth" => "Last month",
        "qtd" => "Quarter to date",
        "ytd" => "Year to date",
        _ => "Last 12 months",
    };

    private static DateOnly EndOfMonth(DateOnly d) => new DateOnly(d.Year, d.Month, 1).AddMonths(1).AddDays(-1);

    public static DateOnly BucketStart(DateOnly d, string bucket) => bucket switch
    {
        "week" => d.AddDays(-(((int)d.DayOfWeek + 6) % 7)),
        "month" => new DateOnly(d.Year, d.Month, 1),
        "quarter" => new DateOnly(d.Year, (d.Month - 1) / 3 * 3 + 1, 1),
        "year" => new DateOnly(d.Year, 1, 1),
        _ => d,
    };

    public static DateOnly NextBucket(DateOnly d, string bucket) => bucket switch
    {
        "week" => d.AddDays(7),
        "month" => d.AddMonths(1),
        "quarter" => d.AddMonths(3),
        "year" => d.AddYears(1),
        _ => d.AddDays(1),
    };

    public static ReportSpecDto Clone(ReportSpecDto s) => new()
    {
        V = s.V, Dataset = s.Dataset, Type = s.Type, Stacked = s.Stacked, Horizontal = s.Horizontal,
        Dimensions = [.. s.Dimensions], Measures = [.. s.Measures], Filters = [.. s.Filters],
        Sort = s.Sort, Limit = s.Limit, Others = s.Others, Fill = s.Fill, Compare = s.Compare,
    };

    // ── post-processing ──────────────────────────────────────────────────────

    /// <summary>
    /// Splits GROUPING_ID rows into detail / subtotals / total, applies top-N +
    /// exact "(Others)" (total − Σ kept, additive measures only) and zero-fill.
    /// Raw rows carry g as their last element when DimCount &gt; 0.
    /// </summary>
    public static AnalyticsResultDto Shape(BuiltQuery q, List<object?[]> raw, ReportSpecDto spec, int cap)
    {
        var res = new AnalyticsResultDto { Columns = q.Columns };
        var width = q.Columns.Count;
        var nd = q.DimCount;

        if (raw.Count > cap) { res.Truncated = true; raw = raw.Take(cap).ToList(); }

        if (nd == 0)
        {
            res.Rows = raw;
            res.Total = raw.FirstOrDefault();
            return res;
        }

        var full = (1 << nd) - 1;
        List<object?[]> detail = [], rowTotals = [], colTotals = [];
        // Column indexes per dimension (key + optional caption), to blank the
        // dimensions a subtotal/total row aggregated away (MAX(caption) would
        // otherwise leak an arbitrary caption into total rows).
        var dimCols = Enumerable.Range(0, nd)
            .Select(i => q.Columns.Select((c, idx) => (c, idx)).Where(x => x.c.Key == $"d{i}" || x.c.Key == $"d{i}c").Select(x => x.idx).ToArray())
            .ToArray();
        foreach (var r in raw)
        {
            var g = Convert.ToInt32(r[width], CultureInfo.InvariantCulture);
            var row = r[..width];
            for (var i = 0; i < nd; i++)
                if ((g & (1 << (nd - 1 - i))) != 0)
                    foreach (var idx in dimCols[i]) row[idx] = null;
            if (g == 0) detail.Add(row);
            else if (g == full) res.Total = row;
            else if (q.Subtotals && g == 1) rowTotals.Add(row);
            else if (q.Subtotals && g == 2) colTotals.Add(row);
        }

        var measureIdx = q.Columns.Select((c, i) => (c, i)).Where(x => x.c.Role == "measure").Select(x => x.i).ToArray();
        var d0 = q.Columns.FindIndex(c => c.Key == "d0");
        var d0c = q.Columns.FindIndex(c => c.Key == "d0c");
        var d1 = q.Columns.FindIndex(c => c.Key == "d1");

        object?[] OthersRow(object?[] template, object?[]? totalRow, IEnumerable<object?[]> kept)
        {
            var row = new object?[width];
            Array.Copy(template, row, width);
            row[d0] = "(Others)";
            if (d0c >= 0) row[d0c] = "(Others)";
            var keptList = kept.ToList();
            foreach (var mi in measureIdx)
                row[mi] = totalRow?[mi] is null ? null : ToDec(totalRow[mi]) - keptList.Sum(k => ToDec(k[mi]));
            return row;
        }

        if (spec.Limit is int limit)
        {
            if (q.Subtotals)
            {
                var keepKeys = rowTotals.Take(limit).Select(r => Key(r[d0])).ToHashSet();
                if (rowTotals.Count > limit)
                {
                    var keptDetail = detail.Where(r => keepKeys.Contains(Key(r[d0]))).ToList();
                    if (spec.Others && q.AllAdditive)
                        foreach (var ct in colTotals)
                            res.OthersRows.Add(OthersRow(ct, ct, keptDetail.Where(r => Key(r[d1]) == Key(ct[d1]))));
                    detail = keptDetail;
                    rowTotals = rowTotals.Take(limit).ToList();
                }
            }
            else if (detail.Count > limit)
            {
                var kept = detail.Take(limit).ToList();
                if (spec.Others && q.AllAdditive && res.Total is not null)
                {
                    var template = new object?[width];
                    res.OthersRows.Add(OthersRow(template, res.Total, kept));
                }
                detail = kept;
            }
        }

        // Zero-fill a single date dimension so trend lines have no silent gaps.
        if (spec.Fill && nd == 1 && q.Columns[d0] is { Type: "date", Bucket: { } bucket } && spec.Limit is null)
        {
            var dates = detail.Select(r => r[d0]).OfType<DateOnly>().ToList();
            DateRange? range = q.Dim0Range ?? (dates.Count > 0 ? new DateRange(dates.Min(), dates.Max()) : null);
            if (range is { } rg)
            {
                var start = BucketStart(rg.From, bucket);
                var count = 0;
                for (var dt = start; dt <= rg.To; dt = NextBucket(dt, bucket)) count++;
                if (count <= MaxFillBuckets)
                {
                    var byDate = detail.Where(r => r[d0] is DateOnly).ToDictionary(r => (DateOnly)r[d0]!);
                    var filled = new List<object?[]>();
                    for (var dt = start; dt <= rg.To; dt = NextBucket(dt, bucket))
                    {
                        if (byDate.TryGetValue(dt, out var existing)) { filled.Add(existing); continue; }
                        var row = new object?[width];
                        row[d0] = dt;
                        foreach (var mi in measureIdx) row[mi] = q.Columns[mi].Additive ? 0m : null;
                        filled.Add(row);
                    }
                    var descending = spec.Sort is { By: "d0" } s && string.Equals(s.Dir, "desc", StringComparison.OrdinalIgnoreCase);
                    if (descending) filled.Reverse();
                    // keep "(invalid date)" rows
                    filled.AddRange(detail.Where(r => r[d0] is null));
                    detail = filled;
                }
            }
        }

        res.Rows = detail;
        if (q.Subtotals) { res.RowTotals = rowTotals; res.ColTotals = colTotals; }
        return res;

        static string Key(object? v) => v switch
        {
            null => "\0",
            DateOnly d => d.ToString("yyyy-MM-dd"),
            _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "\0",
        };
    }

    private static decimal ToDec(object? v) => v is null ? 0m : Convert.ToDecimal(v, CultureInfo.InvariantCulture);
}
