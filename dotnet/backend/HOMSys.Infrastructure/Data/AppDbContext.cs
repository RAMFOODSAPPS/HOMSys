using HOMSys.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HOMSys.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<SiteType> SiteTypes => Set<SiteType>();

    // BMS reference data (seeded from DBF, read-only in HOMSys)
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<PoLog> PoLogs => Set<PoLog>();
    public DbSet<DocClass> DocClasses => Set<DocClass>();

    // Category grouping for products, seeded from F:\PMDM\prodcat.dbf — see
    // PricingDataImporter. Drives pricelist export grouping/order.
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();

    // SKUs hidden from the pricelist export unless the customer's CZone is
    // an allowed zone, seeded from F:\PMDM\PRLISTX2.DBF — see PricingDataImporter.
    public DbSet<PrlistX2Restriction> PrlistX2Restrictions => Set<PrlistX2Restriction>();

    // SKUs fully excluded from the pricelist export for every account, no
    // zone exception, seeded from F:\PMDM\PRLISTX.DBF — see PricingDataImporter.
    public DbSet<PrlistXRestriction> PrlistXRestrictions => Set<PrlistXRestriction>();

    // Pricing masters (seeded from the Pricing Adjustment subsystem's staging
    // DBFs at C:\claude\pricing, not legacy\dbf — see PricingDataImporter)
    public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();
    public DbSet<ZoneAddOn> ZoneAddOns => Set<ZoneAddOn>();
    public DbSet<Zone2AddOn> Zone2AddOns => Set<Zone2AddOn>();

    // Per-branch CZone from F:\AUTOPROG\CUSTOMER\{branch}\cust4win.dbf — the
    // pricing-lookup source of truth for CZone (Customer.CZone is BMSRAM-sourced
    // and may lag branch-side updates). See PricingDataImporter.
    public DbSet<CustomerBranchZone> CustomerBranchZones => Set<CustomerBranchZone>();

    // Free-text customer identifier -> CustKey, learned from the "Import by
    // Customer Name" SO import mapping dialog.
    public DbSet<CustomerIdentifierMap> CustomerIdentifierMaps => Set<CustomerIdentifierMap>();

    // Sales order encoding
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderLine> SalesOrderLines => Set<SalesOrderLine>();

    // Bridge-synced oowkdet snapshot for the OOS report — see OosSyncLine.
    public DbSet<OosSyncLine> OosSyncLines => Set<OosSyncLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRole>()
            .HasKey(ur => new { ur.UserId, ur.RoleId });

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId);

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId);

        modelBuilder.Entity<RolePermission>()
            .HasKey(rp => new { rp.RoleId, rp.PermissionId });

        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId);

        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId);

        ConfigureSalesOrderModule(modelBuilder);

        modelBuilder.Entity<ProductCategory>(e =>
        {
            e.HasKey(x => x.CategoryCode);
            e.Property(x => x.CategoryCode).HasMaxLength(4);
            e.Property(x => x.GroupDesc).HasMaxLength(30);
            e.Property(x => x.SubCat).HasMaxLength(50);
        });

        modelBuilder.Entity<PrlistX2Restriction>(e =>
        {
            e.HasIndex(x => new { x.CProdNo, x.Zone }).IsUnique();
            e.Property(x => x.CProdNo).HasMaxLength(4).IsRequired();
            e.Property(x => x.Zone).HasMaxLength(4).IsRequired();
        });

        modelBuilder.Entity<PrlistXRestriction>(e =>
        {
            e.HasIndex(x => x.CProdNo).IsUnique();
            e.Property(x => x.CProdNo).HasMaxLength(4).IsRequired();
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.Property(x => x.Category).HasMaxLength(4);
            e.Property(x => x.Barcode).HasMaxLength(16);
            e.Property(x => x.CaseBarcode).HasMaxLength(18);
            e.HasIndex(x => x.Category);
        });

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username).IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email).IsUnique();

        modelBuilder.Entity<Role>()
            .HasIndex(r => r.Name).IsUnique();

        modelBuilder.Entity<Permission>()
            .HasIndex(p => p.Key).IsUnique();

        modelBuilder.Entity<Company>()
            .HasIndex(c => c.Name).IsUnique();

        modelBuilder.Entity<Company>()
            .HasIndex(c => c.Code).IsUnique()
            .HasFilter("[Code] != ''");

        modelBuilder.Entity<Department>()
            .HasOne(d => d.Company)
            .WithMany()
            .HasForeignKey(d => d.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Department>()
            .HasIndex(d => new { d.CompanyId, d.Name }).IsUnique();

        modelBuilder.Entity<Department>()
            .HasIndex(d => new { d.CompanyId, d.Code }).IsUnique()
            .HasFilter("[Code] != ''");

        modelBuilder.Entity<Site>()
            .HasOne(s => s.Company)
            .WithMany()
            .HasForeignKey(s => s.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Site>()
            .HasOne(s => s.SiteType)
            .WithMany()
            .HasForeignKey(s => s.SiteTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Site>()
            .HasIndex(s => new { s.CompanyId, s.Name }).IsUnique();

        modelBuilder.Entity<Site>()
            .HasIndex(s => new { s.CompanyId, s.Code }).IsUnique()
            .HasFilter("[Code] != ''");

        modelBuilder.Entity<SiteType>()
            .HasIndex(st => st.Name).IsUnique();

        modelBuilder.Entity<SiteType>()
            .HasIndex(st => st.Code).IsUnique()
            .HasFilter("[Code] != ''");

        // Seed roles
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Admin", Description = "Full system access", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        // Seed permissions (these keys match route data.permission values)
        modelBuilder.Entity<Permission>().HasData(
            new Permission { Id = 1, Key = "users",         Name = "User Management",          Description = "Create, edit, and delete users" },
            new Permission { Id = 2, Key = "roles",         Name = "Role Management",           Description = "Create, edit, and delete roles" },
            new Permission { Id = 3, Key = "authorization", Name = "Authorization Management",  Description = "Configure role-based access to pages" },
            new Permission { Id = 4, Key = "companies",     Name = "Company Management",        Description = "Create, edit, and delete companies" },
            new Permission { Id = 5, Key = "departments",   Name = "Department Management",  Description = "Create, edit, and delete departments" },
            new Permission { Id = 6, Key = "sites",          Name = "Site Management",         Description = "Create, edit, and delete sites" },
            new Permission { Id = 7, Key = "site-types",     Name = "Site Type Management",     Description = "Create, edit, and delete site types" },
            new Permission { Id = 8, Key = "sales-orders",  Name = "Sales Order Encoding",      Description = "Encode and view sales orders" },
            new Permission { Id = 9, Key = "oos-report",     Name = "OOS Report",                Description = "View out-of-stock report for sales orders" },
            new Permission { Id = 10, Key = "pricelist-export", Name = "Pricelist Export",       Description = "Generate branch pricelist Excel exports" },
            new Permission { Id = 11, Key = "legacy-monitoring", Name = "Legacy Monitoring",      Description = "View legacy DBF sync status and trigger manual syncs" }
        );

        // Admin gets all permissions by default
        modelBuilder.Entity<RolePermission>().HasData(
            new RolePermission { RoleId = 1, PermissionId = 1 },
            new RolePermission { RoleId = 1, PermissionId = 2 },
            new RolePermission { RoleId = 1, PermissionId = 3 },
            new RolePermission { RoleId = 1, PermissionId = 4 },
            new RolePermission { RoleId = 1, PermissionId = 5 },
            new RolePermission { RoleId = 1, PermissionId = 6 },
            new RolePermission { RoleId = 1, PermissionId = 7 },
            new RolePermission { RoleId = 1, PermissionId = 8 },
            new RolePermission { RoleId = 1, PermissionId = 9 },
            new RolePermission { RoleId = 1, PermissionId = 10 },
            new RolePermission { RoleId = 1, PermissionId = 11 }
        );

        // Seed default admin user (password: Admin@1234)
        modelBuilder.Entity<User>().HasData(new User
        {
            Id = 1,
            Username = "admin",
            Email = "admin@homsys.local",
            PasswordHash = "$2a$11$HWPtG2hulzuprKTyJrT/cOhS/k.aBx.7jJZJaRYwwVkbs5D5IUvAq",
            FirstName = "System",
            LastName = "Administrator",
            IsActive = true,
            MustChangePassword = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        modelBuilder.Entity<UserRole>().HasData(
            new UserRole { UserId = 1, RoleId = 1 }
        );
    }

    /// <summary>
    /// Sales order encoding module. Column widths mirror the source DBF field
    /// definitions so the Python bridge can write back without truncation
    /// surprises. See legacy/vfp/ANALYSIS.md.
    /// </summary>
    private static void ConfigureSalesOrderModule(ModelBuilder b)
    {
        // ── Customer (from cust4win.DBF) ─────────────────────────────────────
        b.Entity<Customer>(e =>
        {
            e.HasIndex(x => x.CustKey).IsUnique();
            e.HasIndex(x => x.CusName);
            e.Property(x => x.CustKey).HasMaxLength(7).IsRequired();
            e.Property(x => x.CKey).HasMaxLength(5);
            e.Property(x => x.CusName).HasMaxLength(50);
            e.Property(x => x.AddrLn1).HasMaxLength(75);
            e.Property(x => x.AddrLn2).HasMaxLength(75);
            e.Property(x => x.DelAddrLn1).HasMaxLength(75);
            e.Property(x => x.DelAddrLn2).HasMaxLength(75);
            e.Property(x => x.DelArea).HasMaxLength(75);
            e.Property(x => x.CsMan).HasMaxLength(4);
            e.Property(x => x.CZone).HasMaxLength(4);
            e.Property(x => x.VatId).HasMaxLength(1);
            e.Property(x => x.Subd).HasMaxLength(20);
            e.Property(x => x.Tin).HasMaxLength(20);
            e.Property(x => x.AliasKey).HasMaxLength(7);
            e.Property(x => x.ConsoMax2).HasMaxLength(7);
        });

        // ── DocClass (from docclass.DBF) ─────────────────────────────────────
        b.Entity<DocClass>(e =>
        {
            e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasMaxLength(1).IsRequired();
            e.Property(x => x.Description).HasMaxLength(20);
        });

        // ── Product (from prod4win.DBF) ──────────────────────────────────────
        b.Entity<Product>(e =>
        {
            e.HasIndex(x => x.CProdNo).IsUnique();
            e.Property(x => x.CProdNo).HasMaxLength(4).IsRequired();
            e.Property(x => x.ProdDesc).HasMaxLength(75);
            e.Property(x => x.PackSize).HasMaxLength(10);
            e.Property(x => x.Um).HasMaxLength(3);
            e.Property(x => x.TaxRate).HasPrecision(6, 4);
            e.Property(x => x.NewPrice).HasPrecision(8, 2);
            e.Property(x => x.OldPrice1).HasPrecision(8, 2);
            e.Property(x => x.Srp).HasPrecision(7, 2);
        });

        // ── PriceHistory (from prchst.DBF) ───────────────────────────────────
        b.Entity<PriceHistory>(e =>
        {
            e.HasIndex(x => x.ProdNo);
            e.HasIndex(x => x.RecNo);
            e.Property(x => x.NpAfVat).HasPrecision(8, 2);
        });

        // ── ZoneAddOn (from addon\{branch}\zone.DBF) ─────────────────────────
        b.Entity<ZoneAddOn>(e =>
        {
            e.HasIndex(x => new { x.Branch, x.RecNo });
            e.HasIndex(x => new { x.Branch, x.CProdNo, x.CZone });
            e.Property(x => x.Branch).HasMaxLength(10).IsRequired();
            e.Property(x => x.CProdNo).HasMaxLength(4).IsRequired();
            e.Property(x => x.CZone).HasMaxLength(4).IsRequired();
            e.Property(x => x.AddOn).HasPrecision(12, 4);
            e.Property(x => x.Rate).HasPrecision(9, 6);
            e.Property(x => x.FixAmt).HasPrecision(9, 4);
        });

        // ── Zone2AddOn (from addon\{branch}\zone2.DBF) ───────────────────────
        b.Entity<Zone2AddOn>(e =>
        {
            e.HasIndex(x => new { x.Branch, x.RecNo });
            e.HasIndex(x => new { x.Branch, x.CProdNo, x.CustKey });
            e.Property(x => x.Branch).HasMaxLength(10).IsRequired();
            e.Property(x => x.CustKey).HasMaxLength(7).IsRequired();
            e.Property(x => x.CProdNo).HasMaxLength(4).IsRequired();
            e.Property(x => x.AddOn).HasPrecision(7, 2);
            e.Property(x => x.Rate).HasPrecision(9, 4);
            e.Property(x => x.FixAmt).HasPrecision(9, 4);
        });

        // ── CustomerBranchZone (from F:\AUTOPROG\CUSTOMER\{branch}\cust4win.DBF) ──
        b.Entity<CustomerBranchZone>(e =>
        {
            e.HasIndex(x => x.CustKey);
            e.HasIndex(x => x.Branch);
            e.HasIndex(x => new { x.Branch, x.RecNo });
            e.Property(x => x.Branch).HasMaxLength(10).IsRequired();
            e.Property(x => x.CustKey).HasMaxLength(7).IsRequired();
            e.Property(x => x.CZone).HasMaxLength(4).IsRequired();
        });

        // ── CustomerIdentifierMap (learned from "Import by Customer Name") ──
        b.Entity<CustomerIdentifierMap>(e =>
        {
            e.HasIndex(x => x.Identifier).IsUnique();
            e.Property(x => x.Identifier).HasMaxLength(100).IsRequired();
            e.Property(x => x.CustKey).HasMaxLength(7).IsRequired();
            e.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
        });

        // ── PoLog (from pofiles.DBF, appended to on save) ────────────────────
        b.Entity<PoLog>(e =>
        {
            // Deliberately NOT unique: the legacy form warns on a duplicate PO
            // and lets the operator continue, so duplicates are legal data.
            e.HasIndex(x => x.PoNum);
            e.Property(x => x.PoNum).HasMaxLength(15).IsRequired();
            e.Property(x => x.CustKey).HasMaxLength(7);
            e.Property(x => x.CusName).HasMaxLength(50);

            e.HasOne(x => x.SalesOrder)
                .WithMany()
                .HasForeignKey(x => x.SoId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ── SalesOrder (mirrors oowkhdr.DBF) ─────────────────────────────────
        b.Entity<SalesOrder>(e =>
        {
            e.HasKey(x => x.SoId);
            e.HasIndex(x => x.CustKey);
            e.HasIndex(x => x.PoNum);
            e.HasIndex(x => x.OrderDate);

            // The bridge fills these; index so it can find unpushed orders.
            e.HasIndex(x => x.SoNo);

            e.Property(x => x.CustKey).HasMaxLength(7).IsRequired();
            e.Property(x => x.CusName).HasMaxLength(50);
            e.Property(x => x.CKey).HasMaxLength(5);
            e.Property(x => x.ShipToLn1).HasMaxLength(75);
            e.Property(x => x.ShipToLn2).HasMaxLength(75);
            e.Property(x => x.DelArea).HasMaxLength(75);
            e.Property(x => x.CsMan).HasMaxLength(4);
            e.Property(x => x.PoNum).HasMaxLength(15);
            e.Property(x => x.InvRem).HasMaxLength(100);
            e.Property(x => x.Remarks).HasMaxLength(60);
            e.Property(x => x.VatId).HasMaxLength(1);
            e.Property(x => x.UserName).HasMaxLength(20);
            e.Property(x => x.SoElapsed).HasMaxLength(75);
            e.Property(x => x.OrAmt).HasPrecision(10, 2);
            e.Property(x => x.DocClass).HasMaxLength(1);

            e.HasIndex(x => x.SourceFileHash);
            e.Property(x => x.SourceFileHash).HasMaxLength(64);
            e.Property(x => x.SourceFileName).HasMaxLength(255);

            e.HasMany(x => x.Lines)
                .WithOne(l => l.SalesOrder)
                .HasForeignKey(l => l.SoId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.OosSyncLines)
                .WithOne(l => l.SalesOrder)
                .HasForeignKey(l => l.SoId)
                .OnDelete(DeleteBehavior.Cascade);

            ConfigureBmsOwnedHeader(e);
        });

        // ── OosSyncLine (bridge-synced oowkdet snapshot) ─────────────────────
        b.Entity<OosSyncLine>(e =>
        {
            e.HasIndex(x => new { x.SoId, x.CProdNo }).IsUnique();
            e.Property(x => x.CProdNo).HasMaxLength(4).IsRequired();
            e.Property(x => x.NetAmt).HasPrecision(12, 2);
        });

        // ── SalesOrderLine (mirrors oowkdet.DBF) ─────────────────────────────
        b.Entity<SalesOrderLine>(e =>
        {
            e.HasIndex(x => new { x.SoId, x.LineNo });
            e.Property(x => x.CProdNo).HasMaxLength(4).IsRequired();
            // oowkdet.PRODDESC is C(50) even though prod4win.PRODDESC is C(75).
            e.Property(x => x.ProdDesc).HasMaxLength(50);
            e.Property(x => x.PackSize).HasMaxLength(10);
            e.Property(x => x.Um).HasMaxLength(3);
            e.Property(x => x.CSupplier).HasMaxLength(2);
            e.Property(x => x.TaxRate).HasPrecision(6, 4);
            e.Property(x => x.Batch).HasMaxLength(10);
            e.Property(x => x.DrpCust).HasMaxLength(7);

            foreach (var money in new[]
                     {
                         nameof(SalesOrderLine.Price), nameof(SalesOrderLine.ZPrice),
                         nameof(SalesOrderLine.Amt), nameof(SalesOrderLine.NetAmt),
                         nameof(SalesOrderLine.Taxable), nameof(SalesOrderLine.Tax),
                         nameof(SalesOrderLine.Cost), nameof(SalesOrderLine.Uc),
                         nameof(SalesOrderLine.OldAmt), nameof(SalesOrderLine.OldCost),
                         nameof(SalesOrderLine.F10430),
                         nameof(SalesOrderLine.Discount1), nameof(SalesOrderLine.Discount2),
                         nameof(SalesOrderLine.Discount3), nameof(SalesOrderLine.Discount4),
                         nameof(SalesOrderLine.Discount1S), nameof(SalesOrderLine.Discount1C),
                         nameof(SalesOrderLine.Discount2S), nameof(SalesOrderLine.Discount2C),
                         nameof(SalesOrderLine.Discount3S), nameof(SalesOrderLine.Discount3C),
                         nameof(SalesOrderLine.Cash2S), nameof(SalesOrderLine.Cash2C),
                         nameof(SalesOrderLine.Free1S), nameof(SalesOrderLine.Free1C),
                         nameof(SalesOrderLine.JobArea)
                     })
            {
                e.Property(money).HasPrecision(12, 2);
            }

            e.Property(x => x.FreeQCs1).HasPrecision(9, 3);
            e.Property(x => x.FreeQCs2).HasPrecision(9, 3);
        });
    }

    private static void ConfigureBmsOwnedHeader(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<SalesOrder> e)
    {
        e.Property(x => x.Depot).HasMaxLength(1);
        e.Property(x => x.TransTag).HasMaxLength(1);
        e.Property(x => x.Status).HasMaxLength(1);
        e.Property(x => x.CrAuthor).HasMaxLength(1);
        e.Property(x => x.StatDesc).HasMaxLength(15);
        e.Property(x => x.CauseFail).HasMaxLength(100);
        e.Property(x => x.PlateNo).HasMaxLength(10);
        e.Property(x => x.Trucker).HasMaxLength(35);
        e.Property(x => x.Driver).HasMaxLength(35);
        e.Property(x => x.VsRemarks).HasMaxLength(50);
        e.Property(x => x.OldCsMan).HasMaxLength(4);
        e.Property(x => x.DssNo).HasMaxLength(8);
        e.Property(x => x.CheckNo1).HasMaxLength(10);
        e.Property(x => x.ChainCode).HasMaxLength(4);
        e.Property(x => x.Reason).HasMaxLength(75);
        e.Property(x => x.SubdName).HasMaxLength(30);
        e.Property(x => x.JobberCKey).HasMaxLength(5);
        e.Property(x => x.Rem1).HasMaxLength(75);
        e.Property(x => x.Rem2).HasMaxLength(75);
        e.Property(x => x.Rem3).HasMaxLength(75);
        e.Property(x => x.VhType).HasMaxLength(3);
        e.Property(x => x.Vessel).HasMaxLength(15);
        e.Property(x => x.WaybillNo).HasMaxLength(15);
        e.Property(x => x.Voyage).HasMaxLength(15);
        e.Property(x => x.Van).HasMaxLength(15);
        e.Property(x => x.BlNo).HasMaxLength(15);
        e.Property(x => x.Elapsed).HasMaxLength(75);
        e.Property(x => x.DrpCust).HasMaxLength(7);

        foreach (var money in new[]
                 {
                     nameof(SalesOrder.InvAmt), nameof(SalesOrder.InvTax),
                     nameof(SalesOrder.Cewit), nameof(SalesOrder.DssAmt),
                     nameof(SalesOrder.FccosAmt), nameof(SalesOrder.Cl),
                     nameof(SalesOrder.CurSlAmt), nameof(SalesOrder.CurrentSo),
                     nameof(SalesOrder.CurrPay), nameof(SalesOrder.PendingSo),
                     nameof(SalesOrder.OrAmt1), nameof(SalesOrder.RefRgwAmt),
                     nameof(SalesOrder.Rate)
                 })
        {
            e.Property(money).HasPrecision(12, 2);
        }
    }
}
