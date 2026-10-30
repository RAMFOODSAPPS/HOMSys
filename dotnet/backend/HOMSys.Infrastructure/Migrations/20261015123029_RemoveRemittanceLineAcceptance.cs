using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRemittanceLineAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptRemark",
                table: "RemittanceLines");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "RemittanceLines");

            migrationBuilder.DropColumn(
                name: "AcceptedBy",
                table: "RemittanceLines");

            migrationBuilder.DropColumn(
                name: "AcceptedFlags",
                table: "RemittanceLines");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcceptRemark",
                table: "RemittanceLines",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAt",
                table: "RemittanceLines",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptedBy",
                table: "RemittanceLines",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptedFlags",
                table: "RemittanceLines",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
