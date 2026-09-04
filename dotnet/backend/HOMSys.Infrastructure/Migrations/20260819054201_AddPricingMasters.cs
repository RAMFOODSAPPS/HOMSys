using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingMasters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "NewPrice",
                table: "Products",
                type: "decimal(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OldPrice1",
                table: "Products",
                type: "decimal(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PriceFrom",
                table: "Products",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Srp",
                table: "Products",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PriceHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProdNo = table.Column<int>(type: "int", nullable: false),
                    Effective = table.Column<DateOnly>(type: "date", nullable: true),
                    NpAfVat = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Zone2AddOns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Branch = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CustKey = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    CProdNo = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    EffDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AddOn = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    FixAmt = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zone2AddOns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZoneAddOns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Branch = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CProdNo = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    CZone = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    EffDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AddOn = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    FixAmt = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneAddOns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriceHistories_ProdNo",
                table: "PriceHistories",
                column: "ProdNo");

            migrationBuilder.CreateIndex(
                name: "IX_Zone2AddOns_Branch_CProdNo_CustKey",
                table: "Zone2AddOns",
                columns: new[] { "Branch", "CProdNo", "CustKey" });

            migrationBuilder.CreateIndex(
                name: "IX_ZoneAddOns_Branch_CProdNo_CZone",
                table: "ZoneAddOns",
                columns: new[] { "Branch", "CProdNo", "CZone" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriceHistories");

            migrationBuilder.DropTable(
                name: "Zone2AddOns");

            migrationBuilder.DropTable(
                name: "ZoneAddOns");

            migrationBuilder.DropColumn(
                name: "NewPrice",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "OldPrice1",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PriceFrom",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Srp",
                table: "Products");
        }
    }
}
