using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTasafForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Link = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tasafs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RefNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartMonth = table.Column<int>(type: "int", nullable: true),
                    StartYear = table.Column<int>(type: "int", nullable: true),
                    EndMonth = table.Column<int>(type: "int", nullable: true),
                    EndYear = table.Column<int>(type: "int", nullable: true),
                    SalesGroupId = table.Column<int>(type: "int", nullable: true),
                    AreaId = table.Column<int>(type: "int", nullable: true),
                    ChainId = table.Column<int>(type: "int", nullable: true),
                    CustKey = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ProgramTypeId = table.Column<int>(type: "int", nullable: true),
                    ProgramTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ObjectiveId = table.Column<int>(type: "int", nullable: true),
                    ObjectiveOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TargetMarketId = table.Column<int>(type: "int", nullable: true),
                    TargetMarketOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChannelId = table.Column<int>(type: "int", nullable: true),
                    ProductGroupId = table.Column<int>(type: "int", nullable: true),
                    ProductGroup2Id = table.Column<int>(type: "int", nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    Category2Id = table.Column<int>(type: "int", nullable: true),
                    Background = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Concessions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Mechanics = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PromotedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PrePromoSales = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IncrementalSales = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PayInvDeductionAp = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PayInvDeductionSalesman = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PayCheckAp = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PayCheckSalesman = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PayFreeGoodsAp = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PayFreeGoodsSalesman = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PayOffInvoiceAp = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PayOffInvoiceSalesman = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ProponentUserId = table.Column<int>(type: "int", nullable: false),
                    ProponentName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewerUserId = table.Column<int>(type: "int", nullable: true),
                    ReviewerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalLevelId = table.Column<int>(type: "int", nullable: true),
                    ApprovalLevelName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ApproverUserId = table.Column<int>(type: "int", nullable: true),
                    ApproverName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasafs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tasafs_TasafAreas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "TasafAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasafs_TasafCategories_Category2Id",
                        column: x => x.Category2Id,
                        principalTable: "TasafCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasafs_TasafCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "TasafCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasafs_TasafChains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "TasafChains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasafs_TasafChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "TasafChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasafs_TasafObjectives_ObjectiveId",
                        column: x => x.ObjectiveId,
                        principalTable: "TasafObjectives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasafs_TasafProductGroups_ProductGroup2Id",
                        column: x => x.ProductGroup2Id,
                        principalTable: "TasafProductGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasafs_TasafProductGroups_ProductGroupId",
                        column: x => x.ProductGroupId,
                        principalTable: "TasafProductGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasafs_TasafProgramTypes_ProgramTypeId",
                        column: x => x.ProgramTypeId,
                        principalTable: "TasafProgramTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasafs_TasafSalesGroups_SalesGroupId",
                        column: x => x.SalesGroupId,
                        principalTable: "TasafSalesGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasafs_TasafTargetMarkets_TargetMarketId",
                        column: x => x.TargetMarketId,
                        principalTable: "TasafTargetMarkets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasafs_Users_ProponentUserId",
                        column: x => x.ProponentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TasafActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TasafId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ToStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ByUserId = table.Column<int>(type: "int", nullable: false),
                    ByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafActions_Tasafs_TasafId",
                        column: x => x.TasafId,
                        principalTable: "Tasafs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TasafBranches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TasafId = table.Column<int>(type: "int", nullable: false),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    SiteId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafBranches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafBranches_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TasafBranches_Tasafs_TasafId",
                        column: x => x.TasafId,
                        principalTable: "Tasafs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TasafCosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TasafId = table.Column<int>(type: "int", nullable: false),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    CostTypeId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafCosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafCosts_TasafCostTypes_CostTypeId",
                        column: x => x.CostTypeId,
                        principalTable: "TasafCostTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TasafCosts_Tasafs_TasafId",
                        column: x => x.TasafId,
                        principalTable: "Tasafs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TasafProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TasafId = table.Column<int>(type: "int", nullable: false),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    CProdNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    P3MoAvgCs = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SplyCs = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PromotedVolCs = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IncrVolCs = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LspPerCase = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PromotedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafProducts_Tasafs_TasafId",
                        column: x => x.TasafId,
                        principalTable: "Tasafs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Description", "Key", "Name" },
                values: new object[,]
                {
                    { 17, "Create and submit own Trade Activity Spend Approval Forms", "tasaf", "TASAF" },
                    { 18, "View every TASAF, not only your own (also needs TASAF)", "tasaf-view-all", "TASAF View All" }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 17, 1 },
                    { 18, 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_ReadAt",
                table: "Notifications",
                columns: new[] { "UserId", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TasafActions_TasafId",
                table: "TasafActions",
                column: "TasafId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafBranches_SiteId",
                table: "TasafBranches",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafBranches_TasafId",
                table: "TasafBranches",
                column: "TasafId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafCosts_CostTypeId",
                table: "TasafCosts",
                column: "CostTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafCosts_TasafId",
                table: "TasafCosts",
                column: "TasafId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafProducts_TasafId",
                table: "TasafProducts",
                column: "TasafId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_ApproverUserId",
                table: "Tasafs",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_AreaId",
                table: "Tasafs",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_Category2Id",
                table: "Tasafs",
                column: "Category2Id");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_CategoryId",
                table: "Tasafs",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_ChainId",
                table: "Tasafs",
                column: "ChainId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_ChannelId",
                table: "Tasafs",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_ObjectiveId",
                table: "Tasafs",
                column: "ObjectiveId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_ProductGroup2Id",
                table: "Tasafs",
                column: "ProductGroup2Id");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_ProductGroupId",
                table: "Tasafs",
                column: "ProductGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_ProgramTypeId",
                table: "Tasafs",
                column: "ProgramTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_ProponentUserId",
                table: "Tasafs",
                column: "ProponentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_RefNo",
                table: "Tasafs",
                column: "RefNo",
                unique: true,
                filter: "[RefNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_ReviewerUserId",
                table: "Tasafs",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_SalesGroupId",
                table: "Tasafs",
                column: "SalesGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_Status",
                table: "Tasafs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_TargetMarketId",
                table: "Tasafs",
                column: "TargetMarketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "TasafActions");

            migrationBuilder.DropTable(
                name: "TasafBranches");

            migrationBuilder.DropTable(
                name: "TasafCosts");

            migrationBuilder.DropTable(
                name: "TasafProducts");

            migrationBuilder.DropTable(
                name: "Tasafs");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 17, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 18, 1 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 18);
        }
    }
}
