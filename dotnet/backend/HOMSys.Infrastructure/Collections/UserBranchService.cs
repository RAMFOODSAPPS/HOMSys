using HOMSys.Domain.Entities;
using HOMSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Collections;

/// <summary>Admin control of which branches an Invoice Custodian may encode collections for.</summary>
public class UserBranchService(AppDbContext db)
{
    public Task<List<int>> GetAsync(int userId) =>
        db.UserBranches.Where(b => b.UserId == userId).Select(b => b.SiteId).OrderBy(x => x).ToListAsync();

    public async Task<string?> SetAsync(int userId, IEnumerable<int> siteIds)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId)) return "User not found.";
        var wanted = siteIds.Distinct().ToHashSet();
        if (await db.Sites.CountAsync(s => wanted.Contains(s.Id)) != wanted.Count) return "Unknown branch.";

        var current = await db.UserBranches.Where(b => b.UserId == userId).ToListAsync();
        db.UserBranches.RemoveRange(current.Where(b => !wanted.Contains(b.SiteId)));
        foreach (var id in wanted.Except(current.Select(b => b.SiteId)))
            db.UserBranches.Add(new UserBranch { UserId = userId, SiteId = id });
        await db.SaveChangesAsync();
        return null;
    }
}
