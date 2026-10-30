using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PostedRfcNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PostedRfcs_WhseNo_SourceFile_InvoiceNo",
                table: "PostedRfcs");

            migrationBuilder.AddColumn<DateOnly>(
                name: "RfcDate",
                table: "PostedRfcs",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RfcNo",
                table: "PostedRfcs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PostedRfcs_WhseNo_SourceFile_InvoiceNo_RfcNo",
                table: "PostedRfcs",
                columns: new[] { "WhseNo", "SourceFile", "InvoiceNo", "RfcNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PostedRfcs_WhseNo_SourceFile_InvoiceNo_RfcNo",
                table: "PostedRfcs");

            migrationBuilder.DropColumn(
                name: "RfcDate",
                table: "PostedRfcs");

            migrationBuilder.DropColumn(
                name: "RfcNo",
                table: "PostedRfcs");

            migrationBuilder.CreateIndex(
                name: "IX_PostedRfcs_WhseNo_SourceFile_InvoiceNo",
                table: "PostedRfcs",
                columns: new[] { "WhseNo", "SourceFile", "InvoiceNo" },
                unique: true);
        }
    }
}
