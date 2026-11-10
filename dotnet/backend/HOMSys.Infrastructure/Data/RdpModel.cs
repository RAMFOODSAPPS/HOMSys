using HOMSys.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Data;

/// <summary>
/// EF mapping + seed rows for the RDP (RAM Distributor Partner) sales module. Seed values are
/// the "RDP List" and "Reference" sheets of "RAM RDP Sales Raw Data Template.xlsx" (TMG, 2026).
/// </summary>
internal static class RdpModel
{
    /// <summary>RAM's standard channel types, in the Reference sheet's order.</summary>
    public static readonly string[] Channels =
    [
        "Sari-sari store", "Market stall / Public market", "Grocery", "Supermarket / Hypermarket",
        "Convenience store / Minimart", "Wholesaler / Sub-distributor", "Drugstore / Pharmacy",
        "Foodservice / HORECA", "Bakery / Baking supplies", "Meat shop", "Cooperative", "Other channels",
    ];

    /// <summary>Labels seen in distributor files, per standard channel ("Reference" sheet column C).</summary>
    private static readonly (string Channel, string[] Labels)[] SeenLabels =
    [
        ("Sari-sari store", ["SARI-SARI STORE", "SARI-SARI", "SARI - SARI STORE", "SIDE STREET", "SSST", "SARISARI"]),
        ("Market stall / Public market", ["MARKET STALL", "PUBLIC MARKET", "MARKET STALL (DRY)", "MARKET STALL RETAIL", "PUBLIC MARKETS"]),
        ("Grocery", ["GROCERY", "GROCERY STORE", "GROCERIES", "GROC"]),
        ("Supermarket / Hypermarket", ["SUPERMARKET", "KEY SUPERMARKET", "SMKT", "HYPERMARKET", "SMALL SUPERMARKET"]),
        ("Convenience store / Minimart", ["MINI MART", "CONVENIENCE STORE", "CONV", "MINIMART", "CONVENIENCE STORE/ MINIMART", "CONVIENCE STORE"]),
        ("Wholesaler / Sub-distributor", ["WHOLESALER", "WLSL", "SUB-D", "SUBDI", "SUB DISTRIBUTOR", "MARKET STALL WHOLESALE"]),
        ("Drugstore / Pharmacy", ["PHARMACY", "DRUGSTORE", "DRUG STORE", "DRGS"]),
        ("Foodservice / HORECA", ["FILIPINO RESTAURANT", "AT WORK CANTEEN", "RESTAURANT", "OTHER ASIAN RESTAURANTS", "HOTEL", "RESORT"]),
        ("Bakery / Baking supplies", ["BAKERY", "BAKING SUPPLIES", "BAKESHOP", "BAKERY SUPPLIES", "BKRY"]),
        ("Meat shop", ["MEAT SHOP", "MEAT SHOP- INSTI"]),
        ("Cooperative", ["COOPERATIVE", "COOPERATIVES", "COOPERATIVE GROWERS", "COOPERATIVE-INSTI"]),
        ("Other channels", ["END USER", "RETAIL", "OTHER", "DEPARTMENT STORE", "OTHR", "OTP / LGU ORDER / SERVICING ONLY"]),
    ];

