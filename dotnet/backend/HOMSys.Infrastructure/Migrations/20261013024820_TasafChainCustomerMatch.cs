using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafChainCustomerMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerMatch",
                table: "TasafChains",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 1,
                column: "CustomerMatch",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 2,
                column: "CustomerMatch",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 3,
                column: "CustomerMatch",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 4,
                column: "CustomerMatch",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 5,
                column: "CustomerMatch",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 6,
                column: "CustomerMatch",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 7,
                column: "CustomerMatch",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 8,
                column: "CustomerMatch",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 9,
                column: "CustomerMatch",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 10,
                column: "CustomerMatch",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 11,
                column: "CustomerMatch",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 12,
                column: "CustomerMatch",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 13,
                column: "CustomerMatch",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 14,
                column: "CustomerMatch",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 15,
                column: "CustomerMatch",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 16,
                column: "CustomerMatch",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 17,
                column: "CustomerMatch",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 18,
                column: "CustomerMatch",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 19,
                column: "CustomerMatch",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 20,
                column: "CustomerMatch",
                value: "");

            migrationBuilder.UpdateData(
                table: "TasafChains",
                keyColumn: "Id",
                keyValue: 21,
                column: "CustomerMatch",
                value: "");

            // Default customer-name keywords per chain (checked against the live cust4win names
            // 2026-10-13). Editable in TASAF Setup › Customers / Chains; "Others" = no filter.
            migrationBuilder.Sql("""
                UPDATE c SET CustomerMatch = v.Match
                FROM TasafChains c
                JOIN (VALUES
                    ('SVI',             'SUPERVALUE, SAVEMORE, SM CITY, SM HYPERMARKET, SM SUPERMARKET'),
                    ('Waltermart',      'WALTERMART, WALTER MART'),
                    ('Puregold',        'PUREGOLD'),
                    ('Landmark',        'LANDMARK'),
                    ('Robinson',        'ROBINSON'),
                    ('Suysing',         'SUYSING'),
                    ('Super 8',         'SUPER 8, SUPER8'),
                    ('CSI',             'CSI%WAREHOUSE, CSI SUPERMARKET'),
                    ('RCS Chain',       'RCS'),
                    ('Savewise',        'SAVEWISE'),
                    ('LCC',             'LCC'),
                    ('Citi Mart',       'CITIMART, CITI MART'),
                    ('Prince',          'PRINCE -, PRINCE TRADING, PRINCE HYPERMART, PRINCE WAREHOUSE'),
                    ('Metro Gaisano',   'METRO GAISANO, METRO SUPERMARKET, METRO SUPER MARKET, METRO HYPERMARKET, METRO RETAIL'),
                    ('NCCC',            'NCCC'),
                    ('KCC',             'KCC'),
                    ('Gaisano Grand',   'GAISANO GRAND'),
                    ('Gaisano Capital', 'GAISANO CAPITAL'),
                    ('Gaisano Mall',    'GAISANO MALL')
                ) AS v(Name, Match) ON v.Name = c.Name
                WHERE c.CustomerMatch = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerMatch",
                table: "TasafChains");
        }
    }
}
