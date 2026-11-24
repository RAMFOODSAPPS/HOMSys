namespace HOMSys.Domain.Entities;

/// <summary>
/// Servicing branches of a user (Users page; user decision 2026-11-24): the branches a BA covers. A TPAR encoder with
/// any picks only those branches on a TPAR. Separate from <see cref="UserBranch"/> (Invoice Custodian collection branches).
/// </summary>
public class UserServicingBranch
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int SiteId { get; set; }
    public Site Site { get; set; } = null!;
}
