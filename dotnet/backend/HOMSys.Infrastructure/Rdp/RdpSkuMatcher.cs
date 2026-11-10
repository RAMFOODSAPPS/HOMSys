using System.Text.RegularExpressions;

namespace HOMSys.Infrastructure.Rdp;

/// <summary>
/// Finds the RAM SKU of a line that came without a code, from its description — some RDPs send only that (GT Visayas'
/// APEX, NETMAN and COVERBEST: "RAM CORN STARCH48 X 200 G", "RAM WHOLE KERNEL CORN 410GX24", "RAM IODIZED SALT 12/1KL").
/// Strict: the product's name must start the description and BOTH its pieces per case and pack size must match; only a
/// single match counts (the longest name wins over a shorter one it contains). Anything else stays UNTAGGED.
/// </summary>
public partial class RdpSkuMatcher
{
    public record Product(string CProdNo, string ProdDesc, int Pieces, int QtyPerPc, string Um);

    private readonly List<(string Name, int Pieces, decimal Size, string Unit, string Code)> products;
    private readonly Dictionary<string, string?> cache = new();

    public RdpSkuMatcher(IEnumerable<Product> all) =>
        products = all.Where(p => p.Pieces > 0 && p.QtyPerPc > 0 && Normal(p.Um) is not null)
            .Select(p => (Letters(p.ProdDesc), p.Pieces, Normal(p.Um)!.Value.Factor * p.QtyPerPc, Normal(p.Um)!.Value.Unit, p.CProdNo))
            .Where(p => p.Item1.Length >= 6)
            .ToList();

    public string? Match(string description)
    {
        if (cache.TryGetValue(description, out var hit)) return hit;
        return cache[description] = Find(description.ToUpperInvariant());
    }

    private string? Find(string desc)
    {
        // "48 X 200 G" / "12/1KL" (pieces first) or "410GX24" (size first).
        int pieces; decimal size; string unit;
        if (PiecesFirst().Match(desc) is { Success: true } a && Normal(a.Groups[3].Value) is { } ua)
        { pieces = int.Parse(a.Groups[1].Value); size = decimal.Parse(a.Groups[2].Value) * ua.Factor; unit = ua.Unit; }
        else if (SizeFirst().Match(desc) is { Success: true } b && Normal(b.Groups[2].Value) is { } ub)
        { pieces = int.Parse(b.Groups[3].Value); size = decimal.Parse(b.Groups[1].Value) * ub.Factor; unit = ub.Unit; }
        else return null;

        var letters = Letters(desc);
        var hits = products.Where(p => p.Pieces == pieces && p.Unit == unit && p.Size == size && letters.StartsWith(p.Name)).ToList();
        if (hits.Count == 0) return null;
        var longest = hits.Max(p => p.Name.Length);
        var best = hits.Where(p => p.Name.Length == longest).Select(p => p.Code).Distinct().ToList();
        return best.Count == 1 ? best[0] : null;
    }

    private static (decimal Factor, string Unit)? Normal(string um) => um.Trim().ToUpperInvariant() switch
    {
        "G" or "GM" or "GMS" or "GR" or "GRAMS" => (1m, "G"),
        "KG" or "KGS" or "KL" or "KLS" or "KILO" => (1000m, "G"),
        "ML" => (1m, "ML"),
        "L" or "LT" or "LTR" or "LITER" or "LITRE" => (1000m, "ML"),
        "OZ" => (1m, "OZ"),
        _ => null,
    };

    private static string Letters(string s) => NonLetters().Replace(s.ToUpperInvariant(), "");

    [GeneratedRegex(@"[^A-Z]")]
    private static partial Regex NonLetters();

    [GeneratedRegex(@"(\d{1,3})\s*[X/]\s*(\d+(?:\.\d+)?)\s*(KGS?|KLS?|GMS?|GR|G|ML|LTR?|L|OZ)\b")]
    private static partial Regex PiecesFirst();

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*(KGS?|KLS?|GMS?|GR|G|ML|LTR?|L|OZ)\s*[X/]\s*(\d{1,3})\b")]
    private static partial Regex SizeFirst();
}
