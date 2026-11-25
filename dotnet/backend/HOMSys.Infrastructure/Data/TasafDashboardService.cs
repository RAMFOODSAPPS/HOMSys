using System.Security.Claims;
using HOMSys.Application.DTOs.Tasaf;
using HOMSys.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Data;

/// <summary>
/// TASAF Dashboard (user request 2026-11-24, "TASAF Next Steps as of Nov 23.xlsx" › dashboard): per year, approved annual
/// budget vs TASAFs created / approved / completed (with TPAR) / closed and the balance, by sales group and by account
/// chain. A TASAF belongs to the year of its start date (else the Manila year it was created). Sales groups flagged
/// DashboardByArea (LKA, managed by region) show one row per Area in the sales-group view — the TASAF's own Area field.
/// </summary>
public class TasafDashboardService(AppDbContext db, IHttpContextAccessor http)
{
    private ClaimsPrincipal? User => http.HttpContext?.User;
    private bool CanEditBudget => User?.IsInRole("Admin") == true || User?.HasClaim("permission", "tasaf-masters") == true;

    private sealed record Item(string Status, int? SalesGroupId, decimal Total, decimal TparApproved, decimal Actual, List<int> Chains, List<int> Areas);

    public async Task<TasafDashboardDto> GetAsync(int? year)
    {
        var y = year ?? DateTime.UtcNow.AddHours(8).Year;

        var all = await db.Tasafs.AsNoTracking()
            .Where(t => t.Status != TasafStatus.Draft && t.Status != TasafStatus.Cancelled)
            .Select(t => new
            {
                t.Status, t.SalesGroupId, t.TotalCost, t.StartDate, t.CreatedAt,
                Tpar = t.Tpars.Where(p => p.Status == TasafTparStatus.Approved).Sum(p => (decimal?)p.Amount) ?? 0,
                Actual = t.PostEval != null ? t.PostEval.ActualCost ?? 0 : 0,
                Chains = t.Chains.Select(c => c.ChainId).ToList(),
                Areas = t.Areas.Select(a => a.AreaId).ToList()
            })
            .ToListAsync();
        int YearOf(DateOnly? start, DateTime created) => start?.Year ?? created.AddHours(8).Year;

        var items = all.Where(t => YearOf(t.StartDate, t.CreatedAt) == y)
            .Select(t => new Item(t.Status, t.SalesGroupId, t.TotalCost, t.Tpar, t.Actual, t.Chains.Distinct().ToList(), t.Areas.Distinct().ToList()))
            .ToList();

        var budgets = await db.TasafBudgets.AsNoTracking().Where(b => b.Year == y).ToListAsync();
        var groups = await db.TasafSalesGroups.AsNoTracking().OrderBy(g => g.SortOrder).ThenBy(g => g.Id).ToListAsync();
        var chains = await db.TasafChains.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToListAsync();
        var areas = await db.TasafAreas.AsNoTracking().OrderBy(a => a.SortOrder).ThenBy(a => a.Id).ToListAsync();

        var years = all.Select(t => YearOf(t.StartDate, t.CreatedAt))
            .Concat(await db.TasafBudgets.Select(b => b.Year).Distinct().ToListAsync())
            .Append(DateTime.UtcNow.AddHours(8).Year).Append(y)
            .Distinct().OrderByDescending(v => v).ToList();

        // ── By account chain: every active chain (plus inactive ones with data), and "(no chain)" when needed ──
        var byChain = new List<TasafDashboardRowDto>();
        foreach (var g in groups)
        {
            var inGroup = items.Where(t => t.SalesGroupId == g.Id).ToList();
            var groupChains = chains.Where(c => c.SalesGroupId == g.Id).ToList();
            // Chains of other groups used on this group's TASAFs still count here.
            var extra = inGroup.SelectMany(t => t.Chains).Distinct()
                .Where(id => groupChains.All(c => c.Id != id))
                .Select(id => chains.FirstOrDefault(c => c.Id == id)).Where(c => c is not null).Select(c => c!);
            foreach (var c in groupChains.Concat(extra))
            {
                var row = Row(g, inGroup.Where(t => t.Chains.Contains(c.Id)), t => 1m / t.Chains.Count, Budget(budgets, g.Id, c.Id, null));
                row.ChainId = c.Id; row.Chain = c.Name;
                if (c.IsActive || row.CreatedCount > 0 || row.BudgetEntered is not null) byChain.Add(row);
            }
            var noChain = inGroup.Where(t => t.Chains.Count == 0).ToList();
            if (noChain.Count > 0) { var row = Row(g, noChain, _ => 1m, null); row.Chain = "(no chain)"; byChain.Add(row); }
        }

        // ── By sales group: one row per group, or per Area for a DashboardByArea group (LKA) ──
        var bySalesGroup = new List<TasafDashboardRowDto>();
        foreach (var g in groups)
        {
            var inGroup = items.Where(t => t.SalesGroupId == g.Id).ToList();
            if (g.DashboardByArea)
            {
                // The group's own areas (Setup › Sales Groups), in area order, plus any other area its TASAFs used.
                var own = g.AreaNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var used = inGroup.SelectMany(t => t.Areas).ToHashSet();
                foreach (var a in areas.Where(a => (a.IsActive && (own.Count == 0 || own.Contains(a.Name))) || used.Contains(a.Id)))
                {
                    var row = Row(g, inGroup.Where(t => t.Areas.Contains(a.Id)), t => 1m / t.Areas.Count, Budget(budgets, g.Id, null, a.Id));
                    row.AreaId = a.Id; row.Area = a.Name;
                    bySalesGroup.Add(row);
                }
                var noArea = inGroup.Where(t => t.Areas.Count == 0).ToList();
                if (noArea.Count > 0) { var row = Row(g, noArea, _ => 1m, null); row.Area = "(no area)"; bySalesGroup.Add(row); }
                continue;
            }
            var groupBudget = Budget(budgets, g.Id, null, null);
            if (!g.IsActive && inGroup.Count == 0 && groupBudget is null) continue;
            var groupRow = Row(g, inGroup, _ => 1m, groupBudget);
            if (groupBudget is null)
            {
                groupRow.Budget = budgets.Where(b => b.SalesGroupId == g.Id && b.ChainId != null).Sum(b => b.Amount);
                groupRow.Balance = groupRow.Budget - groupRow.ApprovedAmount;
            }
            bySalesGroup.Add(groupRow);
        }
        // TASAFs with no sales group yet.
        var noGroup = items.Where(t => t.SalesGroupId is null || groups.All(g => g.Id != t.SalesGroupId)).ToList();
        if (noGroup.Count > 0)
            bySalesGroup.Add(Row(new TasafSalesGroup { Id = 0, Code = "(no sales group)" }, noGroup, _ => 1m, null));

        // Make the two views add up to the same budget: the part of a sales group's budget not given to any of its chains
        // goes on a "(not allocated to a chain)" row (merged with "(no chain)", which holds TASAFs without a chain).
        // Negative = the chains' budgets add up to more than the group's own.
        foreach (var g in groups)
        {
            var groupBudget = bySalesGroup.Where(r => r.SalesGroupId == g.Id).Sum(r => r.Budget);
            var chainBudget = byChain.Where(r => r.SalesGroupId == g.Id && r.ChainId != null).Sum(r => r.Budget);
            var diff = groupBudget - chainBudget;
            if (diff == 0) continue;
            var rest = byChain.FirstOrDefault(r => r.SalesGroupId == g.Id && r.ChainId == null);
            if (rest is null)
            {
                rest = Row(g, [], _ => 1m, null);
                var last = byChain.FindLastIndex(r => r.SalesGroupId == g.Id);
                if (last >= 0) byChain.Insert(last + 1, rest);
                else
                {
                    // Group without chain rows: keep the sales-group order.
                    var order = groups.FindIndex(x => x.Id == g.Id);
                    var at = byChain.FindIndex(r => groups.FindIndex(x => x.Id == r.SalesGroupId) > order);
                    byChain.Insert(at < 0 ? byChain.Count : at, rest);
                }
            }
            rest.Chain = rest.CreatedCount > 0 ? "(no chain / not allocated)" : "(not allocated to a chain)";
            rest.Budget += diff;
            rest.Balance = rest.Budget - rest.ApprovedAmount;
        }

        return new TasafDashboardDto { Year = y, Years = years, CanEditBudget = CanEditBudget, BySalesGroup = bySalesGroup, ByChain = byChain };
    }

