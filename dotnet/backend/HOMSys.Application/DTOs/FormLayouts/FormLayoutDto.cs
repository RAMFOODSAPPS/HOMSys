using System.Text.Json;

namespace HOMSys.Application.DTOs.FormLayouts;

/// <summary>Layout is passed through as raw JSON — its shape belongs to the frontend designer.</summary>
public record FormLayoutDto(string FormKey, JsonElement Layout, DateTime UpdatedAt, string UpdatedBy);

public record SaveFormLayoutRequest(JsonElement Layout);
