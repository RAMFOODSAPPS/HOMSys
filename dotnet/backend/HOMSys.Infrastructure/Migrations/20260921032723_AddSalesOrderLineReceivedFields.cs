using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesOrderLineReceivedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedAmt",
                table: "SalesOrderLines",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReceivedQtyCs",
                table: "SalesOrderLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReceivedQtyPc",
                table: "SalesOrderLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceivedStatus",
                table: "SalesOrderLines",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceivedAmt",
                table: "SalesOrderLines");

            migrationBuilder.DropColumn(
                name: "ReceivedQtyCs",
                table: "SalesOrderLines");

            migrationBuilder.DropColumn(
                name: "ReceivedQtyPc",
                table: "SalesOrderLines");

            migrationBuilder.DropColumn(
                name: "ReceivedStatus",
                table: "SalesOrderLines");
        }
    }
}
