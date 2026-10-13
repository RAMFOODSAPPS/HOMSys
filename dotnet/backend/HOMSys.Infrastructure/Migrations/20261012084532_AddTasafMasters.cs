using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTasafMasters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TasafApprovalLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApproverUserId = table.Column<int>(type: "int", nullable: true),
                    MaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafApprovalLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafApprovalLevels_Users_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TasafAreas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafAreas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TasafChannels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafChannels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TasafCostTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafCostTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TasafObjectives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafObjectives", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TasafProductGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafProductGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TasafProgramTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafProgramTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TasafSalesGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GkamUserId = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafSalesGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafSalesGroups_Users_GkamUserId",
                        column: x => x.GkamUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TasafTargetMarkets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafTargetMarkets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TasafCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductGroupId = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafCategories_TasafProductGroups_ProductGroupId",
                        column: x => x.ProductGroupId,
                        principalTable: "TasafProductGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TasafChains",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesGroupId = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafChains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafChains_TasafSalesGroups_SalesGroupId",
                        column: x => x.SalesGroupId,
                        principalTable: "TasafSalesGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Description", "Key", "Name" },
                values: new object[] { 16, "Maintain TASAF master lists, GKAM reviewers and the approval matrix", "tasaf-masters", "TASAF Setup" });

            migrationBuilder.InsertData(
                table: "TasafApprovalLevels",
                columns: new[] { "Id", "ApproverUserId", "Code", "CreatedAt", "CreatedBy", "Description", "IsActive", "MaxAmount", "Name", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, null, "L1", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, 50000m, "Trade Marketing Manager", 1, null, null },
                    { 2, null, "L2", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, 100000m, "COO", 2, null, null },
                    { 3, null, "L3", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, null, "President & Chairman (VVK)", 3, null, null }
                });

            migrationBuilder.InsertData(
                table: "TasafAreas",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "Description", "IsActive", "Name", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "GMA / NCR", 1, null, null },
                    { 2, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "North Central Luzon", 2, null, null },
                    { 3, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "South Luzon", 3, null, null },
                    { 4, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Bicol", 4, null, null },
                    { 5, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "East Visayas", 5, null, null },
                    { 6, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "West Visayas", 6, null, null },
                    { 7, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "North Mindanao", 7, null, null },
                    { 8, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "South Mindanao", 8, null, null },
                    { 9, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Total Luzon", 9, null, null },
                    { 10, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Total Visayas", 10, null, null },
                    { 11, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Total Mindanao", 11, null, null },
                    { 12, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Total VisMin", 12, null, null },
                    { 13, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Nationwide", 13, null, null }
                });

            migrationBuilder.InsertData(
                table: "TasafChains",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "Description", "IsActive", "Name", "SalesGroupId", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "SVI", null, 1, null, null },
                    { 2, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Waltermart", null, 2, null, null },
                    { 3, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Puregold", null, 3, null, null },
                    { 4, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Landmark", null, 4, null, null },
                    { 5, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Robinson", null, 5, null, null },
                    { 6, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Suysing", null, 6, null, null },
                    { 7, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Super 8", null, 7, null, null },
                    { 8, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "CSI", null, 8, null, null },
                    { 9, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "RCS Chain", null, 9, null, null },
                    { 10, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Savewise", null, 10, null, null },
                    { 11, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "LCC", null, 11, null, null },
                    { 12, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Citi Mart", null, 12, null, null },
                    { 13, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Prince", null, 13, null, null },
                    { 14, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Metro Gaisano", null, 14, null, null },
                    { 15, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "NCCC", null, 15, null, null },
                    { 16, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "KCC", null, 16, null, null },
                    { 17, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Gaisano Grand", null, 17, null, null },
                    { 18, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Gaisano Capital", null, 18, null, null },
                    { 19, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Gaisano Mall", null, 19, null, null },
                    { 20, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "LKA Others", null, 20, null, null },
                    { 21, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "RDP Others", null, 21, null, null }
                });

            migrationBuilder.InsertData(
                table: "TasafChannels",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "Description", "IsActive", "Name", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "MT Supermarket", 1, null, null },
                    { 2, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "MT Convenience Store", 2, null, null },
                    { 3, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "MT Grocery", 3, null, null },
                    { 4, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "MT WS/Reseller", 4, null, null },
                    { 5, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "GT Supermarkets & Groceries", 5, null, null },
                    { 6, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "GT Market Stall & SSS", 6, null, null },
                    { 7, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "GT Wholesaler", 7, null, null },
                    { 8, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "ALL CHANNELS", 8, null, null }
                });

            migrationBuilder.InsertData(
                table: "TasafCostTypes",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "Description", "IsActive", "Name", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Promotions - Bundling Fee", 1, null, null },
                    { 2, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Promotions - Joining Fee", 2, null, null },
                    { 3, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Promotions - Rebates", 3, null, null },
                    { 4, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Promotions - Bundling Materials", 4, null, null },
                    { 5, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Promotions - Free Goods", 5, null, null },
                    { 6, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Anniversary Support - Monetary Support", 6, null, null },
                    { 7, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Anniversary Support - Promo Rebates", 7, null, null },
                    { 8, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Anniversary Support - Dry Sampling", 8, null, null },
                    { 9, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Anniversary Support - Manpower", 9, null, null },
                    { 10, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Opening Support - Monetary Support", 10, null, null },
                    { 11, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Opening Support - Promo Rebates", 11, null, null },
                    { 12, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Opening Support - Dry Sampling", 12, null, null },
                    { 13, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Opening Support - Manpower", 13, null, null },
                    { 14, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Sponsorship - Free Goods", 14, null, null },
                    { 15, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Sponsorship - Monetary Support", 15, null, null },
                    { 16, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Discount - Regular Discount", 16, null, null },
                    { 17, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Discount - Special/ Wholesale Discount", 17, null, null },
                    { 18, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Discount - LGU Discount", 18, null, null },
                    { 19, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Discount - OND Discount", 19, null, null },
                    { 20, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Listing Fee - Listing Fee", 20, null, null },
                    { 21, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Listing Fee - Intro Discount", 21, null, null },
                    { 22, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Convention - Joining Fee", 22, null, null },
                    { 23, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Convention - Rebates", 23, null, null },
                    { 24, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Convention - Manpower", 24, null, null },
                    { 25, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Convention - Booth & Collaterals", 25, null, null },
                    { 26, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Near Ex Moveout - Rebates", 26, null, null },
                    { 27, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Free Goods - Free Goods", 27, null, null }
                });

            migrationBuilder.InsertData(
                table: "TasafObjectives",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "Description", "IsActive", "Name", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Distribution", 1, null, null },
                    { 2, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Generate Trial", 2, null, null },
                    { 3, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Increase AWOP", 3, null, null },
                    { 4, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Create Brand Loyalty", 4, null, null },
                    { 5, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Others", 5, null, null }
                });

            migrationBuilder.InsertData(
                table: "TasafProductGroups",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "Description", "IsActive", "Name", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "01", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Tom Sauce/Paste", 1, null, null },
                    { 2, "02", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Peas & Beans", 2, null, null },
                    { 3, "03", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Pasta & Sauces", 3, null, null },
                    { 4, "04", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Mitra", 4, null, null },
                    { 5, "05", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Raisins", 5, null, null },
                    { 6, "06", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Pickles", 6, null, null },
                    { 7, "07", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Fruits & Vegetables", 7, null, null },
                    { 8, "08", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Baking & Dry Goods", 8, null, null },
                    { 9, "09", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Condiments", 9, null, null },
                    { 10, "CORP", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "TOTAL RAM / CORPORATE PROGRAM", 10, null, null }
                });

            migrationBuilder.InsertData(
                table: "TasafProgramTypes",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "Description", "IsActive", "Name", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "PROMO- EndCon- Bundling/Tie-Up", 1, null, null },
                    { 2, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "PROMO- EndCon- Price-Off/Discount", 2, null, null },
                    { 3, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "PROMO- Reseller- Bundling/Tie-Up", 3, null, null },
                    { 4, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "PROMO- Trade- Case/Trade Deal", 4, null, null },
                    { 5, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "ACCOUNT SUPPORT- Anniversary Support", 5, null, null },
                    { 6, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "ACCOUNT SUPPORT- Opening Support", 6, null, null },
                    { 7, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "ACCOUNT SUPPORT- Sponsorship", 7, null, null },
                    { 8, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Discount", 8, null, null },
                    { 9, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Listing Fee", 9, null, null },
                    { 10, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Convention", 10, null, null },
                    { 11, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Near Ex Moveout", 11, null, null },
                    { 12, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Free Goods", 12, null, null },
                    { 13, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Others", 13, null, null }
                });

            migrationBuilder.InsertData(
                table: "TasafSalesGroups",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "Description", "GkamUserId", "IsActive", "Name", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "NKA", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "National Key Accounts", null, true, "NKA", 1, null, null },
                    { 2, "RKA", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Regional Key Accounts", null, true, "RKA", 2, null, null },
                    { 3, "LKA", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Local Key Accounts", null, true, "LKA", 3, null, null },
                    { 4, "GT", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "General Trade", null, true, "GT", 4, null, null }
                });

            migrationBuilder.InsertData(
                table: "TasafTargetMarkets",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "Description", "IsActive", "Name", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Shopper/End Con", 1, null, null },
                    { 2, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Reseller (Indirect Distribution)", 2, null, null },
                    { 3, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Channel", 3, null, null },
                    { 4, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Region", 4, null, null },
                    { 5, "", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Others", 5, null, null }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[] { 16, 1 });

            migrationBuilder.InsertData(
                table: "TasafCategories",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "Description", "IsActive", "Name", "ProductGroupId", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "1A", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "01 -Tom Sauce/Paste- 1a -Tomato Sauce - Tomato Sauce", true, "Tomato Sauce", 1, 1, null, null },
                    { 2, "1B", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "01 -Tom Sauce/Paste- 1b -Tomato Paste -Tomato Paste", true, "Tomato Paste", 1, 2, null, null },
                    { 3, "2A", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "02 -Peas & Beans- 2a -Peas -Green Peas", true, "Green Peas", 2, 3, null, null },
                    { 4, "2B", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "02 -Peas & Beans- 2b -Peas -Garbanzos", true, "Garbanzos", 2, 4, null, null },
                    { 5, "2C", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "02 -Peas & Beans- 2c -Beans -Black Beans", true, "Black Beans", 2, 5, null, null },
                    { 6, "03", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "03 -Pasta & Sauces", true, "Pasta & Sauces", 3, 6, null, null },
                    { 7, "04", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "04 -Mitra", true, "Mitra", 4, 7, null, null },
                    { 8, "5A", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "05 -Raisins- 5a -Raisins -Ram Raisins", true, "Ram Raisins", 5, 8, null, null },
                    { 9, "5B", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "05 -Raisins- 5b -Prunes -Prunes", true, "Prunes", 5, 9, null, null },
                    { 10, "6A", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "06 -Pickles- 6a -Pickles -Ram Pickles", true, "Ram Pickles", 6, 10, null, null },
                    { 11, "07", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "07 -Fruits & Vegetables", true, "Fruits & Vegetables", 7, 11, null, null },
                    { 12, "8A", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "08 -Baking & Dry Goods- 8a -Baking Goods -Bread Crumbs", true, "Bread Crumbs", 8, 12, null, null },
                    { 13, "09", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "09 -Condiments", true, "Condiments", 9, 13, null, null },
                    { 14, "CORP", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "TOTAL RAM / CORPORATE PROGRAM", true, "TOTAL RAM / CORPORATE PROGRAM", 10, 14, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_TasafApprovalLevels_ApproverUserId",
                table: "TasafApprovalLevels",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafApprovalLevels_Code",
                table: "TasafApprovalLevels",
                column: "Code",
                unique: true,
                filter: "[Code] != ''");

            migrationBuilder.CreateIndex(
                name: "IX_TasafApprovalLevels_Name",
                table: "TasafApprovalLevels",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasafAreas_Code",
                table: "TasafAreas",
                column: "Code",
                unique: true,
                filter: "[Code] != ''");

            migrationBuilder.CreateIndex(
                name: "IX_TasafAreas_Name",
                table: "TasafAreas",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasafCategories_Code",
                table: "TasafCategories",
                column: "Code",
                unique: true,
                filter: "[Code] != ''");

            migrationBuilder.CreateIndex(
                name: "IX_TasafCategories_Name",
                table: "TasafCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasafCategories_ProductGroupId",
                table: "TasafCategories",
                column: "ProductGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafChains_Code",
                table: "TasafChains",
                column: "Code",
                unique: true,
                filter: "[Code] != ''");

            migrationBuilder.CreateIndex(
                name: "IX_TasafChains_Name",
                table: "TasafChains",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasafChains_SalesGroupId",
                table: "TasafChains",
                column: "SalesGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafChannels_Code",
                table: "TasafChannels",
                column: "Code",
                unique: true,
                filter: "[Code] != ''");

            migrationBuilder.CreateIndex(
                name: "IX_TasafChannels_Name",
                table: "TasafChannels",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasafCostTypes_Code",
                table: "TasafCostTypes",
                column: "Code",
                unique: true,
                filter: "[Code] != ''");

            migrationBuilder.CreateIndex(
                name: "IX_TasafCostTypes_Name",
                table: "TasafCostTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasafObjectives_Code",
                table: "TasafObjectives",
                column: "Code",
                unique: true,
                filter: "[Code] != ''");

            migrationBuilder.CreateIndex(
                name: "IX_TasafObjectives_Name",
                table: "TasafObjectives",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasafProductGroups_Code",
                table: "TasafProductGroups",
                column: "Code",
                unique: true,
                filter: "[Code] != ''");

            migrationBuilder.CreateIndex(
                name: "IX_TasafProductGroups_Name",
                table: "TasafProductGroups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasafProgramTypes_Code",
                table: "TasafProgramTypes",
                column: "Code",
                unique: true,
                filter: "[Code] != ''");

            migrationBuilder.CreateIndex(
                name: "IX_TasafProgramTypes_Name",
                table: "TasafProgramTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasafSalesGroups_Code",
                table: "TasafSalesGroups",
                column: "Code",
                unique: true,
                filter: "[Code] != ''");

            migrationBuilder.CreateIndex(
                name: "IX_TasafSalesGroups_GkamUserId",
                table: "TasafSalesGroups",
                column: "GkamUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafSalesGroups_Name",
                table: "TasafSalesGroups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasafTargetMarkets_Code",
                table: "TasafTargetMarkets",
                column: "Code",
                unique: true,
                filter: "[Code] != ''");

            migrationBuilder.CreateIndex(
                name: "IX_TasafTargetMarkets_Name",
                table: "TasafTargetMarkets",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TasafApprovalLevels");

            migrationBuilder.DropTable(
                name: "TasafAreas");

            migrationBuilder.DropTable(
                name: "TasafCategories");

            migrationBuilder.DropTable(
                name: "TasafChains");

            migrationBuilder.DropTable(
                name: "TasafChannels");

            migrationBuilder.DropTable(
                name: "TasafCostTypes");

            migrationBuilder.DropTable(
                name: "TasafObjectives");

            migrationBuilder.DropTable(
                name: "TasafProgramTypes");

            migrationBuilder.DropTable(
                name: "TasafTargetMarkets");

            migrationBuilder.DropTable(
                name: "TasafProductGroups");

            migrationBuilder.DropTable(
                name: "TasafSalesGroups");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 16, 1 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 16);
        }
    }
}
