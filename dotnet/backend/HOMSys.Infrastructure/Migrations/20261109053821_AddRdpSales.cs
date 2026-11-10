using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRdpSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RdpChannelMaps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RdpChannelMaps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RdpDistributors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustKey = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Rdm = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Territory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SiteId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RdpDistributors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RdpDistributors_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RdpSalesUploads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    CustKeys = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Periods = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LineCount = table.Column<int>(type: "int", nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReplacedLines = table.Column<int>(type: "int", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RdpSalesUploads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RdpSalesLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UploadId = table.Column<int>(type: "int", nullable: false),
                    DistributorId = table.Column<int>(type: "int", nullable: false),
                    Period = table.Column<DateOnly>(type: "date", nullable: false),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InvoiceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TxnType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AccountCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccountKey = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ChannelLabel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Salesman = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SkuCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ItemDesc = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Uom = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Cases = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RdpSalesLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RdpSalesLines_RdpDistributors_DistributorId",
                        column: x => x.DistributorId,
                        principalTable: "RdpDistributors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RdpSalesLines_RdpSalesUploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "RdpSalesUploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Description", "Key", "Name" },
                values: new object[,]
                {
                    { 28, "View the unique buying accounts dashboard built from RDP (distributor) sales files", "rdp-uba", "UBA Dashboard" },
                    { 29, "Upload RDP (distributor) sales files and maintain the RDP list and channel mapping", "rdp-upload", "RDP Sales Upload" }
                });

            migrationBuilder.InsertData(
                table: "RdpChannelMaps",
                columns: new[] { "Id", "Channel", "Label", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "Sari-sari store", "SARI-SARI STORE", null, null },
                    { 2, "Market stall / Public market", "MARKET STALL / PUBLIC MARKET", null, null },
                    { 3, "Grocery", "GROCERY", null, null },
                    { 4, "Supermarket / Hypermarket", "SUPERMARKET / HYPERMARKET", null, null },
                    { 5, "Convenience store / Minimart", "CONVENIENCE STORE / MINIMART", null, null },
                    { 6, "Wholesaler / Sub-distributor", "WHOLESALER / SUB-DISTRIBUTOR", null, null },
                    { 7, "Drugstore / Pharmacy", "DRUGSTORE / PHARMACY", null, null },
                    { 8, "Foodservice / HORECA", "FOODSERVICE / HORECA", null, null },
                    { 9, "Bakery / Baking supplies", "BAKERY / BAKING SUPPLIES", null, null },
                    { 10, "Meat shop", "MEAT SHOP", null, null },
                    { 11, "Cooperative", "COOPERATIVE", null, null },
                    { 12, "Other channels", "OTHER CHANNELS", null, null },
                    { 13, "Sari-sari store", "SARI-SARI", null, null },
                    { 14, "Sari-sari store", "SARI - SARI STORE", null, null },
                    { 15, "Sari-sari store", "SIDE STREET", null, null },
                    { 16, "Sari-sari store", "SSST", null, null },
                    { 17, "Sari-sari store", "SARISARI", null, null },
                    { 18, "Market stall / Public market", "MARKET STALL", null, null },
                    { 19, "Market stall / Public market", "PUBLIC MARKET", null, null },
                    { 20, "Market stall / Public market", "MARKET STALL (DRY)", null, null },
                    { 21, "Market stall / Public market", "MARKET STALL RETAIL", null, null },
                    { 22, "Market stall / Public market", "PUBLIC MARKETS", null, null },
                    { 23, "Grocery", "GROCERY STORE", null, null },
                    { 24, "Grocery", "GROCERIES", null, null },
                    { 25, "Grocery", "GROC", null, null },
                    { 26, "Supermarket / Hypermarket", "SUPERMARKET", null, null },
                    { 27, "Supermarket / Hypermarket", "KEY SUPERMARKET", null, null },
                    { 28, "Supermarket / Hypermarket", "SMKT", null, null },
                    { 29, "Supermarket / Hypermarket", "HYPERMARKET", null, null },
                    { 30, "Supermarket / Hypermarket", "SMALL SUPERMARKET", null, null },
                    { 31, "Convenience store / Minimart", "MINI MART", null, null },
                    { 32, "Convenience store / Minimart", "CONVENIENCE STORE", null, null },
                    { 33, "Convenience store / Minimart", "CONV", null, null },
                    { 34, "Convenience store / Minimart", "MINIMART", null, null },
                    { 35, "Convenience store / Minimart", "CONVENIENCE STORE/ MINIMART", null, null },
                    { 36, "Convenience store / Minimart", "CONVIENCE STORE", null, null },
                    { 37, "Wholesaler / Sub-distributor", "WHOLESALER", null, null },
                    { 38, "Wholesaler / Sub-distributor", "WLSL", null, null },
                    { 39, "Wholesaler / Sub-distributor", "SUB-D", null, null },
                    { 40, "Wholesaler / Sub-distributor", "SUBDI", null, null },
                    { 41, "Wholesaler / Sub-distributor", "SUB DISTRIBUTOR", null, null },
                    { 42, "Wholesaler / Sub-distributor", "MARKET STALL WHOLESALE", null, null },
                    { 43, "Drugstore / Pharmacy", "PHARMACY", null, null },
                    { 44, "Drugstore / Pharmacy", "DRUGSTORE", null, null },
                    { 45, "Drugstore / Pharmacy", "DRUG STORE", null, null },
                    { 46, "Drugstore / Pharmacy", "DRGS", null, null },
                    { 47, "Foodservice / HORECA", "FILIPINO RESTAURANT", null, null },
                    { 48, "Foodservice / HORECA", "AT WORK CANTEEN", null, null },
                    { 49, "Foodservice / HORECA", "RESTAURANT", null, null },
                    { 50, "Foodservice / HORECA", "OTHER ASIAN RESTAURANTS", null, null },
                    { 51, "Foodservice / HORECA", "HOTEL", null, null },
                    { 52, "Foodservice / HORECA", "RESORT", null, null },
                    { 53, "Bakery / Baking supplies", "BAKERY", null, null },
                    { 54, "Bakery / Baking supplies", "BAKING SUPPLIES", null, null },
                    { 55, "Bakery / Baking supplies", "BAKESHOP", null, null },
                    { 56, "Bakery / Baking supplies", "BAKERY SUPPLIES", null, null },
                    { 57, "Bakery / Baking supplies", "BKRY", null, null },
                    { 58, "Meat shop", "MEAT SHOP- INSTI", null, null },
                    { 59, "Cooperative", "COOPERATIVES", null, null },
                    { 60, "Cooperative", "COOPERATIVE GROWERS", null, null },
                    { 61, "Cooperative", "COOPERATIVE-INSTI", null, null },
                    { 62, "Other channels", "END USER", null, null },
                    { 63, "Other channels", "RETAIL", null, null },
                    { 64, "Other channels", "OTHER", null, null },
                    { 65, "Other channels", "DEPARTMENT STORE", null, null },
                    { 66, "Other channels", "OTHR", null, null },
                    { 67, "Other channels", "OTP / LGU ORDER / SERVICING ONLY", null, null }
                });

            migrationBuilder.InsertData(
                table: "RdpDistributors",
                columns: new[] { "Id", "Area", "CustKey", "IsActive", "Name", "Rdm", "SiteId", "Territory", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "NCL GT", 1010022, true, "DIRECT REACH DISTRIBUTOR INC.", "RICARDO ANTONIO", null, "Luzon", null, null },
                    { 2, "SL GT", 1610289, true, "COTTON CITY MARKETING CORPORATION", "RENAN MARUKOT", null, "Luzon", null, null },
                    { 3, "SL GT", 1610292, true, "CHASE MEGA CORPORATION", "RENAN MARUKOT", null, "Luzon", null, null },
                    { 4, "NCL GT", 2110253, true, "TJA MARKETING CORPORATION", "RICARDO ANTONIO", null, "Luzon", null, null },
                    { 5, "NCL GT", 2110286, true, "DRB SALES,INC.", "RICARDO ANTONIO", null, "Luzon", null, null },
                    { 6, "GMA GT", 2210124, true, "BONHEUR MARKETING CORPORATION", "ANDREW ANICETO", null, "Luzon", null, null },
                    { 7, "GMA GT", 2210128, true, "SOUTHLINE CHANNEL MARKETING", "ANDREW ANICETO", null, "Luzon", null, null },
                    { 8, "GMA GT", 2210129, true, "RACE VENTURES INC.", "ANDREW ANICETO", null, "Luzon", null, null },
                    { 9, "SL GT", 2210163, true, "COEL MARKETING, INC.", "RENAN MARUKOT", null, "Luzon", null, null },
                    { 10, "VIS GT", 2210172, true, "NETMAN DISTRIBUTORS, INC.", "NIKKO CAPIOSO", null, "Visayas", null, null },
                    { 11, "SL GT", 2210186, true, "TWO PRINCE TRADING OPC", "RENAN MARUKOT", null, "Luzon", null, null },
                    { 12, "SL GT", 2210206, true, "SOUTHCORE MARKETING CORPORATION", "RENAN MARUKOT", null, "Luzon", null, null },
                    { 13, "SL GT", 2210214, true, "CHUA PHILIPPINES CORPORATION", "RENAN MARUKOT", null, "Luzon", null, null },
                    { 14, "SL GT", 2210219, true, "EIGHT PRINCE TRADING OPC", "RENAN MARUKOT", null, "Luzon", null, null },
                    { 15, "GMA GT", 2210228, true, "1 TO 1 MARKETING INC.", "ANDREW ANICETO", null, "Luzon", null, null },
                    { 16, "SL GT", 2310257, true, "JT INTANDEM DISTRIBUTION", "RENAN MARUKOT", null, "Luzon", null, null },
                    { 17, "NCL GT", 2510474, true, "CHIP ACCENDO ENTERPRISES", "RICARDO ANTONIO", null, "Luzon", null, null },
                    { 18, "NCL GT", 2510475, true, "CHIP ACCENDO ENTERPRISES", "RICARDO ANTONIO", null, "Luzon", null, null },
                    { 19, "NCL GT", 2610118, true, "CHEERS TRADESTAR INC.", "RICARDO ANTONIO", null, "Luzon", null, null },
                    { 20, "VIS GT", 4114314, true, "APEX FOODS DISTRIBUTION CORP.", "NIKKO CAPIOSO", null, "Visayas", null, null },
                    { 21, "VIS GT", 4348770, true, "JACAL FOOD MARKETING", "NIKKO CAPIOSO", null, "Visayas", null, null },
                    { 22, "VIS GT", 4425667, true, "BERTAHAN DISTRIBUTION CORPORATION", "NIKKO CAPIOSO", null, "Visayas", null, null },
                    { 23, "MIN GT", 5410194, true, "DESAN CONSUMERS INC.", "ROY AGUHOB", null, "Mindanao", null, null },
                    { 24, "MIN GT", 6153055, true, "SDC GLOBAL CHOICE CORPORATION", "ROY AGUHOB", null, "Mindanao", null, null },
                    { 25, "MIN GT", 6153057, true, "ROMARK SALES INC.", "ROY AGUHOB", null, "Mindanao", null, null },
                    { 26, "MIN GT", 6153073, true, "CAMIGUIN ISLA TRADING INCORPORATED", "ROY AGUHOB", null, "Mindanao", null, null },
                    { 27, "MIN GT", 6153078, true, "HCB FOOD PRODUCTS MARKETING", "ROY AGUHOB", null, "Mindanao", null, null },
                    { 28, "MIN GT", 6153082, true, "PRIMUS VENTURES", "ROY AGUHOB", null, "Mindanao", null, null },
                    { 29, "MIN GT", 6290074, true, "DAVAO SQUIRE DISTRIBUTORS INC.", "ROY AGUHOB", null, "Mindanao", null, null },
                    { 30, "MIN GT", 6290090, true, "CERSUM MARKETING OPC", "ROY AGUHOB", null, "Mindanao", null, null },
                    { 31, "MIN GT", 6394358, true, "BFV CORPORATION", "ROY AGUHOB", null, "Mindanao", null, null },
                    { 32, "MIN GT", 6394361, true, "BFV POINT-OF-SALE MARKETING", "ROY AGUHOB", null, "Mindanao", null, null },
                    { 33, "MIN GT", 6410129, true, "CERSUM MARKETING OPC", "ROY AGUHOB", null, "Mindanao", null, null },
                    { 34, "MIN GT", 6410136, true, "DESAN CONSUMERS INC.", "ROY AGUHOB", null, "Mindanao", null, null },
                    { 35, "MIN GT", 6571170, true, "YI SHUN CONSUMER GOODS TRADING", "ROY AGUHOB", null, "Mindanao", null, null },
                    { 36, "VIS GT", 6710039, true, "COVERBEST DISTRIBUTIONS CORPORATION", "NIKKO CAPIOSO", null, "Visayas", null, null },
                    { 37, "MIN GT", 6910107, true, "SHERWIN XR MARKETING", "ROY AGUHOB", null, "Mindanao", null, null },
                    { 38, "MIN GT", 6910122, true, "PRIMUS VENTURES", "ROY AGUHOB", null, "Mindanao", null, null }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 28, 1 },
                    { 29, 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_RdpChannelMaps_Label",
                table: "RdpChannelMaps",
                column: "Label",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RdpDistributors_CustKey",
                table: "RdpDistributors",
                column: "CustKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RdpDistributors_SiteId",
                table: "RdpDistributors",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_RdpSalesLines_DistributorId_Period",
                table: "RdpSalesLines",
                columns: new[] { "DistributorId", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_RdpSalesLines_UploadId",
                table: "RdpSalesLines",
                column: "UploadId");

            // Servicing branch per the "RDP List" sheet, linked to the Sites master by code (Sites aren't seed data).
            migrationBuilder.Sql("""
                UPDATE d SET SiteId = s.Id
                FROM RdpDistributors d
                JOIN (VALUES
                    (1010022, 'MEX-B'), (1610289, 'LEG-B'), (1610292, 'LEG-B'), (2110253, 'DAG-B'), (2110286, 'DAG-B'),
                    (2210124, 'CDC-B'), (2210128, 'CDC-B'), (2210129, 'CDC-B'), (2210163, 'CDC-B'), (2210172, 'CDC-B'),
                    (2210186, 'CDC-B'), (2210206, 'CDC-B'), (2210214, 'CDC-B'), (2210219, 'CDC-B'), (2210228, 'CDC-B'),
                    (2310257, 'NAG-B'), (2510474, 'MEX-B'), (2510475, 'MEX-B'), (2610118, 'ISA-B'), (4114314, 'CDC-B'),
                    (4348770, 'BAC-B'), (4425667, 'CDC-B'), (5410194, 'KID-B'), (6153055, 'CDO-B'), (6153057, 'CDO-B'),
                    (6153073, 'CDO-B'), (6153078, 'CDO-B'), (6153082, 'CDO-B'), (6290074, 'DAV-B'), (6290090, 'DAV-B'),
                    (6394358, 'ZAM-B'), (6394361, 'ZAM-B'), (6410129, 'GEN-B'), (6410136, 'GEN-B'), (6571170, 'OZA-B'),
                    (6710039, 'DUM-B'), (6910107, 'BUT-B'), (6910122, 'BUT-B')
                ) AS m(CustKey, Code) ON m.CustKey = d.CustKey
                JOIN Sites s ON s.Code = m.Code
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RdpChannelMaps");

            migrationBuilder.DropTable(
                name: "RdpSalesLines");

            migrationBuilder.DropTable(
                name: "RdpDistributors");

            migrationBuilder.DropTable(
                name: "RdpSalesUploads");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 28, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 29, 1 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 29);
        }
    }
}
