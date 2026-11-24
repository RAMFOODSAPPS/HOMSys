using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UserTempPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TempPasswordExpiresAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TempPasswordFailures",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TempPasswordHash",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TempPasswordRequestedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "TempPasswordExpiresAt", "TempPasswordFailures", "TempPasswordHash", "TempPasswordRequestedAt" },
                values: new object[] { null, 0, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TempPasswordExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TempPasswordFailures",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TempPasswordHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TempPasswordRequestedAt",
                table: "Users");
        }
    }
}
