using System.Text.Json;
using HOMSys.Application.Services;
using HOMSys.Domain.Entities;

namespace HOMSys.Infrastructure.Collections;

/// <summary>
/// Builds the HOMS documents a verified collection is written as — field for field the way HOMS itself fills
/// F:\HOMS\RAM\ARCHIVES (checked on OH03/OD03 and GH03/GD03 Aug–Sep 2026):
///   OR    (CR)  OH03/OD03, DOCID 2:  DR cash in bank (bank line first, BCODE "03") / CR 10301 per invoice / DR 10507 CEWT
///   RPDC  (PDC) GH03/GD03, DOCID 33: DR 10552 / CR 10301 per invoice (applied + CEWT)
///   JV    (PDC matured) GH03/GD03, DOCID 52: DR cash in bank (check amount) + DR 10507 CEWT / CR 10552 per invoice
/// Values are field name → value (null = left blank, as HOMS leaves unused columns); dates as yyyy-MM-dd.
/// Returns an error instead of a document when the collection can't be posted as-is (the posting is then Held).
/// </summary>
public static class HomsPostingBuilder
{
    public const int ArTrade = 10301, Cewt = 10507, PdcDts = 10552;

    public sealed record Doc(string Table, int DocId, int? DocNo, Dictionary<string, object?> Header, List<Dictionary<string, object?>> Lines);

    public sealed record Input(Collection C, DateOnly PostDate, string UserName);

    private static string D(DateOnly d) => d.ToString("yyyy-MM-dd");
    private static string? D(DateOnly? d) => d is { } x ? D(x) : null;
    private static string Cut(string? s, int n) => (s ?? "").Trim() is var t && t.Length > n ? t[..n] : t;

    /// <summary>HOMS PAYMENTID: STR(YY of REFDATE,2)+"-"+STR(REFID,2)+"-"+STR(REFNO,8), e.g. "26- 1-92152626".</summary>
    private static string PaymentId(DateOnly refDate, int refId, int refNo) => $"{refDate.Year % 100,2}-{refId,2}-{refNo,8}";

    /// <summary>Customer fields every line repeats (the invoice's own customer, so chain payments stay right).</summary>
    private static Dictionary<string, object?> Cust(PostedInvoice i)
    {
        var key = i.CustKey.Trim();
        var wh = i.CustWhseNo > 0 ? i.CustWhseNo : key.Length >= 2 && int.TryParse(key[..2], out var w) ? w : 0;
        return new()
        {
            ["WHSENO"] = wh, ["CKEY"] = key.Length > 2 ? key[2..] : key, ["CUSTKEY"] = key,
            ["SALESMAN"] = i.Salesman, ["TERM"] = i.Term,
        };
    }

    private static Dictionary<string, object?> With(Dictionary<string, object?> a, Dictionary<string, object?> b)
    {
        foreach (var (k, v) in b) a[k] = v;
        return a;
    }

    public static (Doc? doc, string? error) Build(HomsPostingKind kind, Input x)
    {
        var c = x.C;
        if (c.Invoices.Count == 0) return (null, "No invoice applied — an on-account receipt has no GL account in HOMS yet.");
        if (c.UnappliedAmount != 0) return (null, $"Unapplied (on-account) {c.UnappliedAmount:N2} has no GL account in HOMS yet.");
        if (c.Deductions.FirstOrDefault(d => d.Type != DeductionType.Ewt2307) is { } other)
            return (null, $"Deduction {(other.Type == DeductionType.RfcBoRtv ? "RFC/BO/RTV" : other.Type.ToString())} {other.Amount:N2}"
                          + $"{(other.Reference is { Length: > 0 } r ? $" ({r})" : "")} is waiting for its GL account in HOMS (only CEWT is posted yet).");
        if (HomsDocMap.Prefix(c.Site.Code) is not { } prefix) return (null, $"Branch {c.Site.Code} has no HOMS document prefix.");
        var digits = new string(c.ReceiptNo.Where(char.IsDigit).ToArray());
        if (digits.Length == 0 || digits.Length > 8 || !int.TryParse(digits, out var printed))
            return (null, $"Receipt no. {c.ReceiptNo} isn't a printed receipt number HOMS can use.");
        var docNo = printed >= 1_000_000 ? printed : HomsDocMap.DocNo(prefix, printed);
        // An RA CR's desk comes from its RA Excel (FOR LUZON → 32, FOR VISMIN → 31).
        var dept = c.HomsDeptCode ?? HomsDocMap.DeptCode(prefix);
        var user = Cut(x.UserName, 20).ToUpperInvariant();

        var ewt = c.Deductions.GroupBy(d => d.PostedInvoiceId).ToDictionary(g => g.Key, g => g.Sum(d => d.Amount));
        var invs = c.Invoices.OrderBy(i => i.PostedInvoice.DocNo).Select(i => (Inv: i.PostedInvoice, Applied: i.AmountApplied,
            Ewt: ewt.GetValueOrDefault(i.PostedInvoiceId))).ToList();
        var first = invs[0].Inv;

        var account = c.DepositBatch?.BankAccount;
        if (kind != HomsPostingKind.Rpdc && account?.HomsAcctNo is null)
            return (null, c.DepositBatch is null ? "No deposit recorded." : $"Deposit account {c.DepositBatch.Bank} has no HOMS GL account (BANKS4W).");
        var gl = account?.HomsAcctNo ?? 0;

        Doc doc = kind switch
        {
            HomsPostingKind.Or => Or(x, docNo, dept, user, invs, first, account!, gl),
            HomsPostingKind.Rpdc => Rpdc(x, docNo, dept, user, invs, first),
            _ => Jv(x, docNo, dept, user, invs, first, account!, gl),
        };

        decimal Sum(string f) => doc.Lines.Sum(l => l.GetValueOrDefault(f) is decimal v ? v : 0m);
        if (Sum("DRAMT") != Sum("CRAMT")) return (null, $"Debits {Sum("DRAMT"):N2} ≠ credits {Sum("CRAMT"):N2}.");
        return (doc, null);
    }

