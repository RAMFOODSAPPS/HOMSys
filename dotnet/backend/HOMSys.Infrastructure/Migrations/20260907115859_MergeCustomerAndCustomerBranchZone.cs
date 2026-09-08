using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MergeCustomerAndCustomerBranchZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerBranchZones");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CustKey",
                table: "Customers");

            migrationBuilder.AddColumn<string>(
                name: "Branch",
                table: "Customers",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Inactive",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RecNo",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Branch",
                table: "Customers",
                column: "Branch");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Branch_RecNo",
                table: "Customers",
                columns: new[] { "Branch", "RecNo" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CustKey",
                table: "Customers",
                column: "CustKey");

            // Every existing Customers row at this point is a pre-merge, BMSRAM-sourced
            // row (that sync stopped 2026-09-05) and defaults to Branch="" from the
            // AddColumn above. Nothing in PricingDataImporter's stale-purge or diff paths
            // will ever touch a Branch="" row (the purge only fires when the discovered
            // branch-folder set itself changes, and the diff only matches rows whose
            // Branch already equals the branch being imported) — so without this, they'd
            // sit forever as duplicates of the fresh F:\-sourced rows the next import
            // creates, now that the CustKey unique index above is gone.
            migrationBuilder.Sql(@"DELETE FROM Customers WHERE Branch = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_Branch",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Branch_RecNo",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CustKey",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Branch",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Inactive",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "RecNo",
                table: "Customers");

            migrationBuilder.CreateTable(
                name: "CustomerBranchZones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Branch = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CZone = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    CustKey = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Inactive = table.Column<bool>(type: "bit", nullable: false),
                    RecNo = table.Column<int>(type: "int", nullable: false),
                    WhseNo = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerBranchZones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CustKey",
                table: "Customers",
                column: "CustKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBranchZones_Branch",
                table: "CustomerBranchZones",
                column: "Branch");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBranchZones_Branch_RecNo",
                table: "CustomerBranchZones",
                columns: new[] { "Branch", "RecNo" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBranchZones_CustKey",
                table: "CustomerBranchZones",
                column: "CustKey");
        }
    }
}
