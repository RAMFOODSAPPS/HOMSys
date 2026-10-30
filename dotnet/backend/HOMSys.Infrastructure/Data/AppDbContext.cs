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

    // Customer master, live-synced from F:\AUTOPROG\CUSTOMER\{branch}\cust4win.dbf
    // (PricingDataImporter / LegacyMasterWatcher) — one row per (Branch, RecNo).
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerClassification> CustomerClassifications => Set<CustomerClassification>();
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

    // Zone code -> description only, for labeling the Pricelist by Zone
    // report's zone picker. See PricingDataImporter.
    public DbSet<ZoneMast> ZoneMasts => Set<ZoneMast>();

    // Free-text customer identifier -> CustKey, learned from the "Import by
    // Customer Name" SO import mapping dialog.
    public DbSet<CustomerIdentifierMap> CustomerIdentifierMaps => Set<CustomerIdentifierMap>();

    // Sales order encoding
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderLine> SalesOrderLines => Set<SalesOrderLine>();

    // Bridge-synced oowkdet snapshot for the OOS report — see OosSyncLine.
    public DbSet<OosSyncLine> OosSyncLines => Set<OosSyncLine>();
    public DbSet<SalesOrderRfc> SalesOrderRfcs => Set<SalesOrderRfc>();
    public DbSet<SalesOrderRfcLine> SalesOrderRfcLines => Set<SalesOrderRfcLine>();

    // Offshore hand-off payload (origin BMS rows replayed into ForBranch's BMS).
    public DbSet<OffshoreTransfer> OffshoreTransfers => Set<OffshoreTransfer>();

    // Collections: branch-encoded payments applied to EOD-mirrored posted invoices
    // (SL files), deposit batches, finance verification + PDC tracking.
    public DbSet<PostedInvoice> PostedInvoices => Set<PostedInvoice>();
    public DbSet<PostedRfc> PostedRfcs => Set<PostedRfc>();
    public DbSet<InvoiceSyncRequest> InvoiceSyncRequests => Set<InvoiceSyncRequest>();
    public DbSet<InvoiceSyncRequestItem> InvoiceSyncRequestItems => Set<InvoiceSyncRequestItem>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<CollectionInvoice> CollectionInvoices => Set<CollectionInvoice>();
    public DbSet<CollectionDeduction> CollectionDeductions => Set<CollectionDeduction>();
    public DbSet<CollectionAttachment> CollectionAttachments => Set<CollectionAttachment>();
    public DbSet<CollectionHistory> CollectionHistories => Set<CollectionHistory>();
    // Remittance Advice (NKA): one payment holding 1+ CRs filled from the chain's RA Excel.
    public DbSet<Remittance> Remittances => Set<Remittance>();
    public DbSet<RemittanceLine> RemittanceLines => Set<RemittanceLine>();
    public DbSet<RemittanceHistory> RemittanceHistories => Set<RemittanceHistory>();
    public DbSet<DepositBatch> DepositBatches => Set<DepositBatch>();
    public DbSet<HomsPosting> HomsPostings => Set<HomsPosting>();
    public DbSet<UserBranch> UserBranches => Set<UserBranch>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<BankAccountSite> BankAccountSites => Set<BankAccountSite>();
    public DbSet<BankStatementUpload> BankStatementUploads => Set<BankStatementUpload>();
    public DbSet<BankStatementLine> BankStatementLines => Set<BankStatementLine>();
    public DbSet<BmsPostedCollection> BmsPostedCollections => Set<BmsPostedCollection>();
    public DbSet<BmsPostedCollectionLine> BmsPostedCollectionLines => Set<BmsPostedCollectionLine>();

    // Per-section "last synced" timestamp for the Legacy Monitoring page,
    // updated by every path that actually applies a sync — see SyncLogExtensions.
    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();

    // Data Analytics saved reports + dashboards (dashboards embed widget specs).
    public DbSet<SavedReport> SavedReports => Set<SavedReport>();

    // TASAF (Trade Activity Spend Approval Form) master lists — see TasafModel.
    public DbSet<TasafSalesGroup> TasafSalesGroups => Set<TasafSalesGroup>();
    public DbSet<TasafChain> TasafChains => Set<TasafChain>();
    public DbSet<TasafArea> TasafAreas => Set<TasafArea>();
    public DbSet<TasafProgramType> TasafProgramTypes => Set<TasafProgramType>();
    public DbSet<TasafObjective> TasafObjectives => Set<TasafObjective>();
    public DbSet<TasafTargetMarket> TasafTargetMarkets => Set<TasafTargetMarket>();
    public DbSet<TasafChannel> TasafChannels => Set<TasafChannel>();
    public DbSet<TasafProductGroup> TasafProductGroups => Set<TasafProductGroup>();
    public DbSet<TasafCostType> TasafCostTypes => Set<TasafCostType>();
    public DbSet<TasafApprovalRole> TasafApprovalRoles => Set<TasafApprovalRole>();
    public DbSet<TasafApprover> TasafApprovers => Set<TasafApprover>();
    public DbSet<TasafSalesGroupUser> TasafSalesGroupUsers => Set<TasafSalesGroupUser>();
    public DbSet<TasafChainCustomer> TasafChainCustomers => Set<TasafChainCustomer>();
    public DbSet<TasafApprovalType> TasafApprovalTypes => Set<TasafApprovalType>();
    public DbSet<TasafApprovalRule> TasafApprovalRules => Set<TasafApprovalRule>();
    public DbSet<TasafApprovalStep> TasafApprovalSteps => Set<TasafApprovalStep>();
    public DbSet<Tasaf> Tasafs => Set<Tasaf>();
    public DbSet<TasafNumbering> TasafNumberings => Set<TasafNumbering>();
    public DbSet<TasafBranch> TasafBranches => Set<TasafBranch>();
    public DbSet<TasafCustomer> TasafCustomers => Set<TasafCustomer>();
    public DbSet<TasafProduct> TasafProducts => Set<TasafProduct>();
    public DbSet<TasafCost> TasafCosts => Set<TasafCost>();
    public DbSet<TasafAction> TasafActions => Set<TasafAction>();

    // In-app notifications (topbar bell) — see NotificationService.
    public DbSet<Notification> Notifications => Set<Notification>();

    // Form Designer layouts (admin-arranged form fields) — see FormLayoutService.
    public DbSet<FormLayout> FormLayouts => Set<FormLayout>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        TasafModel.Configure(modelBuilder);

        modelBuilder.Entity<FormLayout>(e =>
        {
            e.HasIndex(x => x.FormKey).IsUnique();
            e.Property(x => x.FormKey).HasMaxLength(50);
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
        });

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

        // Not unique: several logins may share one email. Username stays unique (the login).
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email);

        modelBuilder.Entity<Role>()
            .HasIndex(r => r.Name).IsUnique();

        modelBuilder.Entity<Permission>()
            .HasIndex(p => p.Key).IsUnique();

        modelBuilder.Entity<Company>()
            .HasIndex(c => c.Name).IsUnique();

        modelBuilder.Entity<Company>()
            .HasIndex(c => c.Code).IsUnique()
            .HasFilter("[Code] != ''");

        modelBuilder.Entity<Company>(e =>
        {
            e.Property(c => c.LogoBlob).HasMaxLength(200);
            e.Property(c => c.LogoContentType).HasMaxLength(50);
        });

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

        modelBuilder.Entity<SyncLog>()
            .HasIndex(s => s.Section).IsUnique();

        modelBuilder.Entity<SyncLog>()
            .Property(s => s.Section).HasMaxLength(50).IsRequired();

        modelBuilder.Entity<SavedReport>(e =>
        {
            e.HasIndex(x => x.OwnerUserId);
            e.Property(x => x.Kind).HasMaxLength(10).IsRequired();
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.DatasetKey).HasMaxLength(50);
            e.Property(x => x.Visual).HasMaxLength(20);
            e.Property(x => x.DefinitionJson).HasColumnType("nvarchar(max)").IsRequired();
            e.Property(x => x.SharedRoleIds).HasMaxLength(200);
            e.Property(x => x.CreatedBy).HasMaxLength(100);
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
        });

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
            new Permission { Id = 11, Key = "legacy-monitoring", Name = "Legacy Monitoring",      Description = "View legacy DBF sync status and trigger manual syncs" },
            new Permission { Id = 12, Key = "pricelist-zone-export", Name = "Pricelist by Zone",  Description = "Generate branch pricelist Excel exports by zone" },
            new Permission { Id = 13, Key = "data-analytics", Name = "Data Analytics",           Description = "Build, view and share analytics reports and dashboards" },
            new Permission { Id = 14, Key = "data-analytics-admin", Name = "Data Analytics Admin", Description = "Publish system analytics templates and manage all shared analytics" },
            new Permission { Id = 15, Key = "offshore-encode", Name = "Offshore Encoding", Description = "Encode sales orders For Branch — processed at this branch's BMS, then handed off to the selected branch" },
            new Permission { Id = 16, Key = "tasaf-masters", Name = "TASAF Setup", Description = "Maintain TASAF master lists, GKAM reviewers and the approval matrix" },
            new Permission { Id = 17, Key = "tasaf", Name = "TASAF", Description = "Create and submit own Trade Activity Spend Approval Forms" },
            new Permission { Id = 18, Key = "tasaf-view-all", Name = "TASAF View All", Description = "View every TASAF, not only your own (also needs TASAF)" },
            new Permission { Id = 19, Key = "products", Name = "Products", Description = "View the product master (read-only, synced from BMS)" },
            new Permission { Id = 20, Key = "customers", Name = "Customers", Description = "View the customer master (read-only, synced from each branch's BMS)" },
            new Permission { Id = 21, Key = "collections-encode", Name = "Collection Encoding", Description = "Encode collections for assigned branches and submit them for verification" },
            new Permission { Id = 22, Key = "collections-verify", Name = "Collection Verification", Description = "Verify or return branch-encoded collections against the attached receipt and bank statement" },
            new Permission { Id = 23, Key = "collections-manage", Name = "Collection Management", Description = "Upload bank statements, void collections, and see the overall collection status dashboard" },
            new Permission { Id = 24, Key = "pdc-view", Name = "PDC Monitoring", Description = "View the post-dated check maturity report" },
            new Permission { Id = 25, Key = "pdc-tag", Name = "PDC Outcome Tagging", Description = "Tag deposited post-dated checks as Succeeded or Bounced" },
            new Permission { Id = 26, Key = "pdc-confirm", Name = "Matured PDC Confirmation", Description = "Confirm a matured PDC's deposit against the bank statement (CNC Verifier) — posts its JV to HOMS" },
            new Permission { Id = 27, Key = "collections-ra-encode", Name = "Remittance Advice Encoding", Description = "Custodian Verifier: encode CRs and Remittance Advices (NKA) for every branch, uploading the chain's RA Excel" }
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
            new RolePermission { RoleId = 1, PermissionId = 11 },
            new RolePermission { RoleId = 1, PermissionId = 12 },
            new RolePermission { RoleId = 1, PermissionId = 13 },
            new RolePermission { RoleId = 1, PermissionId = 14 },
            new RolePermission { RoleId = 1, PermissionId = 15 },
            new RolePermission { RoleId = 1, PermissionId = 16 },
            new RolePermission { RoleId = 1, PermissionId = 17 },
            new RolePermission { RoleId = 1, PermissionId = 18 },
            new RolePermission { RoleId = 1, PermissionId = 19 },
            new RolePermission { RoleId = 1, PermissionId = 20 },
            new RolePermission { RoleId = 1, PermissionId = 21 },
            new RolePermission { RoleId = 1, PermissionId = 22 },
            new RolePermission { RoleId = 1, PermissionId = 23 },
            new RolePermission { RoleId = 1, PermissionId = 24 },
            new RolePermission { RoleId = 1, PermissionId = 25 },
            new RolePermission { RoleId = 1, PermissionId = 26 },
            new RolePermission { RoleId = 1, PermissionId = 27 }
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
        // ── CustomerClassification (HOMSys-owned, one row per CustKey) ─────────
        b.Entity<CustomerClassification>(e =>
        {
            e.HasIndex(x => x.CustKey).IsUnique();
            e.Property(x => x.CustKey).HasMaxLength(7);
            e.Property(x => x.Region).HasMaxLength(30);
            e.Property(x => x.SalesGroup).HasMaxLength(10);
            e.Property(x => x.ServicingBranch).HasMaxLength(40);
            e.Property(x => x.ChainGroup).HasMaxLength(50);
            e.Property(x => x.Source).HasMaxLength(100);
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
        });

        // ── Customer (from F:\AUTOPROG\CUSTOMER\{branch}\cust4win.DBF) ───────
        b.Entity<Customer>(e =>
        {
            // Not unique: CustKey lives in exactly one branch's file in practice,
            // but the identity key is (Branch, RecNo) like every other per-branch
            // pricing table, not CustKey alone.
            e.HasIndex(x => x.CustKey);
            e.HasIndex(x => x.CusName);
            e.HasIndex(x => x.Branch);
            e.HasIndex(x => new { x.Branch, x.RecNo });
            e.Property(x => x.Branch).HasMaxLength(10).IsRequired();
            e.Property(x => x.CustKey).HasMaxLength(7).IsRequired();
            e.Property(x => x.CKey).HasMaxLength(5);
            e.Property(x => x.CusName).HasMaxLength(50);
            e.Property(x => x.CusName2).HasMaxLength(50);
            e.Property(x => x.AddrLn1).HasMaxLength(75);
            e.Property(x => x.AddrLn2).HasMaxLength(75);
            e.Property(x => x.DelAddrLn1).HasMaxLength(75);
            e.Property(x => x.DelAddrLn2).HasMaxLength(75);
            e.Property(x => x.DelArea).HasMaxLength(75);
            e.Property(x => x.CsMan).HasMaxLength(4);
            e.Property(x => x.CZone).HasMaxLength(4).IsRequired();
            e.Property(x => x.VatId).HasMaxLength(1);
            e.Property(x => x.Subd).HasMaxLength(20);
            e.Property(x => x.Tin).HasMaxLength(20);
            e.Property(x => x.AliasKey).HasMaxLength(7);
            e.Property(x => x.ConsoMax2).HasMaxLength(7);
            e.Property(x => x.NRegion).HasMaxLength(20);
            e.Property(x => x.NSlGrp).HasMaxLength(5);
            e.Property(x => x.NAccount).HasMaxLength(50);
            e.Property(x => x.Territory).HasMaxLength(10);
            e.Property(x => x.NSlGrp1).HasMaxLength(15);
            e.Property(x => x.WithholdingUpdatedBy).HasMaxLength(100);
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

        // ── ZoneMast (from addon\{branch}\zonemast.DBF) ──────────────────────
        b.Entity<ZoneMast>(e =>
        {
            e.HasIndex(x => new { x.Branch, x.CZone });
            e.Property(x => x.Branch).HasMaxLength(10).IsRequired();
            e.Property(x => x.CZone).HasMaxLength(4).IsRequired();
            e.Property(x => x.CDesc).HasMaxLength(20);
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

            e.Property(x => x.OriginBranch).HasMaxLength(20);
            e.Property(x => x.ForBranch).HasMaxLength(20);
            e.Property(x => x.OffshoreError).HasMaxLength(500);
            e.Property(x => x.BridgeClaimedBy).HasMaxLength(100);
            e.HasOne(x => x.OffshoreTransfer)
                .WithOne(t => t.SalesOrder)
                .HasForeignKey<OffshoreTransfer>(t => t.SoId)
                .OnDelete(DeleteBehavior.Cascade);

            ConfigureBmsOwnedHeader(e);
        });

        b.Entity<OffshoreTransfer>(e =>
        {
            e.HasKey(x => x.SoId);
            e.Property(x => x.HeaderJson).HasColumnType("nvarchar(max)").IsRequired();
            e.Property(x => x.LinesJson).HasColumnType("nvarchar(max)").IsRequired();
            e.Property(x => x.DiscountsJson).HasColumnType("nvarchar(max)").IsRequired();
        });

        // ── Collections ──────────────────────────────────────────────────────
        b.Entity<PostedInvoice>(e =>
        {
            e.HasIndex(x => new { x.WhseNo, x.DocNo }).IsUnique();
            e.HasIndex(x => new { x.SiteId, x.CustKey });
            e.Property(x => x.CustKey).HasMaxLength(7).IsRequired();
            e.Property(x => x.CKey).HasMaxLength(5).IsRequired();
            e.Property(x => x.CustName).HasMaxLength(50);
            e.Property(x => x.PoNum).HasMaxLength(15);
            e.Property(x => x.SourceFile).HasMaxLength(100);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.Property(x => x.RfcTotal).HasPrecision(18, 2);
            e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PostedRfc>(e =>
        {
            e.HasIndex(x => new { x.WhseNo, x.SourceFile, x.InvoiceNo, x.RfcNo }).IsUnique();
            e.HasIndex(x => new { x.WhseNo, x.InvoiceNo });
            e.Property(x => x.SourceFile).HasMaxLength(100).IsRequired();
            e.Property(x => x.Amt).HasPrecision(18, 2);
        });

        b.Entity<InvoiceSyncRequest>(e =>
        {
            e.HasIndex(x => x.Status);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RequestedBy).HasMaxLength(100);
            e.Property(x => x.Message).HasMaxLength(500);
            e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Items).WithOne(i => i.Request).HasForeignKey(i => i.InvoiceSyncRequestId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<HomsPosting>(e =>
        {
            e.HasIndex(x => new { x.Status, x.Id });
            e.HasIndex(x => new { x.CollectionId, x.Kind });
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(12);
            e.Property(x => x.Table).HasMaxLength(4);
            e.Property(x => x.MonthKey).HasMaxLength(4);
            e.Property(x => x.File).HasMaxLength(40);
            e.Property(x => x.Message).HasMaxLength(500);
            e.Property(x => x.CreatedBy).HasMaxLength(100);
            e.HasOne(x => x.Collection).WithMany(c => c.HomsPostings).HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<DepositBatch>(e =>
        {
            // A slip no. groups the collections deposited together; it's optional (user 2026-10-15), so it's only
            // unique when there is one — a deposit without a reference is its own slip.
            e.HasIndex(x => new { x.SiteId, x.Bank, x.SlipRefNo }).IsUnique().HasFilter("[SlipRefNo] <> ''");
            e.HasIndex(x => new { x.BankAccountId, x.DepositDate });
            e.Property(x => x.Bank).HasMaxLength(50).IsRequired();
            e.Property(x => x.SlipRefNo).HasMaxLength(30).IsRequired();
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.CreatedBy).HasMaxLength(100);
            e.Property(x => x.MatchConfirmedBy).HasMaxLength(100);
            e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.BankAccount).WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<BmsPostedCollection>(e =>
        {
            e.HasIndex(x => new { x.SourceFile, x.DocId, x.DocNo }).IsUnique();
            e.HasIndex(x => x.DocNo);
            e.HasIndex(x => x.CustKey);
            e.HasIndex(x => x.CollectionId);
            e.Property(x => x.SourceFile).HasMaxLength(12).IsRequired();
            e.Property(x => x.UserName).HasMaxLength(20);
            e.Property(x => x.Payor).HasMaxLength(30);
            e.Property(x => x.StatDesc).HasMaxLength(15);
            e.Property(x => x.CustKey).HasMaxLength(7);
            e.Property(x => x.ArApplied).HasPrecision(18, 2);
            e.Property(x => x.Ewt).HasPrecision(18, 2);
            e.Property(x => x.Received).HasPrecision(18, 2);
            e.Property(x => x.CheckNo).HasMaxLength(16);
            e.Property(x => x.DraweeBank).HasMaxLength(20);
            e.Property(x => x.DepBank).HasMaxLength(16);
            e.Property(x => x.LinkMethod).HasMaxLength(20);
            e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Collection).WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Lines).WithOne(l => l.BmsPostedCollection).HasForeignKey(l => l.BmsPostedCollectionId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<BmsPostedCollectionLine>(e =>
        {
            e.HasIndex(x => new { x.RefNo, x.AcctNo });
            e.Property(x => x.CustKey).HasMaxLength(7);
            e.Property(x => x.CKey).HasMaxLength(7);
            e.Property(x => x.DrAmt).HasPrecision(18, 2);
            e.Property(x => x.CrAmt).HasPrecision(18, 2);
            e.Property(x => x.DraweeBank).HasMaxLength(20);
            e.Property(x => x.CheckNo).HasMaxLength(16);
            e.Property(x => x.DepBank).HasMaxLength(16);
            e.Property(x => x.PaymentId).HasMaxLength(20);
            e.Property(x => x.RecBank).HasMaxLength(50);
            e.Property(x => x.Reference).HasMaxLength(40);
        });

        b.Entity<BankAccount>(e =>
        {
            e.HasIndex(x => new { x.BankCode, x.AccountNo }).IsUnique();
            e.Property(x => x.BankCode).HasMaxLength(10).IsRequired();
            e.Property(x => x.AccountNo).HasMaxLength(30).IsRequired();
            e.Property(x => x.AccountName).HasMaxLength(100);
            e.Property(x => x.CreatedBy).HasMaxLength(100);
            e.HasIndex(x => x.HomsAcctNo).IsUnique().HasFilter("[HomsAcctNo] IS NOT NULL");
            e.Property(x => x.BankAbbr).HasMaxLength(20);
            e.Property(x => x.AltAccountNo).HasMaxLength(30);
            e.Property(x => x.HomsBankAc).HasMaxLength(30);
            e.Property(x => x.DeletedBy).HasMaxLength(100);
            e.Ignore(x => x.Label);
        });

        b.Entity<BankAccountSite>(e =>
        {
            e.HasKey(x => new { x.BankAccountId, x.SiteId });
            e.HasOne(x => x.BankAccount).WithMany(a => a.Sites).HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<BankStatementUpload>(e =>
        {
            e.HasIndex(x => new { x.BankAccountId, x.FileHash });
            e.Property(x => x.FileName).HasMaxLength(200).IsRequired();
            e.Property(x => x.FileHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.BlobPath).HasMaxLength(300);
            e.Property(x => x.UploadedBy).HasMaxLength(100);
            e.HasOne(x => x.BankAccount).WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<BankStatementLine>(e =>
        {
            e.HasIndex(x => new { x.BankAccountId, x.Fingerprint }).IsUnique();
            e.HasIndex(x => new { x.BankAccountId, x.TxnDate });
            e.HasIndex(x => x.MatchedDepositBatchId);
            e.Property(x => x.TxnTime).HasMaxLength(20);
            e.Property(x => x.Description).HasMaxLength(300);
            e.Property(x => x.Reference).HasMaxLength(200);
            e.Property(x => x.BankBranch).HasMaxLength(100);
            e.Property(x => x.CheckNo).HasMaxLength(30);
            e.Property(x => x.Debit).HasPrecision(18, 2);
            e.Property(x => x.Credit).HasPrecision(18, 2);
            e.Property(x => x.Balance).HasPrecision(18, 2);
            e.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
            e.Property(x => x.MatchMethod).HasMaxLength(20);
            e.Property(x => x.MatchedBy).HasMaxLength(100);
            e.HasOne(x => x.BankAccount).WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Upload).WithMany().HasForeignKey(x => x.UploadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.MatchedDepositBatch).WithMany().HasForeignKey(x => x.MatchedDepositBatchId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Collection>(e =>
        {
            e.HasIndex(x => x.CollectionKey).IsUnique();
            // One receipt number per branch and receipt type; voided rows keep their number.
            e.HasIndex(x => new { x.SiteId, x.ReceiptType, x.ReceiptNo }).IsUnique();
            e.HasIndex(x => new { x.SiteId, x.VerificationStatus });
            e.HasIndex(x => x.CheckDate);
            e.Property(x => x.ReceiptType).HasConversion<string>().HasMaxLength(2);
            e.Property(x => x.PaymentMode).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PdcOutcome).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ReceiptNo).HasMaxLength(20).IsRequired();
            e.Property(x => x.CustKey).HasMaxLength(7).IsRequired();
            e.Property(x => x.CustName).HasMaxLength(50);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.UnappliedAmount).HasPrecision(18, 2);
            e.Property(x => x.Bank).HasMaxLength(50);
            e.Property(x => x.CheckNo).HasMaxLength(20);
            e.Property(x => x.ReturnReason).HasMaxLength(500);
            e.Property(x => x.VoidReason).HasMaxLength(500);
            e.Property(x => x.Remarks).HasMaxLength(500);
            e.Property(x => x.PdcOutcomeReason).HasMaxLength(500);
            e.Property(x => x.PdcOutcomeBy).HasMaxLength(100);
            e.Property(x => x.CreatedBy).HasMaxLength(100);
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
            e.Property(x => x.VerifiedBy).HasMaxLength(100);
            e.Property(x => x.LodIssuedBy).HasMaxLength(100);
            e.Property(x => x.LodReceivedBy).HasMaxLength(100);
            e.Property(x => x.SettledBy).HasMaxLength(100);
            e.Property(x => x.LegalEndorsedBy).HasMaxLength(100);
            e.Property(x => x.LegalEndorsedTo).HasMaxLength(50);
            e.Property(x => x.LegalRemarks).HasMaxLength(500);
            e.Property(x => x.PdcPullout).HasMaxLength(20);
            e.Property(x => x.PdcPulloutReason).HasMaxLength(500);
            e.Property(x => x.PdcPulloutRequestedBy).HasMaxLength(100);
            e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.DepositBatch).WithMany(d => d.Collections).HasForeignKey(x => x.DepositBatchId).OnDelete(DeleteBehavior.SetNull);
            // One replacement CR settles at most one bounced PDC.
            e.HasOne(x => x.SettledByCollection).WithMany().HasForeignKey(x => x.SettledByCollectionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.SettledByCollectionId).IsUnique().HasFilter("[SettledByCollectionId] IS NOT NULL");
            e.HasOne(x => x.Remittance).WithMany(r => r.Collections).HasForeignKey(x => x.RemittanceId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.RemittanceId);
        });

        b.Entity<Remittance>(e =>
        {
            e.HasIndex(x => x.VerificationStatus);
            e.Property(x => x.ChainName).HasMaxLength(50).IsRequired();
            e.Property(x => x.PaymentMode).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Bank).HasMaxLength(50);
            e.Property(x => x.CheckNo).HasMaxLength(20);
            e.Property(x => x.CheckAmount).HasPrecision(18, 2);
            e.Property(x => x.ReturnReason).HasMaxLength(500);
            e.Property(x => x.VoidReason).HasMaxLength(500);
            e.Property(x => x.Remarks).HasMaxLength(500);
            e.Property(x => x.CreatedBy).HasMaxLength(100);
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
            e.Property(x => x.VerifiedBy).HasMaxLength(100);
            e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.DepositBatch).WithMany().HasForeignKey(x => x.DepositBatchId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<RemittanceLine>(e =>
        {
            e.HasIndex(x => x.CollectionId);
            e.HasIndex(x => x.PostedInvoiceId);
            e.Property(x => x.CustKey).HasMaxLength(10);
            e.Property(x => x.BirSeries).HasMaxLength(40);
            foreach (var m in new[] { nameof(RemittanceLine.Gross), nameof(RemittanceLine.Cewt), nameof(RemittanceLine.RfcBoRtv),
                         nameof(RemittanceLine.Other), nameof(RemittanceLine.Net), nameof(RemittanceLine.SystemTotal), nameof(RemittanceLine.SystemRfc) })
                e.Property(m).HasPrecision(18, 2);
            e.Property(x => x.DeductionsJson).HasMaxLength(4000);
            e.Property(x => x.Flags).HasMaxLength(200);
            e.HasOne(x => x.Collection).WithMany(c => c.RemittanceLines).HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.PostedInvoice).WithMany().HasForeignKey(x => x.PostedInvoiceId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<RemittanceHistory>(e =>
        {
            e.HasIndex(x => x.RemittanceId);
            e.Property(x => x.Action).HasMaxLength(30).IsRequired();
            e.Property(x => x.FromStatus).HasMaxLength(30);
            e.Property(x => x.ToStatus).HasMaxLength(30);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.By).HasMaxLength(100);
            e.HasOne(x => x.Remittance).WithMany(r => r.History).HasForeignKey(x => x.RemittanceId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CollectionInvoice>(e =>
        {
            e.HasIndex(x => new { x.CollectionId, x.PostedInvoiceId }).IsUnique();
            e.HasIndex(x => x.PostedInvoiceId);
            e.Property(x => x.AmountApplied).HasPrecision(18, 2);
            e.HasOne(x => x.Collection).WithMany(c => c.Invoices).HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.PostedInvoice).WithMany(i => i.Allocations).HasForeignKey(x => x.PostedInvoiceId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CollectionDeduction>(e =>
        {
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Reference).HasMaxLength(100);
            e.HasOne(x => x.Collection).WithMany(c => c.Deductions).HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.PostedInvoice).WithMany().HasForeignKey(x => x.PostedInvoiceId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CollectionAttachment>(e =>
        {
            e.Property(x => x.BlobPath).HasMaxLength(300).IsRequired();
            e.Property(x => x.FileName).HasMaxLength(200).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.UploadedBy).HasMaxLength(100);
            e.HasOne(x => x.Collection).WithMany(c => c.Attachments).HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CollectionHistory>(e =>
        {
            e.HasIndex(x => x.CollectionId);
            e.Property(x => x.Action).HasMaxLength(30).IsRequired();
            e.Property(x => x.FromStatus).HasMaxLength(30);
            e.Property(x => x.ToStatus).HasMaxLength(30);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.By).HasMaxLength(100);
            e.HasOne(x => x.Collection).WithMany(c => c.History).HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UserBranch>(e =>
        {
            e.HasKey(x => new { x.UserId, x.SiteId });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── OosSyncLine (bridge-synced oowkdet snapshot) ─────────────────────
        // ── SalesOrderRfc / SalesOrderRfcLine (posted BMS RFCs, bridge-synced) ──
        b.Entity<SalesOrderRfc>(e =>
        {
            e.HasOne(x => x.SalesOrder).WithMany(o => o.Rfcs).HasForeignKey(x => x.SoId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.SoId, x.RfcNo }).IsUnique();
            e.HasIndex(x => x.InvNo);
            e.Property(x => x.UserName).HasMaxLength(20);
            e.Property(x => x.Remarks).HasMaxLength(30);
            e.Property(x => x.Remarks2).HasMaxLength(200);
            e.HasMany(x => x.Lines).WithOne(l => l.Rfc).HasForeignKey(l => l.RfcId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<SalesOrderRfcLine>(e =>
        {
            e.Property(x => x.CProdNo).HasMaxLength(4).IsRequired();
            e.Property(x => x.RetCode).HasMaxLength(2);
            e.Property(x => x.RsNo).HasMaxLength(8);
            e.Property(x => x.Remarks).HasMaxLength(50);
            e.Property(x => x.SpAmt).HasPrecision(12, 2);
            e.Property(x => x.Amt).HasPrecision(12, 2);
            e.Property(x => x.Tax).HasPrecision(12, 2);
            e.Property(x => x.DiscAmt1).HasPrecision(12, 2);
            e.Property(x => x.DiscAmt2).HasPrecision(12, 2);
        });

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
                         nameof(SalesOrderLine.JobArea), nameof(SalesOrderLine.ReceivedAmt)
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
        e.Property(x => x.InvCancelRemarks).HasMaxLength(50);
        e.Property(x => x.InvCancelledBy).HasMaxLength(20);

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