    private static Doc Or(Input x, int docNo, int dept, string user, List<(PostedInvoice Inv, decimal Applied, decimal Ewt)> invs,
        PostedInvoice first, BankAccount acct, int gl)
    {
        var c = x.C;
        var dep = c.DepositBatch!;
        // HOMS ORs paid by cheque carry the customer's cheque (drawee bank / no. / date); a transfer carries the slip.
        var cheque = c.PaymentMode == PaymentMode.Check;
        var header = new Dictionary<string, object?>
        {
            ["DOCID"] = 2, ["DOCNO"] = docNo, ["DOCDATE"] = D(c.ReceiptDate), ["DCRNO"] = c.DcrNo ?? 0, ["DCRDATE"] = D(c.ReceiptDate),
            ["USERNAME"] = user, ["TCODE"] = 1, ["POSTED"] = D(x.PostDate), ["STATDESC"] = "POSTED",
            ["SYSDATE"] = D(DateOnly.FromDateTime(c.CreatedAt)), ["SUBDATE"] = D(c.SubmittedAt is { } s ? DateOnly.FromDateTime(s) : x.PostDate),
            ["DEPTCODE"] = dept,
        };
        Dictionary<string, object?> Line(PostedInvoice inv) => With(Cust(inv), new()
        {
            ["EMPNO"] = inv.EmpNo, ["DOCID"] = 2, ["DOCNO"] = docNo, ["REFID"] = 1, ["REFNO"] = inv.DocNo, ["REFDATE"] = D(inv.DocDate),
            ["CHECKNO"] = Cut(cheque ? c.CheckNo : dep.SlipRefNo, 16), ["CHECKDATE"] = D(cheque ? c.CheckDate : dep.DepositDate),
            ["DRAWEEBANK"] = cheque ? Cut(c.Bank, 20) : null,
            ["DEPBANK"] = Cut(acct.HomsBankAc ?? acct.AccountNo, 16), ["RECBANK"] = acct.BankAbbr, ["TRTYPE"] = 1,
            ["PAYMENTID"] = PaymentId(inv.DocDate, 1, inv.DocNo),
        });
        var lines = new List<Dictionary<string, object?>>
        {
            With(Line(first), new() { ["BCODE"] = "03", ["ACCTNO"] = gl, ["DRAMT"] = c.Amount, ["BADCQF"] = false }),
        };
        foreach (var (inv, applied, ewt) in invs)
        {
            lines.Add(With(Line(inv), new() { ["ACCTNO"] = ArTrade, ["CRAMT"] = applied + ewt, ["BADCQF"] = false }));
            if (ewt > 0) lines.Add(With(Line(inv), new() { ["ACCTNO"] = Cewt, ["DRAMT"] = ewt }));
        }
        return new Doc("OH03", 2, docNo, header, lines);
    }

