using System.Globalization;
using System.Net;
using System.Text;
using HOMSys.Application.DTOs.Tasaf;

namespace HOMSys.Application.Services;

/// <summary>
/// The whole TASAF as an email body, in the form's order (header, details, products, costs,
/// computation, payment, approval chain) — so an approver can review it without opening HOMSys.
/// Email-safe HTML: tables + inline styles only. With a decision token it ends with ONE "Review &amp; Decide"
/// button that opens the no-login decision page (Approve / Return / Reject + comment there) ("{{WEB}}" = site address, filled by the
/// email sender). Also shown on that page itself (no token).
/// </summary>
public static class TasafEmailRenderer
{
    private const string Brand = "#800000";
    /// <summary>Replaced by the site address (Email:WebUrl) when the email is sent.</summary>
    private const string Web = "{{WEB}}";
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    public static string Render(TasafDto d, string intro, string? decisionToken)
    {
        var support = TasafService.IsSupportProgram(d.ProgramTypeName ?? "");
        var sb = new StringBuilder();
        sb.Append($"""<div style="font-family:Segoe UI,Arial,sans-serif;font-size:13px;color:#1f1f1f;max-width:760px">""");

        // Header (like the form's title block)
        sb.Append($"""
            <div style="border-top:4px solid {Brand};padding:10px 0 6px;color:{Brand};font-weight:700">HOMSys</div>
            <div style="font-size:17px;font-weight:700;margin:4px 0 2px">TRADE ACTIVITY SPEND APPROVAL FORM</div>
            <div style="font-size:15px;margin:0 0 10px">{E(d.Title)}</div>
            """);
        if (intro.Length > 0)
            sb.Append($"""<div style="background:#fdf2f2;border-left:4px solid {Brand};padding:8px 12px;margin:0 0 12px">{E(intro)}</div>""");
        sb.Append(Grid([
            ("Document No.", d.RefNo ?? "—"), ("Status", d.Status),
            ("Date", Manila(d.CreatedAt)), ("Proponent", d.ProponentName),
        ]));

        // Details
        sb.Append(Section("Details"));
        var stores = d.Customers.Count == 0 ? "—"
            : string.Join(", ", d.Customers.Take(30).Select(c => $"{c.CustKey} {c.CusName}".Trim()))
              + (d.Customers.Count > 30 ? $" … (+{d.Customers.Count - 30} more, {d.Customers.Count} stores)" : "");
        sb.Append(Rows([
            ("Activity Title", d.Title),
            ("Program Duration", $"{Date(d.StartDate)} – {Date(d.EndDate)}"),
            ("Sales Group", d.SalesGroupName),
            ("Area", d.AreaNames),
            ("Branch(es)", string.Join(", ", d.Branches.Select(b => b.Name))),
            ("Customer / Chain", d.ChainName),
            ("Cust Key", stores),
            ("Program Type", WithOther(d.ProgramTypeName, d.ProgramTypeOther)),
            ("Objective", WithOther(d.ObjectiveName, d.ObjectiveOther)),
            ("Target Market", WithOther(d.TargetMarketName, d.TargetMarketOther)),
            ("Channel", d.ChannelNames),
            ("Product Group", Join(d.ProductGroupName, d.ProductGroup2Name)),
            ("Category", Join(d.CategoryName, d.Category2Name)),
            ("Background", d.Background),
            ("Concessions", d.Concessions),
            ("Program Mechanics", d.Mechanics),
        ]));

        // Participating Products
        if (!support && d.Products.Count > 0)
        {
            sb.Append(Section("Participating Products"));
            sb.Append(Table(
                ["ProdNo", "SKU", "P3 Mo Avg (Cs)", "SPLY (Cs)", "Promoted Vol (Cs)", "Incr Vol (Cs)", "LSP / Case", "Promoted Value"],
                [false, false, true, true, true, true, true, true],
                d.Products.Select(p => new[]
                {
                    p.CProdNo, p.Sku, Num(p.P3MoAvgCs), Num(p.SplyCs), Num(p.PromotedVolCs), Num(p.IncrVolCs),
                    Num(p.LspPerCase) + (p.LspComputed is decimal c && c != p.LspPerCase ? $" (computed {Num(c)})" : ""),
                    Num(p.PromotedValue)
                }).ToList(),
                ["Total", "", "", "", Num(d.Products.Sum(p => p.PromotedVolCs)), Num(d.Products.Sum(p => p.IncrVolCs)), "",
                 Num(d.Products.Sum(p => p.PromotedValue))]));
        }

        // Cost Details
        sb.Append(Section("Cost Details"));
        sb.Append(Table(["Description", "Amount (₱)", "Remarks"], [false, true, false],
            d.Costs.Select(c => new[] { c.CostTypeName ?? "—", Num(c.Amount), c.Remarks }).ToList(),
            ["Total Cost", Num(d.TotalCost), ""]));

        // Computation
        sb.Append(Section(support ? "Account Support & Convention Computation" : "Computation"));
        var rows = new List<(string, string?)>
        {
            (support ? "Annual Projected Sales" : "Promoted Value", Num(d.PromotedValue))
        };
        if (!support)
        {
            rows.Add(("Pre-Promo Sales", Num(d.PrePromoSales) + Pct(d.PromotedVsPrePromo, " promoted vs pre-promo")));
            rows.Add(("Incremental Sales", Num(d.IncrementalSales) + Pct(d.IncrementalVsPrePromo, " vs pre-promo")));
        }
        rows.Add(("Total Cost", Num(d.TotalCost)));
        rows.Add(($"CTS ({(support ? "Annual Projected Sales" : "Promoted Value")})", d.Cts is decimal cts ? cts.ToString("P2", En) : "—"));
        if (d.CtsJustification.Length > 0) rows.Add(("CTS Justification", d.CtsJustification));
        sb.Append(Rows(rows));

        // Mode of Payment
        sb.Append(Section("Mode of Payment"));
        var pay = new (string Label, decimal Amount)[]
        {
            ("Invoice Deduction", d.PayInvDeductionAp + d.PayInvDeductionSalesman),
            ("Check Payment", d.PayCheckAp + d.PayCheckSalesman),
            ("Free Goods", d.PayFreeGoodsAp + d.PayFreeGoodsSalesman),
            ("Off-Invoice", d.PayOffInvoiceAp + d.PayOffInvoiceSalesman),
        }.Where(x => x.Amount != 0).ToList();
        sb.Append(Table(["Payment Type", "AP / Collection (₱)"], [false, true],
            pay.Select(x => new[] { x.Label, Num(x.Amount) }).ToList(), null));

        // Approval chain
        if (d.Steps.Count > 0)
        {
            sb.Append(Section($"Approval{(d.ApprovalTypeName.Length > 0 ? " — " + d.ApprovalTypeName : "")}"));
            sb.Append(Table(["#", "Role", "Approver", "Status", "Date", "Comment"], [false, false, false, false, false, false],
                d.Steps.Select(s => new[]
                {
                    s.Seq.ToString(), s.RoleCode, s.UserName.Length > 0 ? s.UserName : "(vacant)", s.Status,
                    s.ActedAt is DateTime a ? Manila(a) : "", s.Comment
                }).ToList(), null));
        }

        // Review & Decide: opens the no-login decision page (Approve / Return / Reject + required comment)
        if (decisionToken is not null)
        {
            var t = Uri.EscapeDataString(decisionToken);
            sb.Append($"""
                <div style="margin:22px 0 8px">
                  <a href="{Web}/tasaf-decision?t={t}" style="background:{Brand};color:#fff;padding:12px 30px;border-radius:4px;text-decoration:none;font-weight:600">Review &amp; Decide</a>
                </div>
                <div style="color:#666;font-size:12px">Opens a HOMSys page (no login) where you choose Approve, Return or Reject and enter your comment.
                  Return sends it back to the proponent to correct and resubmit; Reject ends it.
                  This link is for you only — please don't forward this email.</div>
                """);
        }
        sb.Append("""<p style="color:#888;font-size:11px;margin-top:18px">Automatic message from HOMSys — please do not reply.</p></div>""");
        return sb.ToString();
    }

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
    /// <summary>Encoded text keeping its line breaks (Background, Mechanics…).</summary>
    private static string Multi(string? s) => E(s).Replace("\r\n", "\n").Replace("\n", "<br>");
    private static string Num(decimal? v) => v is decimal x ? x.ToString("N2", En) : "—";
    private static string Pct(decimal? v, string label) => v is decimal x ? $" ({x.ToString("P2", En)}{label})" : "";
    private static string Date(DateOnly? d) => d?.ToString("MMM d, yyyy", En) ?? "—";
    private static string Manila(DateTime utc) => utc.AddHours(8).ToString("MMM d, yyyy h:mm tt", En);
    private static string? Join(string? a, string? b) => string.Join(" / ", new[] { a, b }.Where(x => !string.IsNullOrWhiteSpace(x)));
    private static string? WithOther(string? name, string other) => other.Trim().Length > 0 ? $"{name} — {other.Trim()}" : name;

