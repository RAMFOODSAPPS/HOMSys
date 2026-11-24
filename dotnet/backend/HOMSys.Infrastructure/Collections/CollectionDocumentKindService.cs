using System.Text;
using HOMSys.Application.DTOs.Collections;
using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Collections;

/// <summary>
/// Supporting-document types for collections (user 2026-11-23), maintained by the Finance Manager. A file stores the
/// type's Code, so renaming the label relabels every file; a type in use can only be deactivated (kept for its files).
/// A type limited to receipt types is offered only for those (the receipt's own copy: Collection Receipt only on a CR).
/// </summary>
public class CollectionDocumentKindService(AppDbContext db)
{
    private static readonly string[] ReceiptCodes = ["CR", "PR", "AR"];

    public async Task<List<CollectionDocumentKindDto>> ListAsync(bool includeInactive)
    {
        var kinds = await db.CollectionDocumentKinds.Where(k => includeInactive || k.IsActive)
            .OrderBy(k => k.SortOrder).ThenBy(k => k.Label).ToListAsync();
        var codes = kinds.Select(k => k.Code).ToList();
        var used = await db.CollectionAttachments.Where(a => codes.Contains(a.Kind))
            .GroupBy(a => a.Kind).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        return kinds.Select(k => ToDto(k, used.GetValueOrDefault(k.Code))).ToList();
    }

    public async Task<(CollectionDocumentKindDto? dto, string? error)> CreateAsync(SaveDocumentKindDto dto, string user)
    {
        var k = new CollectionDocumentKind();
        if (await ApplyAsync(k, dto, user) is { } error) return (null, error);
        k.Code = await NewCodeAsync(k.Label);
        db.CollectionDocumentKinds.Add(k);
        await db.SaveChangesAsync();
        return (ToDto(k, 0), null);
    }

    public async Task<(CollectionDocumentKindDto? dto, string? error)> UpdateAsync(int id, SaveDocumentKindDto dto, string user)
    {
        var k = await db.CollectionDocumentKinds.FindAsync(id);
        if (k is null) return (null, "Document type not found.");
        if (await ApplyAsync(k, dto, user) is { } error) return (null, error);
        await db.SaveChangesAsync();
        return (ToDto(k, await db.CollectionAttachments.CountAsync(a => a.Kind == k.Code)), null);
    }

    public async Task<string?> DeleteAsync(int id)
    {
        var k = await db.CollectionDocumentKinds.FindAsync(id);
        if (k is null) return "Document type not found.";
        if (k.IsSystem) return $"\"{k.Label}\" is used by the system and can't be deleted.";
        var n = await db.CollectionAttachments.CountAsync(a => a.Kind == k.Code);
        if (n > 0) return $"{n} file(s) are labelled \"{k.Label}\" — deactivate it instead.";
        db.CollectionDocumentKinds.Remove(k);
        await db.SaveChangesAsync();
        return null;
    }

    private async Task<string?> ApplyAsync(CollectionDocumentKind k, SaveDocumentKindDto dto, string user)
    {
        var label = (dto.Label ?? "").Trim();
        if (label.Length == 0) return "Enter the document type's name.";
        if (label.Length > 60) return "The name is too long (60 characters at most).";
        if (await db.CollectionDocumentKinds.AnyAsync(x => x.Id != k.Id && x.Label == label)) return $"\"{label}\" already exists.";
        if (Codes(dto.ReceiptTypes) is not { } types) return "Receipt types are CR, PR and AR.";
        if (k.IsSystem && !dto.IsActive) return $"\"{k.Label}\" is used by the system and stays active.";
        if (k.IsSystem && types.Count > 0) return $"\"{k.Label}\" is used by the system and stays offered for every receipt type.";

        k.Label = label;
        k.ReceiptTypes = string.Join(",", types);
        k.SortOrder = Math.Clamp(dto.SortOrder, 0, 9999);
        k.IsActive = dto.IsActive;
        k.UpdatedAt = DateTime.UtcNow;
        k.UpdatedBy = user;
        return null;
    }

    /// <summary>CR / PR / AR in their usual order; null when any isn't one.</summary>
    private static List<string>? Codes(List<string>? list)
    {
        var picked = (list ?? []).Select(x => x.Trim().ToUpperInvariant()).Where(x => x.Length > 0).Distinct().ToList();
        return picked.Any(x => !ReceiptCodes.Contains(x)) ? null : ReceiptCodes.Where(picked.Contains).ToList();
    }

    private static List<string> Split(string csv) => csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    /// <summary>A code from the label ("Proof of Delivery" → "ProofOfDelivery"), made unique.</summary>
    private async Task<string> NewCodeAsync(string label)
    {
        var sb = new StringBuilder();
        foreach (var word in label.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var w = new string(word.Where(char.IsLetterOrDigit).ToArray());
            if (w.Length > 0) sb.Append(char.ToUpperInvariant(w[0])).Append(w[1..]);
        }
        var stem = sb.Length == 0 ? "Doc" : sb.ToString();
        stem = stem[..Math.Min(stem.Length, 26)];
        var code = stem;
        for (var i = 2; await db.CollectionDocumentKinds.AnyAsync(x => x.Code == code) || code == AttachmentKinds.Unspecified; i++) code = $"{stem}{i}";
        return code;
    }

    private static CollectionDocumentKindDto ToDto(CollectionDocumentKind k, int inUse) =>
        new(k.Id, k.Code, k.Label, Split(k.ReceiptTypes), k.SortOrder, k.IsActive, k.IsSystem, inUse);
}