    private static Doc Rpdc(Input x, int docNo, int dept, string user, List<(PostedInvoice Inv, decimal Applied, decimal Ewt)> invs, PostedInvoice first)
    {
        var c = x.C;
        var header = new Dictionary<string, object?>
        {
            ["DOCID"] = 33, ["DOCTYPE"] = "RPDC", ["DOCNO"] = docNo, ["DOCDATE"] = D(c.ReceiptDate),
            ["REFID1"] = 33, ["REFTYPE1"] = "RPDC", ["REFNO1"] = docNo, ["REFDATE1"] = D(c.ReceiptDate),
            ["REFID2"] = 1, ["REFTYPE2"] = "INVOICE", ["REFNO2"] = first.DocNo, ["REFDATE2"] = D(first.DocDate),
            ["CHECKNO"] = Cut(c.CheckNo, 16), ["CHECKDATE"] = D(c.CheckDate),
            ["EXPLN1"] = $"ENTRY for Received RPDC#: {docNo}",
            ["EXPLN2"] = $"Ref. Check Nos.: {c.CheckNo} Due date: {c.CheckDate:MM/dd/yyyy}",
            ["POSTED"] = D(x.PostDate), ["DCRNO"] = 0, ["DCRDATE"] = D(c.ReceiptDate), ["BCODE"] = "03", ["TCODE"] = 1,
            ["STATDESC"] = "Posted", ["USERNAME"] = user, ["EMPNO"] = first.EmpNo, ["DEPTCODE"] = dept,
        };
        var lines = new List<Dictionary<string, object?>>();
        foreach (var (inv, applied, ewt) in invs)
        {
            Dictionary<string, object?> Line() => With(Cust(inv), new()
            {
                ["DOCTYPE"] = "RPDC", ["DOCNO"] = docNo, ["DOCID"] = 33, ["EMPNO"] = inv.EmpNo, ["REFID"] = 1, ["REFNO"] = inv.DocNo,
                ["REFDATE"] = D(inv.DocDate), ["NAMEDET"] = Cut(inv.CustName, 50), ["DRAWEEBANK"] = Cut(c.Bank, 20),
                ["CHECKNO"] = Cut(c.CheckNo, 16), ["CHECKDATE"] = D(c.CheckDate), ["TRTYPE"] = 1,
                ["PAYMENTID"] = PaymentId(inv.DocDate, 1, inv.DocNo),
            });
            lines.Add(With(Line(), new() { ["ACCTNO"] = PdcDts, ["DRAMT"] = applied + ewt }));
            lines.Add(With(Line(), new() { ["ACCTNO"] = ArTrade, ["CRAMT"] = applied + ewt }));
        }
        return new Doc("GH03", 33, docNo, header, lines);
    }

    private static Doc Jv(Input x, int rpdcNo, int dept, string user, List<(PostedInvoice Inv, decimal Applied, decimal Ewt)> invs,
        PostedInvoice first, BankAccount acct, int gl)
    {
        var c = x.C;
        var header = new Dictionary<string, object?>
        {
            ["DOCID"] = 52, ["DOCTYPE"] = "JV", ["DOCDATE"] = D(x.PostDate), ["REFID1"] = 33, ["REFTYPE1"] = "RPDC", ["REFTYPE2"] = "FBS",
            ["EXPLN1"] = $"Matured RPDC {rpdcNo}", ["EXPLN2"] = $",{Cut(c.Bank, 20)}-{c.CheckNo}",
            ["POSTED"] = D(x.PostDate), ["DCRNO"] = 0, ["DCRDATE"] = D(x.PostDate), ["BCODE"] = "03", ["TCODE"] = 1,
            ["STATDESC"] = "Posted", ["USERNAME"] = user, ["DEPTCODE"] = dept,
        };
        Dictionary<string, object?> Line(PostedInvoice inv) => With(Cust(inv), new()
        {
            ["DOCTYPE"] = "JV", ["DOCID"] = 52, ["DRAWEEBANK"] = Cut(c.Bank, 20), ["CHECKNO"] = Cut(c.CheckNo, 16), ["CHECKDATE"] = D(c.CheckDate),
            ["DEPBANK"] = Cut(acct.HomsBankAc ?? acct.AccountNo, 16), ["RECBANK"] = acct.BankAbbr, ["PAYMENTID"] = PaymentId(inv.DocDate, 1, inv.DocNo),
        });
        var lines = new List<Dictionary<string, object?>>
        {
            With(Line(first), new()
            {
                ["ACCTNO"] = gl, ["DRAMT"] = c.Amount, ["EMPNO"] = first.EmpNo, ["REFTYPE"] = "RPDC", ["REFNO"] = rpdcNo,
                ["REFDATE"] = D(first.DocDate), ["REFID"] = 33, ["TRTYPE"] = 0,   // HOMS: REFDATE = the invoice date
            }),
        };
        foreach (var (inv, _, ewt) in invs.Where(i => i.Ewt > 0))   // HOMS leaves TERM / REFDATE / EMPNO / TRTYPE blank here
            lines.Add(With(Line(inv), new() { ["ACCTNO"] = Cewt, ["DRAMT"] = ewt, ["REFTYPE"] = "INVOICE", ["REFNO"] = inv.DocNo, ["REFID"] = 1, ["TERM"] = null }));
        foreach (var (inv, applied, ewt) in invs)
            lines.Add(With(Line(inv), new()
            {
                ["ACCTNO"] = PdcDts, ["CRAMT"] = applied + ewt, ["EMPNO"] = inv.EmpNo, ["REFTYPE"] = "RPDC", ["REFNO"] = rpdcNo,
                ["REFDATE"] = D(inv.DocDate), ["REFID"] = 33, ["INVNO"] = inv.DocNo, ["INVDATE"] = D(inv.DocDate),
                ["ID_NUMBER"] = "1INVOICE", ["TRTYPE"] = 1,
            }));
        return new Doc("GH03", 52, null, header, lines);
    }

    public static string Json(object o) => JsonSerializer.Serialize(o);
}
