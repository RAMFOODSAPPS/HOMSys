using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <summary>
    /// Corrects the TASAF area branch defaults (migration TasafAreaBranches) with the customer
    /// master's own regions (cust4win NREGION, mapped to Sites through WhseNo, 2026-10-14):
    /// North Luzon customers include CDC; Mindanao splits into Central (BUT, CDO, DAV, GEN, KID,
    /// PAN, TGM) and West (OZA, ZAM). VAL has no regioned customers, so it stays in North Mindanao.
    /// Only rows still holding the old default are changed, so edits made in TASAF Setup › Areas survive.
    /// </summary>
    public partial class TasafAreaBranchesFromCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE a SET [BranchCodes] = v.[New]
                FROM [TasafAreas] a
                JOIN (VALUES
                    ('North Central Luzon', 'DAG-B, ISA-B, MEX-B',               'DAG-B, ISA-B, MEX-B, CDC-B'),
                    ('North Mindanao',      'CDO-B, OZA-B, BUT-B, VAL-B, ZAM-B', 'OZA-B, ZAM-B, VAL-B'),
                    ('South Mindanao',      'DAV-B, GEN-B, KID-B, PAN-B, TGM-B', 'BUT-B, CDO-B, DAV-B, GEN-B, KID-B, PAN-B, TGM-B')
                ) v([Area], [Old], [New]) ON a.[Name] = v.[Area]
                WHERE a.[BranchCodes] = v.[Old];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE a SET [BranchCodes] = v.[Old]
                FROM [TasafAreas] a
                JOIN (VALUES
                    ('North Central Luzon', 'DAG-B, ISA-B, MEX-B',               'DAG-B, ISA-B, MEX-B, CDC-B'),
                    ('North Mindanao',      'CDO-B, OZA-B, BUT-B, VAL-B, ZAM-B', 'OZA-B, ZAM-B, VAL-B'),
                    ('South Mindanao',      'DAV-B, GEN-B, KID-B, PAN-B, TGM-B', 'BUT-B, CDO-B, DAV-B, GEN-B, KID-B, PAN-B, TGM-B')
                ) v([Area], [Old], [New]) ON a.[Name] = v.[Area]
                WHERE a.[BranchCodes] = v.[New];
                """);
        }
    }
}
