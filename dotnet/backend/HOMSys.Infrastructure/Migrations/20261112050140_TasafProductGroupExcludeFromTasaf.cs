using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafProductGroupExcludeFromTasaf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExcludeFromTasaf",
                table: "TasafProductGroups",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 1,
                column: "ExcludeFromTasaf",
                value: false);

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 2,
                column: "ExcludeFromTasaf",
                value: false);

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 3,
                column: "ExcludeFromTasaf",
                value: false);

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 4,
                column: "ExcludeFromTasaf",
                value: false);

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 5,
                column: "ExcludeFromTasaf",
                value: false);

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 6,
                column: "ExcludeFromTasaf",
                value: false);

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 7,
                column: "ExcludeFromTasaf",
                value: false);

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 8,
                column: "ExcludeFromTasaf",
                value: false);

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 9,
                column: "ExcludeFromTasaf",
                value: false);

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 10,
                column: "ExcludeFromTasaf",
                value: false);

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 11,
                column: "ExcludeFromTasaf",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExcludeFromTasaf",
                table: "TasafProductGroups");
        }
    }
}
