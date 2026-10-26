using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRemittanceAdvice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DcrNo",
                table: "Collections",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomsDeptCode",
                table: "Collections",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemittanceId",
                table: "Collections",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reference",
                table: "CollectionDeductions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "RemittanceLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionId = table.Column<int>(type: "int", nullable: false),
                    RowNo = table.Column<int>(type: "int", nullable: false),
                    DocNo = table.Column<int>(type: "int", nullable: false),
                    WhseNo = table.Column<int>(type: "int", nullable: false),
                    CustKey = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DocDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BirSeries = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Gross = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Cewt = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RfcBoRtv = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Other = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Net = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeductionsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    PostedInvoiceId = table.Column<int>(type: "int", nullable: true),
                    SystemTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SystemRfc = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FetchRequestId = table.Column<int>(type: "int", nullable: true),
                    Flags = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AcceptedFlags = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AcceptRemark = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AcceptedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemittanceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemittanceLines_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RemittanceLines_PostedInvoices_PostedInvoiceId",
                        column: x => x.PostedInvoiceId,
                        principalTable: "PostedInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Remittances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SiteId = table.Column<int>(type: "int", nullable: false),
                    ChainName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PaymentMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Bank = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CheckNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CheckDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CheckAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DepositBatchId = table.Column<int>(type: "int", nullable: true),
                    VerificationStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReturnReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_Remittances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Remittances_DepositBatches_DepositBatchId",
                        column: x => x.DepositBatchId,
                        principalTable: "DepositBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Remittances_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RemittanceHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RemittanceId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    By = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemittanceHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemittanceHistories_Remittances_RemittanceId",
                        column: x => x.RemittanceId,
                        principalTable: "Remittances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Description", "Key", "Name" },
                values: new object[] { 27, "Custodian Verifier: encode CRs and Remittance Advices (NKA) for every branch, uploading the chain's RA Excel", "collections-ra-encode", "Remittance Advice Encoding" });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[] { 27, 1 });

            // Custodian Verifier (user 2026-10-15): HO, every branch — encodes CRs and Remittance Advices (NKA chains).
            // Idempotent like the AddCollections / AddCncVerifier roles.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'Custodian Verifier')
                    INSERT INTO Roles (Name, Description, CreatedAt, CreatedBy)
                    VALUES ('Custodian Verifier', 'Encodes CRs and Remittance Advices (NKA chains) for every branch: types the OR / DCR no., uploads the RA Excel and resolves its mismatches', GETUTCDATE(), 'system');

                INSERT INTO RolePermissions (RoleId, PermissionId)
                SELECT r.Id, p.Id FROM Roles r
                JOIN (VALUES ('Custodian Verifier', 27), ('Custodian Verifier', 21), ('Custodian Verifier', 24)) g(RoleName, PermId) ON g.RoleName = r.Name
                JOIN Permissions p ON p.Id = g.PermId
                WHERE NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = r.Id AND x.PermissionId = p.Id);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Collections_RemittanceId",
                table: "Collections",
                column: "RemittanceId");

            migrationBuilder.CreateIndex(
                name: "IX_RemittanceHistories_RemittanceId",
                table: "RemittanceHistories",
                column: "RemittanceId");

            migrationBuilder.CreateIndex(
                name: "IX_RemittanceLines_CollectionId",
                table: "RemittanceLines",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_RemittanceLines_PostedInvoiceId",
                table: "RemittanceLines",
                column: "PostedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Remittances_DepositBatchId",
                table: "Remittances",
                column: "DepositBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Remittances_SiteId",
                table: "Remittances",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_Remittances_VerificationStatus",
                table: "Remittances",
                column: "VerificationStatus");

            migrationBuilder.AddForeignKey(
                name: "FK_Collections_Remittances_RemittanceId",
                table: "Collections",
                column: "RemittanceId",
                principalTable: "Remittances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Collections_Remittances_RemittanceId",
                table: "Collections");

            migrationBuilder.DropTable(
                name: "RemittanceHistories");

            migrationBuilder.DropTable(
                name: "RemittanceLines");

            migrationBuilder.DropTable(
                name: "Remittances");

            migrationBuilder.DropIndex(
                name: "IX_Collections_RemittanceId",
                table: "Collections");

            migrationBuilder.Sql("DELETE FROM RolePermissions WHERE PermissionId = 27 AND RoleId <> 1");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 27, 1 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DropColumn(
                name: "DcrNo",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "HomsDeptCode",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "RemittanceId",
                table: "Collections");

            migrationBuilder.AlterColumn<string>(
                name: "Reference",
                table: "CollectionDeductions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
