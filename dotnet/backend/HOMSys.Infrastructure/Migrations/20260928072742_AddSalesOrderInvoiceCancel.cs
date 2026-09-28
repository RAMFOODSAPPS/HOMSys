using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesOrderInvoiceCancel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CancelledInvNo",
                table: "SalesOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "InvCancelDate",
                table: "SalesOrders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvCancelRemarks",
                table: "SalesOrders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvCancelledBy",
                table: "SalesOrders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelledInvNo",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "InvCancelDate",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "InvCancelRemarks",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "InvCancelledBy",
                table: "SalesOrders");
        }
    }
}
