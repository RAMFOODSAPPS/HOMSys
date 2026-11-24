namespace HOMSys.Application.DTOs.Collections;

/// <summary>A supporting-document type. ReceiptTypes: offered for CR / PR / AR (empty = every type).
/// InUse = files labelled with it (it can then only be deactivated, not deleted).</summary>
public record CollectionDocumentKindDto(int Id, string Code, string Label, List<string> ReceiptTypes,
    int SortOrder, bool IsActive, bool IsSystem, int InUse);

public record SaveDocumentKindDto(string? Label, List<string>? ReceiptTypes, int SortOrder, bool IsActive);
