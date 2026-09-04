using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerZones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Branch = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CustKey = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    CZone = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerZones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerZones_Branch",
                table: "CustomerZones",
                column: "Branch");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerZones_CustKey",
                table: "CustomerZones",
                column: "CustKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerZones");
        }
    }
}
