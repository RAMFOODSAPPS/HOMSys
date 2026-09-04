using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesOrderSourceFileHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceFileHash",
                table: "SalesOrders",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFileName",
                table: "SalesOrders",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_SourceFileHash",
                table: "SalesOrders",
                column: "SourceFileHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_SourceFileHash",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "SourceFileHash",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "SourceFileName",
                table: "SalesOrders");
        }
    }
}
