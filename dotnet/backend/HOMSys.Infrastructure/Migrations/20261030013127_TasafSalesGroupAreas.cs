using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafSalesGroupAreas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AreaNames",
                table: "TasafSalesGroups",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "TasafSalesGroups",
                keyColumn: "Id",
                keyValue: 1,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafSalesGroups",
                keyColumn: "Id",
                keyValue: 2,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafSalesGroups",
                keyColumn: "Id",
                keyValue: 3,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafSalesGroups",
                keyColumn: "Id",
                keyValue: 4,
                column: "AreaNames",
                value: "");

            // Areas per sales group = the REGION values each SALESGROUP has in RAM BRANCHES ACTIVECUSTOMER
            // LIST 2.xlsx (CUSTOMER FILE); no group has NCR there. Editable in TASAF Setup › Sales Groups.
            migrationBuilder.Sql("""
                UPDATE TasafSalesGroups SET AreaNames = CASE Code
                    WHEN 'NKA' THEN 'MT/NKA'
                    WHEN 'RKA' THEN 'MT/NKA, South Luzon, Visayas, West Mindanao'
                    WHEN 'LKA' THEN 'North Luzon, South Luzon, Visayas, West Mindanao, Central Mindanao, MT/NKA'
                    WHEN 'GT'  THEN 'North Luzon, South Luzon, Visayas, West Mindanao, Central Mindanao, MT/NKA'
                    ELSE AreaNames END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AreaNames",
                table: "TasafSalesGroups");
        }
    }
}