    private static decimal? Budget(List<TasafBudget> budgets, int groupId, int? chainId, int? areaId) =>
        budgets.FirstOrDefault(b => b.SalesGroupId == groupId && b.ChainId == chainId && b.AreaId == areaId)?.Amount;

    /// <summary>Adds up the TASAFs; share = the part of a TASAF's amount that belongs to this row (chains / areas split evenly).</summary>
    private static TasafDashboardRowDto Row(TasafSalesGroup g, IEnumerable<Item> list, Func<Item, decimal> share, decimal? budget)
    {
        var r = new TasafDashboardRowDto
        {
            SalesGroupId = g.Id, SalesGroup = g.Code.Length > 0 ? g.Code : g.Name, BudgetEntered = budget, Budget = budget ?? 0
        };
        foreach (var t in list)
        {
            var s = share(t);
            r.CreatedCount++; r.CreatedAmount += t.Total * s;
            if (t.Status is TasafStatus.Approved or TasafStatus.Completed or TasafStatus.Closed) { r.ApprovedCount++; r.ApprovedAmount += t.Total * s; }
            if (t.Status is TasafStatus.Completed or TasafStatus.Closed) { r.CompletedCount++; r.CompletedAmount += t.TparApproved * s; }
            // Closed = post evaluation approved: its actual cost.
            if (t.Status == TasafStatus.Closed) { r.ClosedCount++; r.ClosedAmount += t.Actual * s; }
        }
        r.CreatedAmount = Math.Round(r.CreatedAmount, 2);
        r.ApprovedAmount = Math.Round(r.ApprovedAmount, 2);
        r.CompletedAmount = Math.Round(r.CompletedAmount, 2);
        r.ClosedAmount = Math.Round(r.ClosedAmount, 2);
        r.Balance = r.Budget - r.ApprovedAmount;
        return r;
    }