    private static string Section(string title) =>
        $"""<div style="margin:18px 0 6px;padding-bottom:3px;border-bottom:2px solid {Brand};color:{Brand};font-weight:700;font-size:14px">{E(title)}</div>""";

    /// <summary>Label / value rows (the form's fields).</summary>
    private static string Rows(IEnumerable<(string Label, string? Value)> rows)
    {
        var sb = new StringBuilder("""<table cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%">""");
        foreach (var (label, value) in rows)
            sb.Append($"""<tr><td style="padding:5px 10px 5px 0;color:#555;width:190px;vertical-align:top;border-bottom:1px solid #eee">{E(label)}</td><td style="padding:5px 0;vertical-align:top;border-bottom:1px solid #eee">{(string.IsNullOrWhiteSpace(value) ? "—" : Multi(value))}</td></tr>""");
        return sb.Append("</table>").ToString();
    }

    /// <summary>Two label/value pairs per row (the header block).</summary>
    private static string Grid(IReadOnlyList<(string Label, string Value)> cells)
    {
        var sb = new StringBuilder("""<table cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;background:#faf3f3">""");
        for (var i = 0; i < cells.Count; i += 2)
        {
            sb.Append("<tr>");
            foreach (var (label, value) in cells.Skip(i).Take(2))
                sb.Append($"""<td style="padding:6px 10px;color:#555;width:110px">{E(label)}</td><td style="padding:6px 10px;font-weight:600">{E(value)}</td>""");
            sb.Append("</tr>");
        }
        return sb.Append("</table>").ToString();
    }

