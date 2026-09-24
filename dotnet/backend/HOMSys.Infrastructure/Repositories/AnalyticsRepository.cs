using HOMSys.Application.DTOs.Analytics;
using HOMSys.Application.Interfaces;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Analytics;
using HOMSys.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HOMSys.Infrastructure.Repositories;

/// <summary>
/// Runs AnalyticsSqlBuilder output on its own SqlConnection (not EF's), so
/// every dashboard widget request is independent of the scoped DbContext.
/// Uses ConnectionStrings:Analytics when configured (recommended: a
/// db_datareader-only login), else DefaultConnection.
/// </summary>
public class AnalyticsRepository(AppDbContext db, IConfiguration config, ILogger<AnalyticsRepository> logger) : IAnalyticsRepository
{
    private const int CommandTimeoutSeconds = 30;

    private string ConnectionString =>
        config.GetConnectionString("Analytics") is { Length: > 0 } cs ? cs : config.GetConnectionString("DefaultConnection")!;

    private static readonly IReadOnlyList<DatasetDto> DatasetDtos = AnalyticsCatalog.All.Select(d => new DatasetDto(
        d.Key, d.Label, d.Grain, d.DefaultDate, d.Heavy, d.National,
        d.Fields.Select(f => new FieldDto(
            f.Key, f.Label, f.Kind.ToString().ToLowerInvariant(), f.Kind == FieldKind.Date ? "date" : f.Type,
            AnalyticsSqlBuilder.AllowedAggs(f), AnalyticsSqlBuilder.DefaultAgg(f), f.Format,
            f.Caption is not null, f.DrillTo, f.Desc, f.Additive)).ToList(),
        d.Detail ?? [], d.Defaults ?? [], d.Permission)).ToList();

    public IReadOnlyList<DatasetDto> Datasets => DatasetDtos;

    public async Task<(AnalyticsResultDto? Result, string? Error)> QueryAsync(ReportSpecDto spec, BranchScope? scope, DateOnly today, int cap)
    {
        if (!AnalyticsCatalog.ByKey.TryGetValue(spec.Dataset ?? "", out var ds))
            return (null, $"Unknown dataset '{spec.Dataset}'.");
        try
        {
            var q = AnalyticsSqlBuilder.Build(ds, spec, scope, today, cap);
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            var raw = await ReadAsync(conn, q);
            var result = AnalyticsSqlBuilder.Shape(q, raw, spec, cap);
            result.Scope = scope?.SiteCode;

            if (spec.Compare == "previousPeriod" && q.DimCount == 0 && AnalyticsSqlBuilder.PreviousPeriod(ds, spec, today) is { } prev)
            {
                var pq = AnalyticsSqlBuilder.Build(ds, prev.Spec, scope, today, cap);
                result.Compare = (await ReadAsync(conn, pq)).FirstOrDefault();
                result.CompareLabel = prev.Label;
            }

            if (ds.AsOfSql is not null)
            {
                await using var cmd = new SqlCommand(ds.AsOfSql, conn) { CommandTimeout = CommandTimeoutSeconds };
                if (await cmd.ExecuteScalarAsync() is DateTime asOf)
                    result.AsOf = DateTime.SpecifyKind(asOf, DateTimeKind.Utc);
            }
            return (result, null);
        }
        catch (SpecException ex)
        {
            return (null, ex.Message);
        }
        catch (SqlException ex) when (ex.Number == -2)
        {
            return (null, "The query took too long. Add filters or reduce the number of fields.");
        }
    }

    public async Task<(List<ValueOptionDto>? Values, string? Error)> ValuesAsync(
        string dataset, string field, string? q, int take, BranchScope? scope, DateOnly today)
    {
        if (!AnalyticsCatalog.ByKey.TryGetValue(dataset, out var ds))
            return (null, $"Unknown dataset '{dataset}'.");
        try
        {
            var built = AnalyticsSqlBuilder.BuildValues(ds, field, q, take, scope, today);
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            var rows = await ReadAsync(conn, built);
            return (rows.Select(r => new ValueOptionDto(Str(r[0]), Str(r[1]))).ToList(), null);
        }
        catch (SpecException ex)
        {
            return (null, ex.Message);
        }
    }

