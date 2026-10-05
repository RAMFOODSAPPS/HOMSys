using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteBmsDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "BmsDate",
                table: "Sites",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BmsDateUpdatedUtc",
                table: "Sites",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BmsDate",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "BmsDateUpdatedUtc",
                table: "Sites");
        }
    }
}
