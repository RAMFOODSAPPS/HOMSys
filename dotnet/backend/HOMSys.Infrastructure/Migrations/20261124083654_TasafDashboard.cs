using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafDashboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DashboardByArea",
                table: "TasafSalesGroups",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "TasafBudgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    SalesGroupId = table.Column<int>(type: "int", nullable: false),
                    ChainId = table.Column<int>(type: "int", nullable: true),
                    AreaId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafBudgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafBudgets_TasafAreas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "TasafAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TasafBudgets_TasafChains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "TasafChains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TasafBudgets_TasafSalesGroups_SalesGroupId",
                        column: x => x.SalesGroupId,
                        principalTable: "TasafSalesGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Description", "Key", "Name" },
                values: new object[] { 31, "Budget dashboard: approved annual budget vs TASAFs created / approved / completed per sales group and account chain", "tasaf-dashboard", "TASAF Dashboard" });

            migrationBuilder.UpdateData(
                table: "TasafSalesGroups",
                keyColumn: "Id",
                keyValue: 1,
                column: "DashboardByArea",
                value: false);

            migrationBuilder.UpdateData(
                table: "TasafSalesGroups",
                keyColumn: "Id",
                keyValue: 2,
                column: "DashboardByArea",
                value: false);

            migrationBuilder.UpdateData(
                table: "TasafSalesGroups",
                keyColumn: "Id",
                keyValue: 3,
                column: "DashboardByArea",
                value: true);

            migrationBuilder.UpdateData(
                table: "TasafSalesGroups",
                keyColumn: "Id",
                keyValue: 4,
                column: "DashboardByArea",
                value: false);

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[] { 31, 1 });

            migrationBuilder.CreateIndex(
                name: "IX_TasafBudgets_AreaId",
                table: "TasafBudgets",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafBudgets_ChainId",
                table: "TasafBudgets",
                column: "ChainId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafBudgets_SalesGroupId",
                table: "TasafBudgets",
                column: "SalesGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafBudgets_Year_SalesGroupId_ChainId_AreaId",
                table: "TasafBudgets",
                columns: new[] { "Year", "SalesGroupId", "ChainId", "AreaId" },
                unique: true);
            // COO and TMM (roles made on the Roles page, so matched by name) also get the dashboard.
            migrationBuilder.Sql(@"
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, 31 FROM Roles r
WHERE r.Name IN (N'COO', N'TMM')
  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = r.Id AND x.PermissionId = 31);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM RolePermissions WHERE PermissionId = 31 AND RoleId <> 1;");
            migrationBuilder.DropTable(
                name: "TasafBudgets");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 31, 1 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DropColumn(
                name: "DashboardByArea",
                table: "TasafSalesGroups");
        }
    }
}
