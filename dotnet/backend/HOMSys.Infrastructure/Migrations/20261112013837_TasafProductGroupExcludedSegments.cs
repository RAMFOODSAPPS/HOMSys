using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafProductGroupExcludedSegments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExcludedSegments",
                table: "TasafProductGroups",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 1,
                column: "ExcludedSegments",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 2,
                column: "ExcludedSegments",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 3,
                column: "ExcludedSegments",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 4,
                column: "ExcludedSegments",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 5,
                column: "ExcludedSegments",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 6,
                column: "ExcludedSegments",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 7,
                column: "ExcludedSegments",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 8,
                column: "ExcludedSegments",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 9,
                column: "ExcludedSegments",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 10,
                column: "ExcludedSegments",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 11,
                column: "ExcludedSegments",
                value: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExcludedSegments",
                table: "TasafProductGroups");
        }
    }
}
