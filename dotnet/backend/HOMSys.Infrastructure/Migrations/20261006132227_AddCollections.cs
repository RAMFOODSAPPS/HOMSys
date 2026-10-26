using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DepositBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SiteId = table.Column<int>(type: "int", nullable: false),
                    Bank = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DepositDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SlipRefNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MatchedBankLineId = table.Column<int>(type: "int", nullable: true),
                    MatchedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepositBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepositBatches_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceSyncRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SiteId = table.Column<int>(type: "int", nullable: false),
                    Wh = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceSyncRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceSyncRequests_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PostedInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WhseNo = table.Column<int>(type: "int", nullable: false),
                    DocNo = table.Column<int>(type: "int", nullable: false),
                    DocDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SiteId = table.Column<int>(type: "int", nullable: true),
                    CustKey = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    CKey = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CustName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Term = table.Column<int>(type: "int", nullable: false),
                    PoNum = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RfcTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SourceFile = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostedInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostedInvoices_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PostedRfcs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WhseNo = table.Column<int>(type: "int", nullable: false),
                    SourceFile = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InvoiceNo = table.Column<int>(type: "int", nullable: false),
                    Amt = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostedRfcs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserBranches",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SiteId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBranches", x => new { x.UserId, x.SiteId });
                    table.ForeignKey(
                        name: "FK_UserBranches_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserBranches_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Collections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteId = table.Column<int>(type: "int", nullable: false),
                    ReceiptType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ReceiptNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReceiptDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CustKey = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    CustName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PaymentMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UnappliedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Bank = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CheckNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CheckDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DepositBatchId = table.Column<int>(type: "int", nullable: true),
                    PaymentStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VerificationStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReturnReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PdcOutcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PdcOutcomeDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PdcOutcomeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PdcOutcomeBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostedToBmsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Collections_DepositBatches_DepositBatchId",
                        column: x => x.DepositBatchId,
                        principalTable: "DepositBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Collections_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceSyncRequestItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceSyncRequestId = table.Column<int>(type: "int", nullable: false),
                    InvoiceNo = table.Column<int>(type: "int", nullable: false),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Found = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceSyncRequestItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceSyncRequestItems_InvoiceSyncRequests_InvoiceSyncRequestId",
                        column: x => x.InvoiceSyncRequestId,
                        principalTable: "InvoiceSyncRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollectionAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionId = table.Column<int>(type: "int", nullable: false),
                    BlobPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionAttachments_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollectionDeductions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionId = table.Column<int>(type: "int", nullable: false),
                    PostedInvoiceId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionDeductions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionDeductions_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollectionDeductions_PostedInvoices_PostedInvoiceId",
                        column: x => x.PostedInvoiceId,
                        principalTable: "PostedInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectionHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    By = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionHistories_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollectionInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionId = table.Column<int>(type: "int", nullable: false),
                    PostedInvoiceId = table.Column<int>(type: "int", nullable: false),
                    AmountApplied = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionInvoices_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollectionInvoices_PostedInvoices_PostedInvoiceId",
                        column: x => x.PostedInvoiceId,
                        principalTable: "PostedInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Description", "Key", "Name" },
                values: new object[,]
                {
                    { 21, "Encode collections for assigned branches and submit them for verification", "collections-encode", "Collection Encoding" },
                    { 22, "Verify or return branch-encoded collections against the attached receipt and bank statement", "collections-verify", "Collection Verification" },
                    { 23, "Upload bank statements, void collections, and see the overall collection status dashboard", "collections-manage", "Collection Management" },
                    { 24, "View the post-dated check maturity report", "pdc-view", "PDC Monitoring" },
                    { 25, "Tag deposited post-dated checks as Succeeded or Bounced", "pdc-tag", "PDC Outcome Tagging" }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 21, 1 },
                    { 22, 1 },
                    { 23, 1 },
                    { 24, 1 },
                    { 25, 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionAttachments_CollectionId",
                table: "CollectionAttachments",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionDeductions_CollectionId",
                table: "CollectionDeductions",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionDeductions_PostedInvoiceId",
                table: "CollectionDeductions",
                column: "PostedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionHistories_CollectionId",
                table: "CollectionHistories",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionInvoices_CollectionId_PostedInvoiceId",
                table: "CollectionInvoices",
                columns: new[] { "CollectionId", "PostedInvoiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionInvoices_PostedInvoiceId",
                table: "CollectionInvoices",
                column: "PostedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_CheckDate",
                table: "Collections",
                column: "CheckDate");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_CollectionKey",
                table: "Collections",
                column: "CollectionKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Collections_DepositBatchId",
                table: "Collections",
                column: "DepositBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_SiteId_ReceiptType_ReceiptNo",
                table: "Collections",
                columns: new[] { "SiteId", "ReceiptType", "ReceiptNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Collections_SiteId_VerificationStatus",
                table: "Collections",
                columns: new[] { "SiteId", "VerificationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_DepositBatches_SiteId_Bank_SlipRefNo",
                table: "DepositBatches",
                columns: new[] { "SiteId", "Bank", "SlipRefNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSyncRequestItems_InvoiceSyncRequestId",
                table: "InvoiceSyncRequestItems",
                column: "InvoiceSyncRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSyncRequests_SiteId",
                table: "InvoiceSyncRequests",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSyncRequests_Status",
                table: "InvoiceSyncRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PostedInvoices_SiteId_CustKey",
                table: "PostedInvoices",
                columns: new[] { "SiteId", "CustKey" });

            migrationBuilder.CreateIndex(
                name: "IX_PostedInvoices_WhseNo_DocNo",
                table: "PostedInvoices",
                columns: new[] { "WhseNo", "DocNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostedRfcs_WhseNo_InvoiceNo",
                table: "PostedRfcs",
                columns: new[] { "WhseNo", "InvoiceNo" });

            migrationBuilder.CreateIndex(
                name: "IX_PostedRfcs_WhseNo_SourceFile_InvoiceNo",
                table: "PostedRfcs",
                columns: new[] { "WhseNo", "SourceFile", "InvoiceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserBranches_SiteId",
                table: "UserBranches",
                column: "SiteId");

            // Collection roles (created here like the Offshore Encoder role; idempotent, admins can adjust grants later).
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'Invoice Custodian')
                    INSERT INTO Roles (Name, Description, CreatedAt, CreatedBy)
                    VALUES ('Invoice Custodian', 'Encodes collections (CR / PR / AR) for the branches assigned to them and submits them for verification', GETUTCDATE(), 'system');
                IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'Finance Verifier')
                    INSERT INTO Roles (Name, Description, CreatedAt, CreatedBy)
                    VALUES ('Finance Verifier', 'Verifies or returns branch-encoded collections against the attached receipt and the bank statement; monitors PDCs', GETUTCDATE(), 'system');
                IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'Finance Manager')
                    INSERT INTO Roles (Name, Description, CreatedAt, CreatedBy)
                    VALUES ('Finance Manager', 'Uploads daily bank statements, voids collections, tags PDC outcomes and sees the overall collection dashboard', GETUTCDATE(), 'system');

                INSERT INTO RolePermissions (RoleId, PermissionId)
                SELECT r.Id, p.Id FROM Roles r
                JOIN (VALUES ('Invoice Custodian', 21), ('Invoice Custodian', 24),
                             ('Finance Verifier', 22), ('Finance Verifier', 24), ('Finance Verifier', 25),
                             ('Finance Manager', 23), ('Finance Manager', 24), ('Finance Manager', 25), ('Finance Manager', 13)) g(RoleName, PermId) ON g.RoleName = r.Name
                JOIN Permissions p ON p.Id = g.PermId
                WHERE NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = r.Id AND x.PermissionId = p.Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The roles are kept (they may have users); only their collection grants go with the permissions.
            migrationBuilder.Sql("DELETE FROM RolePermissions WHERE PermissionId IN (21, 22, 23, 24, 25) AND RoleId <> 1");

            migrationBuilder.DropTable(
                name: "CollectionAttachments");

            migrationBuilder.DropTable(
                name: "CollectionDeductions");

            migrationBuilder.DropTable(
                name: "CollectionHistories");

            migrationBuilder.DropTable(
                name: "CollectionInvoices");

            migrationBuilder.DropTable(
                name: "InvoiceSyncRequestItems");

            migrationBuilder.DropTable(
                name: "PostedRfcs");

            migrationBuilder.DropTable(
                name: "UserBranches");

            migrationBuilder.DropTable(
                name: "Collections");

            migrationBuilder.DropTable(
                name: "PostedInvoices");

            migrationBuilder.DropTable(
                name: "InvoiceSyncRequests");

            migrationBuilder.DropTable(
                name: "DepositBatches");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 21, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 22, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 23, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 24, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 25, 1 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 25);
        }
    }
}
