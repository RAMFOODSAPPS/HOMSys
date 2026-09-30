using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBridgeClaim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BridgeClaimedAt",
                table: "SalesOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BridgeClaimedBy",
                table: "SalesOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BridgeClaimedAt",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "BridgeClaimedBy",
                table: "SalesOrders");
        }
    }
}
