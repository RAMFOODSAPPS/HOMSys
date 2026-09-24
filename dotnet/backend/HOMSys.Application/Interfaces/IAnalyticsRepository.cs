using HOMSys.Application.DTOs.Analytics;
using HOMSys.Domain.Entities;

namespace HOMSys.Application.Interfaces;

/// <summary>
/// Data Analytics: the dataset catalog + query engine (Infrastructure\Analytics)
/// and SavedReports CRUD. Engine methods return (result, error) where error is
/// a user-safe message for a spec the catalog rejects.
/// </summary>
public interface IAnalyticsRepository
{
    IReadOnlyList<DatasetDto> Datasets { get; }

    Task<(AnalyticsResultDto? Result, string? Error)> QueryAsync(ReportSpecDto spec, BranchScope? scope, DateOnly today, int cap);
    Task<(List<ValueOptionDto>? Values, string? Error)> ValuesAsync(string dataset, string field, string? q, int take, BranchScope? scope, DateOnly today);

    /// <summary>Builds (never executes) the spec; null when valid.</summary>
    string? Validate(ReportSpecDto spec, DateOnly today);

    /// <summary>Human-readable filter summary for export headers.</summary>
    string Describe(ReportSpecDto spec, DateOnly today);

    Task<List<SavedReport>> GetItemsAsync();
    Task<SavedReport?> GetItemAsync(int id);
    Task<SavedReport> CreateItemAsync(SavedReport item);
    Task UpdateItemAsync(SavedReport item);
    Task DeleteItemAsync(int id);
    Task<Dictionary<int, string>> GetUserNamesAsync(IEnumerable<int> userIds);
}
