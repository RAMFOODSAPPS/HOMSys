using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafChainAreas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AreaNames",
                table: "TasafChains",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 1,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 2,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 3,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 4,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 5,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 6,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 7,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 8,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 9,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 10,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 11,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 12,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 13,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 14,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 15,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 16,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 17,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 18,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 19,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 20,
                column: "AreaNames",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 21,
                column: "AreaNames",
                value: "");

            // Areas per chain = the REGION values of the chain's rows in RAM BRANCHES ACTIVECUSTOMER LIST 2.xlsx
            // (CUSTOMER FILE › CUSTOMER/CHAIN/GROUP), counting the rows of the chain's own sales group (Landmark
            // has none for NKA, so all its rows). The GMA chains have no rows; they get NCR, which no sales group
            // lists, so they don't show until NCR is added to a group. Matched by name (ids differ per DB).
            // LKA / RDP Others (inactive) stay blank. Editable in TASAF Setup › Customers / Chains.
            migrationBuilder.Sql("""
                UPDATE TasafChains SET AreaNames = CASE Name
                    WHEN 'RDP GMA'         THEN 'NCR'
                    WHEN 'RDP North Luzon' THEN 'North Luzon'
                    WHEN 'RDP South Luzon' THEN 'South Luzon, MT/NKA'
                    WHEN 'RDP Visayas'     THEN 'Visayas, MT/NKA'
                    WHEN 'RDP Mindanao'    THEN 'Central Mindanao, West Mindanao'
                    WHEN 'LKA GMA'         THEN 'NCR'
                    WHEN 'LKA North Luzon' THEN 'North Luzon'
                    WHEN 'LKA South Luzon' THEN 'South Luzon'
                    WHEN 'LKA Visayas'     THEN 'Visayas'
                    WHEN 'LKA Mindanao'    THEN 'Central Mindanao, West Mindanao'
                    WHEN 'SM Group'        THEN 'MT/NKA'
                    WHEN 'Waltermart'      THEN 'MT/NKA'
                    WHEN 'Puregold'        THEN 'MT/NKA'
                    WHEN 'Landmark'        THEN 'MT/NKA'
                    WHEN 'RRHI'            THEN 'MT/NKA'
                    WHEN 'Suysing'         THEN 'MT/NKA'
                    WHEN 'Super 8'         THEN 'MT/NKA'
                    WHEN 'CSI'             THEN 'MT/NKA'
                    WHEN 'RCS Chain'       THEN 'MT/NKA'
                    WHEN 'Savewise'        THEN 'MT/NKA'
                    WHEN 'LCC'             THEN 'MT/NKA, South Luzon'
                    WHEN 'Citi Mart'       THEN 'MT/NKA, South Luzon'
                    WHEN 'Prince'          THEN 'MT/NKA'
                    WHEN 'Metro Gaisano'   THEN 'MT/NKA, South Luzon'
                    WHEN 'NCCC'            THEN 'MT/NKA'
                    WHEN 'KCC'             THEN 'MT/NKA'
                    WHEN 'Gaisano Grand'   THEN 'MT/NKA, West Mindanao'
                    WHEN 'Gaisano Capital' THEN 'MT/NKA'
                    WHEN 'Gaisano Mall'    THEN 'MT/NKA'
                    ELSE AreaNames END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AreaNames",
                table: "TasafChains");
        }
    }
}
