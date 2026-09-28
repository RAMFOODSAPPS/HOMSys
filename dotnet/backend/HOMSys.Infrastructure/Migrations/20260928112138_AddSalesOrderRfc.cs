using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesOrderRfc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesOrderRfcs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SoId = table.Column<int>(type: "int", nullable: false),
                    RfcNo = table.Column<int>(type: "int", nullable: false),
                    InvNo = table.Column<int>(type: "int", nullable: false),
                    RfcDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PostedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Remarks2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrderRfcs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesOrderRfcs_SalesOrders_SoId",
                        column: x => x.SoId,
                        principalTable: "SalesOrders",
                        principalColumn: "SoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrderRfcLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RfcId = table.Column<int>(type: "int", nullable: false),
                    CProdNo = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    QtyCs = table.Column<int>(type: "int", nullable: false),
                    QtyPc = table.Column<int>(type: "int", nullable: false),
                    Pieces = table.Column<int>(type: "int", nullable: false),
                    SpAmt = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Amt = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Tax = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    DiscAmt1 = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    DiscAmt2 = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    RetCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    RsNo = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrderRfcLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesOrderRfcLines_SalesOrderRfcs_RfcId",
                        column: x => x.RfcId,
                        principalTable: "SalesOrderRfcs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderRfcLines_RfcId",
                table: "SalesOrderRfcLines",
                column: "RfcId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderRfcs_InvNo",
                table: "SalesOrderRfcs",
                column: "InvNo");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderRfcs_SoId_RfcNo",
                table: "SalesOrderRfcs",
                columns: new[] { "SoId", "RfcNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesOrderRfcLines");

            migrationBuilder.DropTable(
                name: "SalesOrderRfcs");
        }
    }
}
