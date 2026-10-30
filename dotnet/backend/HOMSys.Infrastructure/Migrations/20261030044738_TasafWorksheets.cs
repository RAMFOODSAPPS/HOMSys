using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafWorksheets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TasafWorksheets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProponentUserId = table.Column<int>(type: "int", nullable: false),
                    ProponentName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TasafId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafWorksheets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafWorksheets_Tasafs_TasafId",
                        column: x => x.TasafId,
                        principalTable: "Tasafs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TasafWorksheets_Users_ProponentUserId",
                        column: x => x.ProponentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TasafListingFeeLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorksheetId = table.Column<int>(type: "int", nullable: false),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    SalesGroup = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Account = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    LfPerSku = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SkuCount = table.Column<int>(type: "int", nullable: true),
                    DoorCount = table.Column<int>(type: "int", nullable: true),
                    Basis = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ProjectedAnnualSales = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Cts = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafListingFeeLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafListingFeeLines_TasafWorksheets_WorksheetId",
                        column: x => x.WorksheetId,
                        principalTable: "TasafWorksheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TasafNearExLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorksheetId = table.Column<int>(type: "int", nullable: false),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    SalesGroup = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Account = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    LspPerCase = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LspPerPc = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PcsPerCase = table.Column<int>(type: "int", nullable: true),
                    Expiry = table.Column<DateOnly>(type: "date", nullable: true),
                    InvCases = table.Column<int>(type: "int", nullable: true),
                    InvPieces = table.Column<int>(type: "int", nullable: true),
                    TotalPcs = table.Column<int>(type: "int", nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MoveoutScheme = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Discount = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    MoveoutCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafNearExLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafNearExLines_TasafWorksheets_WorksheetId",
                        column: x => x.WorksheetId,
                        principalTable: "TasafWorksheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TasafListingFeeLines_WorksheetId",
                table: "TasafListingFeeLines",
                column: "WorksheetId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafNearExLines_WorksheetId",
                table: "TasafNearExLines",
                column: "WorksheetId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafWorksheets_ProponentUserId",
                table: "TasafWorksheets",
                column: "ProponentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafWorksheets_TasafId",
                table: "TasafWorksheets",
                column: "TasafId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TasafListingFeeLines");

            migrationBuilder.DropTable(
                name: "TasafNearExLines");

            migrationBuilder.DropTable(
                name: "TasafWorksheets");
        }
    }
}
