using HOMSys.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Data;

/// <summary>Admin control of a user's servicing branches (the branches a BA covers — used by the TPAR page).</summary>
public class UserServicingBranchService(AppDbContext db)
{
    public Task<List<int>> GetAsync(int userId) =>
        db.UserServicingBranches.Where(b => b.UserId == userId).Select(b => b.SiteId).OrderBy(x => x).ToListAsync();

    public async Task<string?> SetAsync(int userId, IEnumerable<int> siteIds)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId)) return "User not found.";
        var wanted = siteIds.Distinct().ToHashSet();
        if (await db.Sites.CountAsync(s => wanted.Contains(s.Id)) != wanted.Count) return "Unknown branch.";

        var current = await db.UserServicingBranches.Where(b => b.UserId == userId).ToListAsync();
        db.UserServicingBranches.RemoveRange(current.Where(b => !wanted.Contains(b.SiteId)));
        foreach (var id in wanted.Except(current.Select(b => b.SiteId)))
            db.UserServicingBranches.Add(new UserServicingBranch { UserId = userId, SiteId = id });
        await db.SaveChangesAsync();
        return null;
    }
}
