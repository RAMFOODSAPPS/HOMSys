using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <summary>
    /// Replaces the TASAF area list with the customer master's own regions (cust4win NREGION:
    /// GMA → NCR, NKA → MT/NKA, 2026-10-19). The old workbook areas are deactivated, not deleted
    /// or renamed: saved TASAFs (some Approved) still point at them and keep their area names.
    /// South Luzon is in both lists and keeps its row (it now includes the Bicol branches).
    /// MT/NKA has no branch codes (= every branch). Edit on TASAF Setup › Areas.
    /// </summary>
    public partial class TasafAreasByRegion : Migration
    {
        private const string OldAreas = """
            (1, 'GMA / NCR'), (2, 'North Central Luzon'), (3, 'South Luzon'), (4, 'Bicol'), (5, 'East Visayas'),
            (6, 'West Visayas'), (7, 'North Mindanao'), (8, 'South Mindanao'), (9, 'Total Luzon'),
            (10, 'Total Visayas'), (11, 'Total Mindanao'), (12, 'Total VisMin'), (13, 'Nationwide')
            """;

        private const string NewAreas = """
            (1, 'NCR',              'HON-B'),
            (2, 'North Luzon',      'DAG-B, ISA-B, MEX-B, CDC-B'),
            (3, 'South Luzon',      'LUC-B, LEG-B, NAG-B'),
            (4, 'Visayas',          'CEB-B, TAC-B, DUM-B, ILO-B, BAC-B, ROX-B'),
            (5, 'West Mindanao',    'OZA-B, ZAM-B, VAL-B'),
            (6, 'Central Mindanao', 'BUT-B, CDO-B, DAV-B, GEN-B, KID-B, PAN-B, TGM-B'),
            (7, 'MT/NKA',           '')
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Names are unique, so a new area whose name already exists (South Luzon) reuses
            // that row: it stays active and takes the new branches.
            migrationBuilder.Sql($"""
                UPDATE a SET [IsActive] = 0, [UpdatedAt] = SYSUTCDATETIME(), [UpdatedBy] = 'migration'
                FROM [TasafAreas] a
                JOIN (VALUES {OldAreas}) v([Sort], [Name]) ON a.[Name] = v.[Name]
                WHERE a.[IsActive] = 1;

                UPDATE a SET [IsActive] = 1, [SortOrder] = v.[Sort], [BranchCodes] = v.[Codes],
                             [UpdatedAt] = SYSUTCDATETIME(), [UpdatedBy] = 'migration'
                FROM [TasafAreas] a
                JOIN (VALUES {NewAreas}) v([Sort], [Name], [Codes]) ON a.[Name] = v.[Name];

                INSERT INTO [TasafAreas] ([Code], [Name], [Description], [SortOrder], [IsActive], [CreatedAt], [CreatedBy], [BranchCodes])
                SELECT '', v.[Name], '', v.[Sort], 1, SYSUTCDATETIME(), 'migration', v.[Codes]
                FROM (VALUES {NewAreas}) v([Sort], [Name], [Codes])
                WHERE NOT EXISTS (SELECT 1 FROM [TasafAreas] a WHERE a.[Name] = v.[Name]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Inserted areas already used by a TASAF are only deactivated (FK Restrict).
            migrationBuilder.Sql($"""
                DELETE a FROM [TasafAreas] a
                JOIN (VALUES {NewAreas}) v([Sort], [Name], [Codes]) ON a.[Name] = v.[Name]
                WHERE a.[CreatedBy] = 'migration'
                  AND NOT EXISTS (SELECT 1 FROM [TasafAreaLinks] l WHERE l.[AreaId] = a.[Id]);

                UPDATE a SET [IsActive] = 0
                FROM [TasafAreas] a
                JOIN (VALUES {NewAreas}) v([Sort], [Name], [Codes]) ON a.[Name] = v.[Name]
                WHERE a.[CreatedBy] = 'migration';

                UPDATE a SET [IsActive] = 1, [SortOrder] = v.[Sort]
                FROM [TasafAreas] a
                JOIN (VALUES {OldAreas}) v([Sort], [Name]) ON a.[Name] = v.[Name]
                WHERE a.[CreatedBy] = 'seed';

                UPDATE [TasafAreas] SET [BranchCodes] = 'LUC-B'
                WHERE [Name] = 'South Luzon' AND [CreatedBy] = 'seed' AND [BranchCodes] = 'LUC-B, LEG-B, NAG-B';
                """);
        }
    }
}