    private static string Table(string[] head, bool[] right, List<string[]> rows, string[]? total)
    {
        string Cell(string v, bool r, bool th, bool bold) =>
            $"""<{(th ? "th" : "td")} style="padding:5px 8px;border:1px solid #e5e5e5;text-align:{(r ? "right" : "left")};{(th ? "background:#f3e6e6;font-weight:600;" : "")}{(bold ? "font-weight:700;background:#faf3f3;" : "")}white-space:{(r ? "nowrap" : "normal")}">{E(v)}</{(th ? "th" : "td")}>""";
        var sb = new StringBuilder("""<table cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;font-size:12px">""");
        sb.Append("<tr>").Append(string.Concat(head.Select((h, i) => Cell(h, right[i], true, false)))).Append("</tr>");
        if (rows.Count == 0)
            sb.Append($"""<tr><td colspan="{head.Length}" style="padding:6px 8px;border:1px solid #e5e5e5;color:#888">—</td></tr>""");
        foreach (var r in rows)
            sb.Append("<tr>").Append(string.Concat(r.Select((v, i) => Cell(v, right[i], false, false)))).Append("</tr>");
        if (total is not null)
            sb.Append("<tr>").Append(string.Concat(total.Select((v, i) => Cell(v, right[i], false, true)))).Append("</tr>");
        return sb.Append("</table>").ToString();
    }
}
