using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReturnRfcLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RfcLinkedAt",
                table: "CollectionDeductions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RfcLinkedBy",
                table: "CollectionDeductions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RfcNo",
                table: "CollectionDeductions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionDeductions_RfcNo",
                table: "CollectionDeductions",
                column: "RfcNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CollectionDeductions_RfcNo",
                table: "CollectionDeductions");

            migrationBuilder.DropColumn(
                name: "RfcLinkedAt",
                table: "CollectionDeductions");

            migrationBuilder.DropColumn(
                name: "RfcLinkedBy",
                table: "CollectionDeductions");

            migrationBuilder.DropColumn(
                name: "RfcNo",
                table: "CollectionDeductions");
        }
    }
}
