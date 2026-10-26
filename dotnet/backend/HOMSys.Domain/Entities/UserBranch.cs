namespace HOMSys.Domain.Entities;

/// <summary>Admin-controlled list of sites an Invoice Custodian may encode collections for (usually one).</summary>
public class UserBranch
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int SiteId { get; set; }
    public Site Site { get; set; } = null!;
}
