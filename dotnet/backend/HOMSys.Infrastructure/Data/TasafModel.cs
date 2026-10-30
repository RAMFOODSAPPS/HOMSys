using HOMSys.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Data;

/// <summary>
/// EF mapping + seed rows for the TASAF master lists. Seed values are the DATA
/// sheet of "TASA Form_MT version template 2026.xlsx" (trade marketing, 2026).
/// </summary>
internal static class TasafModel
{
    private static readonly DateTime SeedAt = new(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);

    public static void Configure(ModelBuilder mb)
    {
        Lookup<TasafSalesGroup>(mb);
        Lookup<TasafChain>(mb);
        Lookup<TasafArea>(mb);
        Lookup<TasafProgramType>(mb);
        Lookup<TasafObjective>(mb);
        Lookup<TasafTargetMarket>(mb);
        Lookup<TasafChannel>(mb);
        Lookup<TasafProductGroup>(mb);
        Lookup<TasafCostType>(mb);
        Lookup<TasafApprovalRole>(mb);
        Lookup<TasafApprovalType>(mb);

        mb.Entity<TasafProgramType>()
            .HasOne(x => x.ApprovalType).WithMany()
            .HasForeignKey(x => x.ApprovalTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<TasafApprovalRule>(e =>
        {
            e.HasIndex(x => new { x.ApprovalTypeId, x.RoleId }).IsUnique();
            e.HasOne(x => x.ApprovalType).WithMany().HasForeignKey(x => x.ApprovalTypeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.OverAmount).HasPrecision(18, 2);
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
        });

        mb.Entity<TasafChain>().Property(x => x.CustomerMatch).HasMaxLength(500);
        mb.Entity<TasafArea>().Property(x => x.BranchCodes).HasMaxLength(500);
        mb.Entity<TasafSalesGroup>().Property(x => x.AreaNames).HasMaxLength(500);
        mb.Entity<TasafChain>().Property(x => x.AreaNames).HasMaxLength(500);
        mb.Entity<TasafChain>()
            .HasOne(x => x.SalesGroup).WithMany()
            .HasForeignKey(x => x.SalesGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<TasafProgramType>()
            .Property(x => x.MaxCtsPercent).HasPrecision(5, 2);

        mb.Entity<TasafApprover>(e =>
        {
            e.HasIndex(x => new { x.SalesGroupId, x.RoleId }).IsUnique();
            e.HasOne(x => x.SalesGroup).WithMany().HasForeignKey(x => x.SalesGroupId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
        });

        ConfigureDocument(mb);
        Seed(mb);
    }

    private static void ConfigureDocument(ModelBuilder mb)
    {
        // TASAF Setup › Numbering — single row.
        mb.Entity<TasafNumbering>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
            e.HasData(new TasafNumbering { Id = 1, NextNumber = 1, MinDigits = 2 });
        });

        mb.Entity<Tasaf>(e =>
        {
            e.Property(x => x.RefNo).HasMaxLength(30);
            e.HasIndex(x => x.RefNo).IsUnique().HasFilter("[RefNo] IS NOT NULL");
            e.HasIndex(x => x.RefSeq).IsUnique().HasFilter("[RefSeq] IS NOT NULL");
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.ProponentUserId);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.ProgramTypeOther).HasMaxLength(200);
            e.Property(x => x.ObjectiveOther).HasMaxLength(200);
            e.Property(x => x.TargetMarketOther).HasMaxLength(200);
            e.Property(x => x.ProponentName).HasMaxLength(150);
            e.Property(x => x.CtsJustification).HasMaxLength(1000);
            e.Property(x => x.CreatedBy).HasMaxLength(100);
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
            foreach (var p in new[] { nameof(Tasaf.TotalCost), nameof(Tasaf.PromotedValue), nameof(Tasaf.PrePromoSales),
                         nameof(Tasaf.IncrementalSales), nameof(Tasaf.PayInvDeductionAp), nameof(Tasaf.PayInvDeductionSalesman),
                         nameof(Tasaf.PayCheckAp), nameof(Tasaf.PayCheckSalesman), nameof(Tasaf.PayFreeGoodsAp),
                         nameof(Tasaf.PayFreeGoodsSalesman), nameof(Tasaf.PayOffInvoiceAp), nameof(Tasaf.PayOffInvoiceSalesman) })
                e.Property(p).HasPrecision(18, 2);

            // Lookups: a referenced master row can't be deleted (deactivate it instead).
            e.HasOne(x => x.SalesGroup).WithMany().HasForeignKey(x => x.SalesGroupId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ProgramType).WithMany().HasForeignKey(x => x.ProgramTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Objective).WithMany().HasForeignKey(x => x.ObjectiveId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.TargetMarket).WithMany().HasForeignKey(x => x.TargetMarketId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ProponentUser).WithMany().HasForeignKey(x => x.ProponentUserId).OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.Areas).WithOne().HasForeignKey(x => x.TasafId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Chains).WithOne().HasForeignKey(x => x.TasafId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.ProductCategories).WithOne().HasForeignKey(x => x.TasafId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.SubCategories).WithOne().HasForeignKey(x => x.TasafId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Channels).WithOne().HasForeignKey(x => x.TasafId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Branches).WithOne().HasForeignKey(x => x.TasafId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Customers).WithOne().HasForeignKey(x => x.TasafId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Products).WithOne().HasForeignKey(x => x.TasafId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Costs).WithOne().HasForeignKey(x => x.TasafId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Actions).WithOne().HasForeignKey(x => x.TasafId).OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Steps).WithOne().HasForeignKey(x => x.TasafId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.CurrentApproverUserId);
            e.Property(x => x.ApprovalTypeName).HasMaxLength(150);
        });

        // Plain ids (no FK): the chain is a snapshot taken at submit, kept even if
        // the user or role is later removed.
        mb.Entity<TasafApprovalStep>(e =>
        {
            e.HasIndex(x => x.UserId);
            e.Property(x => x.RoleCode).HasMaxLength(20);
            e.Property(x => x.RoleName).HasMaxLength(150);
            e.Property(x => x.UserName).HasMaxLength(150);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.Property(x => x.EmailTokenHash).HasMaxLength(64);
            e.HasIndex(x => x.EmailTokenHash).HasFilter("[EmailTokenHash] IS NOT NULL");
        });

        mb.Entity<TasafAction>(e =>
        {
            e.Property(x => x.Action).HasMaxLength(20);
            e.Property(x => x.FromStatus).HasMaxLength(20);
            e.Property(x => x.ToStatus).HasMaxLength(20);
            e.Property(x => x.ByName).HasMaxLength(150);
            e.Property(x => x.Role).HasMaxLength(150);
            e.Property(x => x.Comment).HasMaxLength(1000);
        });

        mb.Entity<Notification>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.ReadAt });
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Message).HasMaxLength(500);
            e.Property(x => x.Link).HasMaxLength(200);
            e.Property(x => x.EntityType).HasMaxLength(50);
        });

        mb.Entity<TasafCustomer>(e =>
        {
            e.Property(x => x.CustKey).HasMaxLength(20);
            e.Property(x => x.CusName).HasMaxLength(150);
            e.HasIndex(x => x.CustKey);
        });

        mb.Entity<TasafSalesGroupUser>(e =>
        {
            e.ToTable("TasafSalesGroupUsers");
            e.HasIndex(x => new { x.SalesGroupId, x.UserId }).IsUnique();
            e.HasOne(x => x.SalesGroup).WithMany(g => g.Members).HasForeignKey(x => x.SalesGroupId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // Worksheets (LF Breakdown / Near Ex) attached to a TASAF. Deleting a TASAF only detaches them.
        mb.Entity<TasafWorksheet>(e =>
        {
            e.ToTable("TasafWorksheets");
            e.Property(x => x.Type).HasMaxLength(20);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.ProponentName).HasMaxLength(150);
            e.Property(x => x.CreatedBy).HasMaxLength(100);
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
            e.HasOne(x => x.ProponentUser).WithMany().HasForeignKey(x => x.ProponentUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Tasaf).WithMany(t => t.Worksheets).HasForeignKey(x => x.TasafId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.ListingFeeLines).WithOne().HasForeignKey(x => x.WorksheetId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.NearExLines).WithOne().HasForeignKey(x => x.WorksheetId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<TasafListingFeeLine>(e =>
        {
            e.ToTable("TasafListingFeeLines");
            foreach (var p in new[] { nameof(TasafListingFeeLine.SalesGroup), nameof(TasafListingFeeLine.Channel),
                                      nameof(TasafListingFeeLine.Account), nameof(TasafListingFeeLine.Category) })
                e.Property(p).HasMaxLength(150);
            e.Property(x => x.Basis).HasMaxLength(20);
            e.Property(x => x.Remarks).HasMaxLength(500);
            e.Property(x => x.LfPerSku).HasPrecision(18, 2);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.Property(x => x.ProjectedAnnualSales).HasPrecision(18, 2);
            e.Property(x => x.Cts).HasPrecision(9, 6);
        });

        mb.Entity<TasafNearExLine>(e =>
        {
            e.ToTable("TasafNearExLines");
            foreach (var p in new[] { nameof(TasafNearExLine.SalesGroup), nameof(TasafNearExLine.Account),
                                      nameof(TasafNearExLine.Category), nameof(TasafNearExLine.Sku),
                                      nameof(TasafNearExLine.MoveoutScheme) })
                e.Property(p).HasMaxLength(150);
            e.Property(x => x.LspPerCase).HasPrecision(18, 2);
            e.Property(x => x.LspPerPc).HasPrecision(18, 2);
            e.Property(x => x.TotalValue).HasPrecision(18, 2);
            e.Property(x => x.Discount).HasPrecision(9, 6);
            e.Property(x => x.MoveoutCost).HasPrecision(18, 2);
        });

        mb.Entity<TasafChainCustomer>(e =>
        {
            e.ToTable("TasafChainCustomers");
            // A store can be listed more than once under a chain (another region / sales group), as in the Excel.
            e.HasIndex(x => new { x.ChainId, x.CustKey, x.SalesGroup, x.AreaName }).IsUnique();
            e.HasOne(x => x.Chain).WithMany().HasForeignKey(x => x.ChainId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.CustKey).HasMaxLength(20);
            e.Property(x => x.CusName).HasMaxLength(150);
            e.Property(x => x.AreaName).HasMaxLength(100);
            e.Property(x => x.SiteCode).HasMaxLength(20);
            e.Property(x => x.SalesGroup).HasMaxLength(20);
        });

        mb.Entity<TasafChannelLink>(e =>
        {
            e.ToTable("TasafChannelLinks");
            e.HasOne(x => x.Channel).WithMany().HasForeignKey(x => x.ChannelId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<TasafAreaLink>(e =>
        {
            e.ToTable("TasafAreaLinks");
            e.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<TasafChainLink>(e =>
        {
            e.ToTable("TasafChainLinks");
            e.HasOne(x => x.Chain).WithMany().HasForeignKey(x => x.ChainId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<TasafProductCategoryLink>(e =>
        {
            e.ToTable("TasafProductCategoryLinks");
            e.HasOne(x => x.ProductGroup).WithMany().HasForeignKey(x => x.ProductGroupId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<TasafSubCategoryLink>(e =>
        {
            e.ToTable("TasafSubCategoryLinks");
            e.Property(x => x.Code).HasMaxLength(10);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Segment).HasMaxLength(100);
        });

        mb.Entity<TasafBranch>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<TasafProduct>(e =>
        {
            e.Property(x => x.CProdNo).HasMaxLength(10);
            e.Property(x => x.Sku).HasMaxLength(100);
            e.Property(x => x.P3MoAvgCs).HasPrecision(18, 2);
            e.Property(x => x.SplyCs).HasPrecision(18, 2);
            e.Property(x => x.PromotedVolCs).HasPrecision(18, 2);
            e.Property(x => x.IncrVolCs).HasPrecision(18, 2);
            e.Property(x => x.LspPerCase).HasPrecision(18, 2);
            e.Property(x => x.LspComputed).HasPrecision(18, 2);
            e.Property(x => x.PromotedValue).HasPrecision(18, 2);
        });

        mb.Entity<TasafCost>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Remarks).HasMaxLength(500);
            e.HasOne(x => x.CostType).WithMany().HasForeignKey(x => x.CostTypeId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void Lookup<T>(ModelBuilder mb, bool uniqueName = true) where T : TasafLookup
    {
        mb.Entity<T>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.CreatedBy).HasMaxLength(100);
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
            if (uniqueName) e.HasIndex(x => x.Name).IsUnique();
            e.HasIndex(x => x.Code).IsUnique().HasFilter("[Code] != ''");
        });
    }

    private static T Row<T>(int id, string code, string name, string description = "") where T : TasafLookup, new() => new()
    {
        Id = id, Code = code, Name = name, Description = description,
        SortOrder = id, IsActive = true, CreatedAt = SeedAt, CreatedBy = "seed"
    };

    private static T Inactive<T>(T row) where T : TasafLookup { row.IsActive = false; return row; }

    private static T[] Rows<T>(params string[] names) where T : TasafLookup, new() =>
        names.Select((n, i) => Row<T>(i + 1, "", n)).ToArray();

    private static void Seed(ModelBuilder mb)
    {
        mb.Entity<TasafSalesGroup>().HasData(
            Row<TasafSalesGroup>(1, "NKA", "NKA", "National Key Accounts"),
            Row<TasafSalesGroup>(2, "RKA", "RKA", "Regional Key Accounts"),
            Row<TasafSalesGroup>(3, "LKA", "LKA", "Local Key Accounts"),
            Row<TasafSalesGroup>(4, "GT",  "GT",  "General Trade"));

        // Sales group per chain is not in the workbook — assigned on the setup page.
        mb.Entity<TasafChain>().HasData(Rows<TasafChain>(
            "SVI", "Waltermart", "Puregold", "Landmark", "Robinson", "Suysing", "Super 8",
            "CSI", "RCS Chain", "Savewise", "LCC", "Citi Mart", "Prince", "Metro Gaisano",
            "NCCC", "KCC", "Gaisano Grand", "Gaisano Capital", "Gaisano Mall", "LKA Others", "RDP Others"));

        // The workbook areas. Migration TasafAreasByRegion deactivates them and adds the
        // customer master's regions (NCR, North Luzon, … MT/NKA); old TASAFs keep these.
        mb.Entity<TasafArea>().HasData(Rows<TasafArea>(
            "GMA / NCR", "North Central Luzon", "South Luzon", "Bicol", "East Visayas", "West Visayas",
            "North Mindanao", "South Mindanao", "Total Luzon", "Total Visayas", "Total Mindanao",
            "Total VisMin", "Nationwide"));

        // The two approval matrices of "Data Fields for IT.xlsx" › TASAF.
        const int PromoType = 1, SupportType = 2;
        mb.Entity<TasafApprovalType>().HasData(
            Row<TasafApprovalType>(PromoType,   "PROMO",   "Promo / Near Ex Moveout",
                "By amount. Include CTS as a metric; promo packs must be within 8%."),
            Row<TasafApprovalType>(SupportType, "SUPPORT", "Listing Fees, Conventions, Other Account Support",
                "Must go to NSD and COO regardless of amount."));

        // Promo packs must be within 8% CTS. Listing fees, conventions and account
        // support use the Account Support matrix; everything else the Promo one.
        var programTypes = Rows<TasafProgramType>(
            "PROMO- EndCon- Bundling/Tie-Up", "PROMO- EndCon- Price-Off/Discount",
            "PROMO- Reseller- Bundling/Tie-Up", "PROMO- Trade- Case/Trade Deal",
            "ACCOUNT SUPPORT- Anniversary Support", "ACCOUNT SUPPORT- Opening Support",
            "ACCOUNT SUPPORT- Sponsorship", "Discount", "Listing Fee", "Convention",
            "Near Ex Moveout", "Free Goods", "Others");
        foreach (var pt in programTypes)
        {
            if (pt.Name.StartsWith("PROMO-")) pt.MaxCtsPercent = 8m;
            pt.ApprovalTypeId = pt.Name.StartsWith("ACCOUNT SUPPORT-") || pt.Name is "Listing Fee" or "Convention"
                ? SupportType : PromoType;
        }
        mb.Entity<TasafProgramType>().HasData(programTypes);

        mb.Entity<TasafObjective>().HasData(Rows<TasafObjective>(
            "Distribution", "Generate Trial", "Increase AWOP", "Create Brand Loyalty", "Others"));

        mb.Entity<TasafTargetMarket>().HasData(Rows<TasafTargetMarket>(
            "Shopper/End Con", "Reseller (Indirect Distribution)", "Channel", "Region", "Others"));

        mb.Entity<TasafChannel>().HasData(Rows<TasafChannel>(
            "MT Supermarket", "MT Convenience Store", "MT Grocery", "MT WS/Reseller",
            "GT Supermarkets & Groceries", "GT Market Stall & SSS", "GT Wholesaler", "ALL CHANNELS"));

        mb.Entity<TasafProductGroup>().HasData(
            Row<TasafProductGroup>(1,  "01",   "Tom Sauce/Paste"),
            Row<TasafProductGroup>(2,  "02",   "Peas & Beans"),
            Row<TasafProductGroup>(3,  "03",   "Pasta & Sauces"),
            Row<TasafProductGroup>(4,  "04",   "Mitra"),
            Row<TasafProductGroup>(5,  "05",   "Raisins"),
            Row<TasafProductGroup>(6,  "06",   "Pickles"),
            Row<TasafProductGroup>(7,  "07",   "Fruits & Vegetables"),
            Row<TasafProductGroup>(8,  "08",   "Baking & Dry Goods"),
            Row<TasafProductGroup>(9,  "09",   "Condiments"),
            Inactive(Row<TasafProductGroup>(10, "CORP", "TOTAL RAM / CORPORATE PROGRAM")), // not in Book2.xlsx
            Row<TasafProductGroup>(11, "10",   "Non Food & Toll Pack"));

        mb.Entity<TasafCostType>().HasData(Rows<TasafCostType>(
            "Promotions - Bundling Fee", "Promotions - Joining Fee", "Promotions - Rebates",
            "Promotions - Bundling Materials", "Promotions - Free Goods",
            "Anniversary Support - Monetary Support", "Anniversary Support - Promo Rebates",
            "Anniversary Support - Dry Sampling", "Anniversary Support - Manpower",
            "Opening Support - Monetary Support", "Opening Support - Promo Rebates",
            "Opening Support - Dry Sampling", "Opening Support - Manpower",
            "Sponsorship - Free Goods", "Sponsorship - Monetary Support",
            "Discount - Regular Discount", "Discount - Special/ Wholesale Discount",
            "Discount - LGU Discount", "Discount - OND Discount",
            "Listing Fee - Listing Fee", "Listing Fee - Intro Discount",
            "Convention - Joining Fee", "Convention - Rebates", "Convention - Manpower",
            "Convention - Booth & Collaterals", "Near Ex Moveout - Rebates", "Free Goods - Free Goods"));

        // Simplified Approval Matrix roles, in signing order. Who holds each role per
        // sales group is set on the setup page (TasafApprover).
        const int Gkam = 1, Tmm = 2, Nsm = 3, Nsd = 4, Coo = 5;
        mb.Entity<TasafApprovalRole>().HasData(
            Row<TasafApprovalRole>(Gkam, "GKAM", "Group Key Account Manager"),
            Row<TasafApprovalRole>(Tmm,  "TMM",  "Trade Marketing Manager"),
            Row<TasafApprovalRole>(Nsm,  "NSM",  "National Sales Manager"),
            Row<TasafApprovalRole>(Nsd,  "NSD",  "National Sales Director"),
            Row<TasafApprovalRole>(Coo,  "COO",  "Chief Operating Officer"));

        // Rules (OverAmount null = always; signs only when total is MORE THAN it):
        //   Promo:   GKAM, TMM · NSM > 20,000 · NSD > 50,000 · COO > 100,000
        //   Support: GKAM, TMM · NSM > 20,000 · NSD and COO always
        var rule = 0;
        TasafApprovalRule Rule(int type, int role, decimal? over) =>
            new() { Id = ++rule, ApprovalTypeId = type, RoleId = role, OverAmount = over };
        mb.Entity<TasafApprovalRule>().HasData(
            Rule(PromoType,   Gkam, null), Rule(PromoType,   Tmm, null), Rule(PromoType,   Nsm, 20000m),
            Rule(PromoType,   Nsd, 50000m), Rule(PromoType,  Coo, 100000m),
            Rule(SupportType, Gkam, null), Rule(SupportType, Tmm, null), Rule(SupportType, Nsm, 20000m),
            Rule(SupportType, Nsd, null),  Rule(SupportType, Coo, null));
    }
}
