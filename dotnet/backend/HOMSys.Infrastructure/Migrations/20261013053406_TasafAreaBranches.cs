using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <summary>
    /// Branch (Site) codes per TASAF area — picks the area's stores for the Cust Key dropdown.
    /// Defaults below are editable on TASAF Setup › Areas; only blank areas are filled.
    /// Nationwide stays blank (= every customer).
    /// </summary>
    public partial class TasafAreaBranches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BranchCodes",
                table: "TasafAreas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE a SET [BranchCodes] = v.[Codes]
                FROM [TasafAreas] a
                JOIN (VALUES
                    ('GMA / NCR',           'HON-B'),
                    ('North Central Luzon', 'DAG-B, ISA-B, MEX-B'),
                    ('South Luzon',         'LUC-B'),
                    ('Bicol',               'LEG-B, NAG-B'),
                    ('East Visayas',        'CEB-B, TAC-B, DUM-B'),
                    ('West Visayas',        'ILO-B, BAC-B, ROX-B'),
                    ('North Mindanao',      'CDO-B, OZA-B, BUT-B, VAL-B, ZAM-B'),
                    ('South Mindanao',      'DAV-B, GEN-B, KID-B, PAN-B, TGM-B'),
                    ('Total Luzon',         'HON-B, CDC-B, DAG-B, ISA-B, MEX-B, LUC-B, LEG-B, NAG-B'),
                    ('Total Visayas',       'CEB-B, TAC-B, DUM-B, ILO-B, BAC-B, ROX-B'),
                    ('Total Mindanao',      'CDO-B, OZA-B, BUT-B, VAL-B, ZAM-B, DAV-B, GEN-B, KID-B, PAN-B, TGM-B'),
                    ('Total VisMin',        'CEB-B, TAC-B, DUM-B, ILO-B, BAC-B, ROX-B, CDO-B, OZA-B, BUT-B, VAL-B, ZAM-B, DAV-B, GEN-B, KID-B, PAN-B, TGM-B')
                ) v([Area], [Codes]) ON a.[Name] = v.[Area]
                WHERE a.[BranchCodes] = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BranchCodes",
                table: "TasafAreas");
        }
    }
}