    public string? Validate(ReportSpecDto spec, DateOnly today)
    {
        if (!AnalyticsCatalog.ByKey.TryGetValue(spec.Dataset ?? "", out var ds))
            return $"Unknown dataset '{spec.Dataset}'.";
        try
        {
            AnalyticsSqlBuilder.Build(ds, spec, null, today, 1);
            return null;
        }
        catch (SpecException ex)
        {
            return ex.Message;
        }
    }

    public string Describe(ReportSpecDto spec, DateOnly today) =>
        AnalyticsCatalog.ByKey.TryGetValue(spec.Dataset ?? "", out var ds)
            ? AnalyticsSqlBuilder.Describe(ds, spec, today)
            : "";

    private async Task<List<object?[]>> ReadAsync(SqlConnection conn, BuiltQuery q)
    {
        await using var cmd = new SqlCommand(q.Sql, conn) { CommandTimeout = CommandTimeoutSeconds };
        cmd.Parameters.AddRange(q.Params.Select(p => ((ICloneable)p).Clone()).ToArray());
        logger.LogDebug("Analytics SQL ({Params}):\n{Sql}", string.Join(", ", q.Params.Select(p => p.ParameterName)), q.Sql);

        var rows = new List<object?[]>();
        await using var reader = await cmd.ExecuteReaderAsync();
        var isDate = Enumerable.Range(0, reader.FieldCount)
            .Select(i => string.Equals(reader.GetDataTypeName(i), "date", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        while (await reader.ReadAsync())
        {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++)
            {
                var v = reader.GetValue(i);
                row[i] = v is DBNull ? null : isDate[i] && v is DateTime dt ? DateOnly.FromDateTime(dt) : v;
            }
            rows.Add(row);
        }
        return rows;
    }

    private static string? Str(object? v) => v switch
    {
        null => null,
        DateOnly d => d.ToString("yyyy-MM-dd"),
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => v.ToString(),
    };

    // ── SavedReports CRUD ────────────────────────────────────────────────────

    public async Task<List<SavedReport>> GetItemsAsync() =>
        await db.SavedReports.AsNoTracking().OrderBy(r => r.Name).ToListAsync();

    public async Task<SavedReport?> GetItemAsync(int id) =>
        await db.SavedReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);

    public async Task<SavedReport> CreateItemAsync(SavedReport item)
    {
        db.SavedReports.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task UpdateItemAsync(SavedReport item)
    {
        db.ChangeTracker.Clear();
        await db.SavedReports.Where(r => r.Id == item.Id).ExecuteUpdateAsync(x => x
            .SetProperty(r => r.Name, item.Name)
            .SetProperty(r => r.Description, item.Description)
            .SetProperty(r => r.DatasetKey, item.DatasetKey)
            .SetProperty(r => r.Visual, item.Visual)
            .SetProperty(r => r.DefinitionJson, item.DefinitionJson)
            .SetProperty(r => r.IsSystem, item.IsSystem)
            .SetProperty(r => r.SharedRoleIds, item.SharedRoleIds)
            .SetProperty(r => r.UpdatedAt, item.UpdatedAt)
            .SetProperty(r => r.UpdatedBy, item.UpdatedBy));
    }

    public async Task DeleteItemAsync(int id) =>
        await db.SavedReports.Where(r => r.Id == id).ExecuteDeleteAsync();

    public async Task<Dictionary<int, string>> GetUserNamesAsync(IEnumerable<int> userIds)
    {
        var ids = userIds.Distinct().ToList();
        return await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.FirstName + " " + u.LastName).Trim() == "" ? u.Username : (u.FirstName + " " + u.LastName).Trim());
    }
}
