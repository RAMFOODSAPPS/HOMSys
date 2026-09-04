using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrlistX2Restrictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrlistX2Restrictions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CProdNo = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Zone = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrlistX2Restrictions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrlistX2Restrictions_CProdNo_Zone",
                table: "PrlistX2Restrictions",
                columns: new[] { "CProdNo", "Zone" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrlistX2Restrictions");
        }
    }
}
