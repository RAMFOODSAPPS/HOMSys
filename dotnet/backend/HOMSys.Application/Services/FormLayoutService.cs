using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using HOMSys.Application.DTOs.FormLayouts;
using HOMSys.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace HOMSys.Application.Services;

/// <summary>Form Designer layouts: anyone signed in reads them, Admins save/reset (enforced in the controller).</summary>
public partial class FormLayoutService(IFormLayoutRepository repo, IHttpContextAccessor http)
{
    private const int MaxJsonLength = 100_000;

    private string CurrentUser =>
        http.HttpContext?.User?.FindFirstValue(ClaimTypes.Name) ?? "system";

    [GeneratedRegex("^[a-z0-9-]{1,50}$")]
    private static partial Regex FormKeyPattern();

    public async Task<(FormLayoutDto? dto, string? error)> GetAsync(string formKey)
    {
        if (!FormKeyPattern().IsMatch(formKey)) return (null, "Invalid form key.");
        var row = await repo.GetAsync(formKey);
        if (row is null) return (null, null);
        using var doc = JsonDocument.Parse(row.LayoutJson);
        return (new FormLayoutDto(row.FormKey, doc.RootElement.Clone(), row.UpdatedAt, row.UpdatedBy), null);
    }

    public async Task<(FormLayoutDto? dto, string? error)> SaveAsync(string formKey, SaveFormLayoutRequest req)
    {
        if (!FormKeyPattern().IsMatch(formKey)) return (null, "Invalid form key.");
        if (req.Layout.ValueKind != JsonValueKind.Object ||
            !req.Layout.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return (null, "Layout must be an object with an items array.");
        var json = req.Layout.GetRawText();
        if (json.Length > MaxJsonLength) return (null, "Layout is too large.");

        await repo.UpsertAsync(formKey, json, CurrentUser);
        return await GetAsync(formKey);
    }

    public async Task<string?> ResetAsync(string formKey)
    {
        if (!FormKeyPattern().IsMatch(formKey)) return "Invalid form key.";
        await repo.DeleteAsync(formKey);
        return null;
    }
}
