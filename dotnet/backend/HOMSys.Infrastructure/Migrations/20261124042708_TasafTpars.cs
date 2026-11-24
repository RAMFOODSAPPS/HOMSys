using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafTpars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TasafTpars",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TasafId = table.Column<int>(type: "int", nullable: false),
                    TparNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafTpars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafTpars_Tasafs_TasafId",
                        column: x => x.TasafId,
                        principalTable: "Tasafs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Description", "Key", "Name" },
                values: new object[] { 30, "Enter TPARs (Trade Promo Analysis Reports) against Approved TASAFs", "tpar", "TPAR Encoding" });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[] { 30, 1 });

            migrationBuilder.CreateIndex(
                name: "IX_TasafTpars_TasafId",
                table: "TasafTpars",
                column: "TasafId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafTpars_TparNo",
                table: "TasafTpars",
                column: "TparNo",
                unique: true,
                filter: "[Status] <> N'Cancelled'");

            // Each TASAF's old single TPAR No. becomes its first TPAR — Approved (it was final then), Amount 0 (no amount
            // was kept). A number used on two TASAFs gets " (#id)" on the later one so the unique index holds.
            migrationBuilder.Sql("""
                INSERT INTO TasafTpars (TasafId, TparNo, Amount, Remarks, Status, CreatedByUserId, CreatedByName, CreatedAt, ApprovedAt, ApprovedBy)
                SELECT t.Id,
                       CASE WHEN ROW_NUMBER() OVER (PARTITION BY LTRIM(RTRIM(t.TparNo)) ORDER BY t.Id) = 1
                            THEN LTRIM(RTRIM(t.TparNo))
                            ELSE LEFT(LTRIM(RTRIM(t.TparNo)), 38) + N' (#' + CAST(t.Id AS nvarchar(10)) + N')' END,
                       0, N'Entered before TPAR amounts were kept', N'Approved',
                       a.ByUserId, COALESCE(a.ByName, t.UpdatedBy, N''), COALESCE(a.At, t.UpdatedAt, t.CreatedAt),
                       COALESCE(a.At, t.UpdatedAt, t.CreatedAt), COALESCE(a.ByName, t.UpdatedBy)
                FROM Tasafs t
                OUTER APPLY (SELECT TOP 1 x.ByUserId, x.ByName, x.At FROM TasafActions x
                             WHERE x.TasafId = t.Id AND x.Action IN (N'Closed', N'TPAR No.') ORDER BY x.Id DESC) a
                WHERE t.TparNo IS NOT NULL AND LTRIM(RTRIM(t.TparNo)) <> N'';
                """);

            // "Closed" is now "Completed" (user decision 2026-11-24).
            migrationBuilder.Sql("UPDATE Tasafs SET Status = N'Completed' WHERE Status = N'Closed';");
            migrationBuilder.Sql("UPDATE TasafActions SET FromStatus = N'Completed' WHERE FromStatus = N'Closed';");
            migrationBuilder.Sql("UPDATE TasafActions SET ToStatus = N'Completed' WHERE ToStatus = N'Closed';");
            migrationBuilder.Sql("UPDATE TasafActions SET Action = N'Completed' WHERE Action = N'Closed';");

            migrationBuilder.DropColumn(
                name: "TparNo",
                table: "Tasafs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to one TPAR No. per TASAF (the first live one) and "Closed".
            migrationBuilder.AddColumn<string>(
                name: "TparNo",
                table: "Tasafs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
            migrationBuilder.Sql("""
                UPDATE t SET TparNo = (SELECT TOP 1 p.TparNo FROM TasafTpars p
                                       WHERE p.TasafId = t.Id AND p.Status <> N'Cancelled' ORDER BY p.Id)
                FROM Tasafs t;
                """);
            migrationBuilder.Sql("UPDATE Tasafs SET Status = N'Closed' WHERE Status = N'Completed';");

            migrationBuilder.DropTable(
                name: "TasafTpars");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 30, 1 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 30);
        }
    }
}