    /// <summary>Sets budgets for a year (amount null or 0 removes it). tasaf-masters / Admin only.</summary>
    public async Task<string?> SaveBudgetsAsync(SaveTasafBudgetsDto dto)
    {
        if (!CanEditBudget) return "Only TASAF Setup users (TMM / Admin) can enter budgets.";
        if (dto.Year is < 2000 or > 2100) return "Pick a valid year.";
        var who = User?.FindFirstValue(ClaimTypes.Name) ?? "system";
        var existing = await db.TasafBudgets.Where(b => b.Year == dto.Year).ToListAsync();
        foreach (var i in dto.Items.GroupBy(i => (i.SalesGroupId, i.ChainId, i.AreaId)).Select(g => g.Last()))
        {
            if (i.ChainId is not null && i.AreaId is not null) return "A budget is per chain or per area, not both.";
            if (i.Amount is < 0) return "A budget can't be negative.";
            if (i.Amount is decimal a && decimal.Round(a, 2) != a) return "Budgets can have at most 2 decimals.";
            var row = existing.FirstOrDefault(b => b.SalesGroupId == i.SalesGroupId && b.ChainId == i.ChainId && b.AreaId == i.AreaId);
            if (i.Amount is null or 0) { if (row is not null) db.TasafBudgets.Remove(row); continue; }
            if (row is null)
            {
                if (!await db.TasafSalesGroups.AnyAsync(g => g.Id == i.SalesGroupId)) return "Unknown sales group.";
                if (i.ChainId is int cid && !await db.TasafChains.AnyAsync(c => c.Id == cid)) return "Unknown account chain.";
                if (i.AreaId is int aid && !await db.TasafAreas.AnyAsync(x => x.Id == aid)) return "Unknown area.";
                db.TasafBudgets.Add(new TasafBudget { Year = dto.Year, SalesGroupId = i.SalesGroupId, ChainId = i.ChainId, AreaId = i.AreaId,
                    Amount = i.Amount.Value, UpdatedAt = DateTime.UtcNow, UpdatedBy = who });
            }
            else if (row.Amount != i.Amount) { row.Amount = i.Amount.Value; row.UpdatedAt = DateTime.UtcNow; row.UpdatedBy = who; }
        }
        await db.SaveChangesAsync();
        return null;
    }
}
