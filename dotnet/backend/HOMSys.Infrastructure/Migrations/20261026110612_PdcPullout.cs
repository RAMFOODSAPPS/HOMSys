using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PdcPullout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PdcPullout",
                table: "Collections",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdcPulloutReason",
                table: "Collections",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PdcPulloutRequestedAt",
                table: "Collections",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdcPulloutRequestedBy",
                table: "Collections",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PdcPullout",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "PdcPulloutReason",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "PdcPulloutRequestedAt",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "PdcPulloutRequestedBy",
                table: "Collections");
        }
    }
}