    private static readonly (int CustKey, string Name, string Rdm, string Area, string Territory)[] Distributors =
    [
        (1010022, "DIRECT REACH DISTRIBUTOR INC.", "RICARDO ANTONIO", "NCL GT", "Luzon"),
        (1610289, "COTTON CITY MARKETING CORPORATION", "RENAN MARUKOT", "SL GT", "Luzon"),
        (1610292, "CHASE MEGA CORPORATION", "RENAN MARUKOT", "SL GT", "Luzon"),
        (2110253, "TJA MARKETING CORPORATION", "RICARDO ANTONIO", "NCL GT", "Luzon"),
        (2110286, "DRB SALES,INC.", "RICARDO ANTONIO", "NCL GT", "Luzon"),
        (2210124, "BONHEUR MARKETING CORPORATION", "ANDREW ANICETO", "GMA GT", "Luzon"),
        (2210128, "SOUTHLINE CHANNEL MARKETING", "ANDREW ANICETO", "GMA GT", "Luzon"),
        (2210129, "RACE VENTURES INC.", "ANDREW ANICETO", "GMA GT", "Luzon"),
        (2210163, "COEL MARKETING, INC.", "RENAN MARUKOT", "SL GT", "Luzon"),
        (2210172, "NETMAN DISTRIBUTORS, INC.", "NIKKO CAPIOSO", "VIS GT", "Visayas"),
        (2210186, "TWO PRINCE TRADING OPC", "RENAN MARUKOT", "SL GT", "Luzon"),
        (2210206, "SOUTHCORE MARKETING CORPORATION", "RENAN MARUKOT", "SL GT", "Luzon"),
        (2210214, "CHUA PHILIPPINES CORPORATION", "RENAN MARUKOT", "SL GT", "Luzon"),
        (2210219, "EIGHT PRINCE TRADING OPC", "RENAN MARUKOT", "SL GT", "Luzon"),
        (2210228, "1 TO 1 MARKETING INC.", "ANDREW ANICETO", "GMA GT", "Luzon"),
        (2310257, "JT INTANDEM DISTRIBUTION", "RENAN MARUKOT", "SL GT", "Luzon"),
        (2510474, "CHIP ACCENDO ENTERPRISES", "RICARDO ANTONIO", "NCL GT", "Luzon"),
        (2510475, "CHIP ACCENDO ENTERPRISES", "RICARDO ANTONIO", "NCL GT", "Luzon"),
        (2610118, "CHEERS TRADESTAR INC.", "RICARDO ANTONIO", "NCL GT", "Luzon"),
        (4114314, "APEX FOODS DISTRIBUTION CORP.", "NIKKO CAPIOSO", "VIS GT", "Visayas"),
        (4348770, "JACAL FOOD MARKETING", "NIKKO CAPIOSO", "VIS GT", "Visayas"),
        (4425667, "BERTAHAN DISTRIBUTION CORPORATION", "NIKKO CAPIOSO", "VIS GT", "Visayas"),
        (5410194, "DESAN CONSUMERS INC.", "ROY AGUHOB", "MIN GT", "Mindanao"),
        (6153055, "SDC GLOBAL CHOICE CORPORATION", "ROY AGUHOB", "MIN GT", "Mindanao"),
        (6153057, "ROMARK SALES INC.", "ROY AGUHOB", "MIN GT", "Mindanao"),
        (6153073, "CAMIGUIN ISLA TRADING INCORPORATED", "ROY AGUHOB", "MIN GT", "Mindanao"),
        (6153078, "HCB FOOD PRODUCTS MARKETING", "ROY AGUHOB", "MIN GT", "Mindanao"),
        (6153082, "PRIMUS VENTURES", "ROY AGUHOB", "MIN GT", "Mindanao"),
        (6290074, "DAVAO SQUIRE DISTRIBUTORS INC.", "ROY AGUHOB", "MIN GT", "Mindanao"),
        (6290090, "CERSUM MARKETING OPC", "ROY AGUHOB", "MIN GT", "Mindanao"),
        (6394358, "BFV CORPORATION", "ROY AGUHOB", "MIN GT", "Mindanao"),
        (6394361, "BFV POINT-OF-SALE MARKETING", "ROY AGUHOB", "MIN GT", "Mindanao"),
        (6410129, "CERSUM MARKETING OPC", "ROY AGUHOB", "MIN GT", "Mindanao"),
        (6410136, "DESAN CONSUMERS INC.", "ROY AGUHOB", "MIN GT", "Mindanao"),
        (6571170, "YI SHUN CONSUMER GOODS TRADING", "ROY AGUHOB", "MIN GT", "Mindanao"),
        (6710039, "COVERBEST DISTRIBUTIONS CORPORATION", "NIKKO CAPIOSO", "VIS GT", "Visayas"),
        (6910107, "SHERWIN XR MARKETING", "ROY AGUHOB", "MIN GT", "Mindanao"),
        (6910122, "PRIMUS VENTURES", "ROY AGUHOB", "MIN GT", "Mindanao"),
    ];

    public static void Configure(ModelBuilder mb)
    {
        mb.Entity<RdpDistributor>(e =>
        {
            e.HasIndex(x => x.CustKey).IsUnique();
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Rdm).HasMaxLength(100);
            e.Property(x => x.Area).HasMaxLength(50);
            e.Property(x => x.Territory).HasMaxLength(50);
            e.Property(x => x.FileNames).HasMaxLength(500);
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
            e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.SetNull);
            e.HasData(Distributors.Select((d, i) => new RdpDistributor
            {
                Id = i + 1, CustKey = d.CustKey, Name = d.Name, Rdm = d.Rdm, Area = d.Area, Territory = d.Territory, IsActive = true,
            }));
        });

        mb.Entity<RdpChannelMap>(e =>
        {
            e.HasIndex(x => x.Label).IsUnique();
            e.Property(x => x.Label).HasMaxLength(100);
            e.Property(x => x.Channel).HasMaxLength(60);
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
            // Each standard name maps to itself, so a file already using RAM's list needs no mapping.
            var seed = Channels.Select(c => (Label: c.ToUpperInvariant(), Channel: c))
                .Concat(SeenLabels.SelectMany(s => s.Labels.Select(l => (Label: l, Channel: s.Channel))))
                .DistinctBy(x => x.Label).ToList();
            e.HasData(seed.Select((s, i) => new RdpChannelMap { Id = i + 1, Label = s.Label, Channel = s.Channel }));
        });

        mb.Entity<RdpSalesUpload>(e =>
        {
            e.Property(x => x.FileName).HasMaxLength(260);
            e.Property(x => x.CustKeys).HasMaxLength(500);
            e.Property(x => x.Periods).HasMaxLength(500);
            e.Property(x => x.NetAmount).HasPrecision(18, 2);
            e.Property(x => x.UploadedBy).HasMaxLength(100);
        });

        mb.Entity<RdpSalesLine>(e =>
        {
            e.HasIndex(x => new { x.DistributorId, x.Period });
            e.HasIndex(x => x.UploadId);
            e.HasOne<RdpSalesUpload>().WithMany().HasForeignKey(x => x.UploadId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<RdpDistributor>().WithMany().HasForeignKey(x => x.DistributorId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.InvoiceNo).HasMaxLength(50);
            e.Property(x => x.TxnType).HasMaxLength(20);
            e.Property(x => x.AccountCode).HasMaxLength(50);
            e.Property(x => x.AccountName).HasMaxLength(200);
            e.Property(x => x.AccountKey).HasMaxLength(260);
            e.Property(x => x.ChannelLabel).HasMaxLength(100);
            e.Property(x => x.Province).HasMaxLength(100);
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.Salesman).HasMaxLength(100);
            e.Property(x => x.SkuCode).HasMaxLength(20);
            e.Property(x => x.ItemDesc).HasMaxLength(200);
            e.Property(x => x.Uom).HasMaxLength(20);
            e.Property(x => x.Quantity).HasPrecision(18, 4);
            e.Property(x => x.Cases).HasPrecision(18, 4);
            e.Property(x => x.NetAmount).HasPrecision(18, 2);
        });
    }
}
