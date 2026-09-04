using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOosSyncLineTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllocatedQtyCs",
                table: "SalesOrderLines");

            migrationBuilder.DropColumn(
                name: "AllocatedQtyPc",
                table: "SalesOrderLines");

            migrationBuilder.CreateTable(
                name: "OosSyncLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SoId = table.Column<int>(type: "int", nullable: false),
                    CProdNo = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    AllocatedQtyCs = table.Column<int>(type: "int", nullable: false),
                    AllocatedQtyPc = table.Column<int>(type: "int", nullable: false),
                    StkFlag = table.Column<int>(type: "int", nullable: true),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OosSyncLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OosSyncLines_SalesOrders_SoId",
                        column: x => x.SoId,
                        principalTable: "SalesOrders",
                        principalColumn: "SoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OosSyncLines_SoId_CProdNo",
                table: "OosSyncLines",
                columns: new[] { "SoId", "CProdNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OosSyncLines");

            migrationBuilder.AddColumn<int>(
                name: "AllocatedQtyCs",
                table: "SalesOrderLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AllocatedQtyPc",
                table: "SalesOrderLines",
                type: "int",
                nullable: true);
        }
    }
}
