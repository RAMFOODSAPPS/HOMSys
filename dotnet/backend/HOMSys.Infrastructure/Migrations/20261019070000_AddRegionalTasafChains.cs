using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <summary>
    /// Regional LKA / RDP customer chains for TASAF (2026-10-19), after the existing chains.
    /// LKA → LKA sales group, RDP → GT (same as "LKA Others" / "RDP Others"). Skipped when the
    /// name already exists, so nothing is overwritten. No customer keywords (= free search).
    /// </summary>
    public partial class AddRegionalTasafChains : Migration
    {
        private const string Chains = """
            (1,  'LKA GMA',          'LKA'),
            (2,  'LKA North Luzon',  'LKA'),
            (3,  'LKA South Luzon',  'LKA'),
            (4,  'LKA Visayas',      'LKA'),
            (5,  'LKA Mindanao',     'LKA'),
            (6,  'RDP GMA',          'GT'),
            (7,  'RDP North Luzon',  'GT'),
            (8,  'RDP South Luzon',  'GT'),
            (9,  'RDP Visayas',      'GT'),
            (10, 'RDP Mindanao',     'GT')
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DECLARE @base int = (SELECT ISNULL(MAX([SortOrder]), 0) FROM [TasafChains]);
                INSERT INTO [TasafChains] ([Code], [Name], [Description], [SortOrder], [IsActive], [CreatedAt], [CreatedBy], [SalesGroupId], [CustomerMatch])
                SELECT '', v.[Name], '', @base + v.[Sort], 1, GETUTCDATE(), 'migration', g.[Id], ''
                FROM (VALUES {Chains}) v([Sort], [Name], [Group])
                LEFT JOIN [TasafSalesGroups] g ON g.[Code] = v.[Group]
                WHERE NOT EXISTS (SELECT 1 FROM [TasafChains] c WHERE c.[Name] = v.[Name]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Chains already used by a TASAF are kept (FK Restrict).
            migrationBuilder.Sql($"""
                DELETE c FROM [TasafChains] c
                JOIN (VALUES {Chains}) v([Sort], [Name], [Group]) ON c.[Name] = v.[Name]
                WHERE c.[CreatedBy] = 'migration'
                  AND NOT EXISTS (SELECT 1 FROM [Tasafs] t WHERE t.[ChainId] = c.[Id]);
                """);
        }
    }
}
