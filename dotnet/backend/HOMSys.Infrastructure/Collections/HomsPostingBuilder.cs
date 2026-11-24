using System.Text.Json;
using HOMSys.Application.Services;
using HOMSys.Domain.Entities;

namespace HOMSys.Infrastructure.Collections;

/// <summary>
/// Builds the HOMS documents a verified collection is written as — field for field the way HOMS itself fills
/// F:\HOMS\RAM\ARCHIVES (checked on OH03/OD03 and GH03/GD03 Aug–Sep 2026):
///   OR    (CR)  OH03/OD03, DOCID 2:  DR cash in bank (bank line first, BCODE "03") / CR 10301 per invoice / DR CEWT
///   RPDC  (PDC) GH03/GD03, DOCID 33: DR 10552 / CR 10301 per invoice (applied + CEWT)
///   JV    (PDC matured) GH03/GD03, DOCID 52: DR cash in bank (check amount) + DR CEWT / CR 10552 per invoice
/// The CEWT is debited to each EWT deduction's own ACCTNO (since 2026-11-12): 10507 with its 2307 attached, 10508 without,
/// or whatever account the custodian typed. HOMS's own ORs only ever used 10507.
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
        if (HomsDocMap.Prefix(c.Site.Code) is not { } prefix) return (null, $"Branch {c.Site.Code} has no HOMS document prefix.");
        if (kind is HomsPostingKind.Or or HomsPostingKind.Rpdc && AwaitingRfcNote(c) is { } waiting) return (null, waiting);
        var digits = new string(c.ReceiptNo.Where(char.IsDigit).ToArray());
        if (digits.Length == 0 || digits.Length > 8 || !int.TryParse(digits, out var printed))
            return (null, $"Receipt no. {c.ReceiptNo} isn't a printed receipt number HOMS can use.");
        var docNo = printed >= 1_000_000 ? printed : HomsDocMap.DocNo(prefix, printed);
        // An RA CR's desk comes from its RA Excel (FOR LUZON → 32, FOR VISMIN → 31).
        var dept = c.HomsDeptCode ?? HomsDocMap.DeptCode(prefix);
        var user = Cut(x.UserName, 20).ToUpperInvariant();

        // Only EWT (2307) is an OR line (its own CEWT row). The other deductions go to HOMS's ordeduc2 as HOMS's own ORs
        // record them (user 2026-11-21), not as GL lines; see DeductionRows.
        var ewt = c.Deductions.Where(d => d.Type == DeductionType.Ewt2307)
            .GroupBy(d => d.PostedInvoiceId).ToDictionary(g => g.Key, g => g.Sum(d => d.Amount));
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
            foreach (var (ewtGl, amt) in EwtLines(c, inv.Id)) lines.Add(With(Line(inv), new() { ["ACCTNO"] = ewtGl, ["DRAMT"] = amt }));
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
        foreach (var (inv, _, _) in invs)   // HOMS leaves TERM / REFDATE / EMPNO / TRTYPE blank here
            foreach (var (ewtGl, amt) in EwtLines(c, inv.Id))
                lines.Add(With(Line(inv), new() { ["ACCTNO"] = ewtGl, ["DRAMT"] = amt, ["REFTYPE"] = "INVOICE", ["REFNO"] = inv.DocNo, ["REFID"] = 1, ["TERM"] = null }));
        foreach (var (inv, applied, ewt) in invs)
            lines.Add(With(Line(inv), new()
            {
                ["ACCTNO"] = PdcDts, ["CRAMT"] = applied + ewt, ["EMPNO"] = inv.EmpNo, ["REFTYPE"] = "RPDC", ["REFNO"] = rpdcNo,
                ["REFDATE"] = D(inv.DocDate), ["REFID"] = 33, ["INVNO"] = inv.DocNo, ["INVDATE"] = D(inv.DocDate),
                ["ID_NUMBER"] = "1INVOICE", ["TRTYPE"] = 1,
            }));
        return new Doc("GH03", 52, null, header, lines);
    }

    /// <summary>An invoice's CEWT debits, one per HOMS GL (a deduction saved before ACCTNO existed is a 10507).</summary>
    private static List<(int Acct, decimal Amount)> EwtLines(Collection c, int postedInvoiceId) =>
        c.Deductions.Where(d => d.PostedInvoiceId == postedInvoiceId && d.Type == DeductionType.Ewt2307)
            .GroupBy(d => d.AcctNo ?? Cewt).OrderBy(g => g.Key)
            .Select(g => (g.Key, g.Sum(d => d.Amount))).Where(x => x.Item2 > 0).ToList();

    /// <summary>ordeduc2 REMARKS (50): the selection's short text (DeductionReasons.Remark), then for "Others" its description,
    /// then the Ref. the custodian typed — so HOMS / the VFP reports can tell the selections apart.</summary>
    public static string DedRemarks(CollectionDeduction d)
    {
        var r = DeductionReasons.Of(d);
        var parts = new[] { r.Remark, r.Code == DeductionReasons.Others ? d.Description : null, d.Reference }
            .Select(x => (x ?? "").Trim()).Where(x => x.Length > 0);
        return Cut(string.Join(" ", parts).ToUpperInvariant(), 50);
    }

    /// <summary>
    /// Rows for HOMS's ordeduc2 (F:\HOMS\RAM\ordeduc2.DBF), field for field the way HOMS's own ORs fill it (checked on
    /// 2026 rows, e.g. OR 93004129): one per deduction that is <see cref="CollectionDeduction.ReadyForHoms"/> — every type
    /// but EWT (an OR line already), a Service Invoice only with its scanned copy (user 2026-11-21). docId 2 = OR, 33 = RPDC.
    /// POSTED = the OR / RPDC's own posting date; SYSDATE = when this row is sent.
    /// </summary>
    public static (List<Dictionary<string, object?>> Rows, List<int> Ids) DeductionRows(Collection c, int docId, int docNo, string userName,
        DateOnly posted, DateOnly sysDate)
    {
        var invs = c.Invoices.Where(i => i.PostedInvoice is not null).ToDictionary(i => i.PostedInvoiceId, i => i.PostedInvoice);
        var user = Cut(userName, 20).ToUpperInvariant();
        var rows = new List<Dictionary<string, object?>>();
        var ids = new List<int>();
        foreach (var d in c.Deductions.Where(d => d.ReadyForHoms && d.Amount > 0).OrderBy(d => d.Id))
        {
            if (!invs.TryGetValue(d.PostedInvoiceId, out var inv)) continue;
            var reason = DeductionReasons.Of(d);
            var (code, text) = (reason.DedCode, reason.DedType);
            // BO RETURNS: a return references its RFC (REFID 6 / "RFC" / REFNO = RFC no., APPLYDOC = the invoice — 150k of
            // HOMS's own rows); a code-11 row with no RFC (RFC amount difference) references the invoice, as HOMS's recent
            // rows without one do (REFTYPE "INV", REFNO = invoice).
            var rfc = code == 11 && d.RfcNo is > 0;
            var cust = Cust(inv);
            rows.Add(new()
            {
                ["DOCID"] = docId, ["DOCNO"] = docNo, ["WHSENO"] = cust["WHSENO"], ["CKEY"] = cust["CKEY"], ["CUSTKEY"] = cust["CUSTKEY"],
                ["DOCDATE"] = D(c.ReceiptDate), ["DEDCODE"] = code, ["DEDTYPE"] = text, ["DEALCODE"] = 0,
                ["REFID"] = code == 11 ? 6 : 1, ["REFTYPE"] = rfc ? "RFC" : "INV", ["REFNO"] = rfc ? d.RfcNo : inv.DocNo,
                ["AMOUNT"] = d.Amount, ["PAYMENTID"] = PaymentId(inv.DocDate, 1, inv.DocNo), ["REMARKS"] = DedRemarks(d),
                ["FORAUTODN"] = false, ["POSTED"] = D(posted), ["SALESMAN"] = inv.Salesman, ["EMPNO"] = inv.EmpNo,
                ["APPLYID"] = 1, ["APPLYDOC"] = inv.DocNo, ["APPDOCDATE"] = D(inv.DocDate),
                ["USERNAME"] = user, ["SYSDATE"] = D(sysDate),
            });
            ids.Add(d.Id);
        }
        return (rows, ids);
    }

    /// <summary>
    /// Why the OR / RPDC waits (user 2026-11-24): a "Return from customer" can't be downloaded to HOMS A/R until its RFC is
    /// posted there and linked, e.g. "Waiting for the RFC: inv 94021551 return 14,570.09 — not yet posted in HOMS A/R.
    /// The CNC Verifier links it (PDC › Returns awaiting RFC)." Null when every return has its RFC.
    /// </summary>
    public static string? AwaitingRfcNote(Collection c)
    {
        var waiting = c.Deductions.Where(d => d.AwaitingRfc).ToList();
        if (waiting.Count == 0) return null;
        var nos = c.Invoices.Where(i => i.PostedInvoice is not null).ToDictionary(i => i.PostedInvoiceId, i => i.PostedInvoice.DocNo);
        var list = string.Join("; ", waiting.Select(d =>
            $"inv {(nos.TryGetValue(d.PostedInvoiceId, out var no) ? no : d.PostedInvoice?.DocNo)} return {d.Amount:N2}"));
        var text = $"Waiting for the RFC: {list} — not yet posted in HOMS A/R. The CNC Verifier links it (PDC › Returns awaiting RFC).";
        return text.Length > 500 ? text[..500] : text;
    }

    /// <summary>Short name of a deduction's selection for notes ("Parking fee", "Others: freight to Naga").</summary>
    private static string Label(CollectionDeduction d)
    {
        var r = DeductionReasons.Of(d);
        var name = r.Label.Split(" (")[0];
        return r.Code == DeductionReasons.Others && d.Description is { Length: > 0 } desc ? $"Others: {desc}" : name;
    }

    /// <summary>
    /// Where a posting's deductions went, for the collection's history: EWT is an OR line; the rest go to HOMS's ordeduc2,
    /// except a Service Invoice still without its scanned copy (a hanging balance in HOMS until it's uploaded), e.g.
    /// "To HOMS ordeduc2: inv 94021515 Other 296.40, Discount 8.01. Waiting for the scanned Service Invoice (not in
    /// ordeduc2 yet): inv 94021515 SI 1234 500.00". Null when there are none.
    /// </summary>
    public static string? DeductionNote(Collection c)
    {
        var nos = c.Invoices.Where(i => i.PostedInvoice is not null).ToDictionary(i => i.PostedInvoiceId, i => i.PostedInvoice.DocNo);
        string Part(IEnumerable<CollectionDeduction> ds) => string.Join("; ", ds.GroupBy(d => d.PostedInvoiceId)
            .Select(g => $"inv {(nos.TryGetValue(g.Key, out var no) ? no : g.First().PostedInvoice?.DocNo)} "
                         + string.Join(", ", g.Select(d => d.Type == DeductionType.ServiceInvoice
                             ? $"SI {d.Reference} {d.Amount:N2}"
                             : $"{Label(d)} {d.Amount:N2}" + (d.RfcNo is > 0 ? $" RFC {d.RfcNo}" : "")
                               + (d.Reference is { Length: > 0 } r && r != $"RFC {d.RfcNo}" ? $" ({r})" : "")))));
        var other = c.Deductions.Where(d => d.Type != DeductionType.Ewt2307 && d.Amount != 0).ToList();
        var waiting = other.Where(d => d.Type == DeductionType.ServiceInvoice && d.AttachmentId is null).ToList();
        var sent = other.Except(waiting).ToList();
        var parts = new List<string>();
        if (sent.Count > 0) parts.Add($"To HOMS ordeduc2: {Part(sent)}.");
        if (waiting.Count > 0) parts.Add($"Waiting for the scanned Service Invoice (not in ordeduc2 yet): {Part(waiting)}.");
        if (parts.Count == 0) return null;
        var text = string.Join(" ", parts);
        return text.Length > 500 ? text[..500] : text;
    }

    public static string Json(object o) => JsonSerializer.Serialize(o);
}
