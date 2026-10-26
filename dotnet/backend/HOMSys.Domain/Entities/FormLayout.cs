namespace HOMSys.Domain.Entities;

/// <summary>
/// Admin-designed field arrangement for one form (Form Designer). One row per form;
/// no row = the form's built-in default layout. LayoutJson is owned by the frontend.
/// </summary>
public class FormLayout
{
    public int Id { get; set; }
    /// <summary>e.g. "tasaf".</summary>
    public string FormKey { get; set; } = string.Empty;
    public string LayoutJson { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}
