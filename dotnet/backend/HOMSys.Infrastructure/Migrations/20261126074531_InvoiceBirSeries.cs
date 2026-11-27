using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceBirSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DocNoFromBir",
                table: "RemittanceLines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "InvoiceBirSeries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceFile = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    WhseNo = table.Column<int>(type: "int", nullable: false),
                    DocId = table.Column<int>(type: "int", nullable: false),
                    DocNo = table.Column<int>(type: "int", nullable: false),
                    DocDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BirFrom = table.Column<int>(type: "int", nullable: false),
                    BirTo = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceBirSeries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceBirSeries_BirFrom_BirTo",
                table: "InvoiceBirSeries",
                columns: new[] { "BirFrom", "BirTo" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceBirSeries_SourceFile_DocId_DocNo",
                table: "InvoiceBirSeries",
                columns: new[] { "SourceFile", "DocId", "DocNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceBirSeries");

            migrationBuilder.DropColumn(
                name: "DocNoFromBir",
                table: "RemittanceLines");
        }
    }
}
