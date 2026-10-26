using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <summary>
    /// Only the agreed TASAF areas and customer chains stay active (2026-10-19); every other
    /// active one is deactivated (never deleted — saved TASAFs keep their links). Names are
    /// matched case-insensitively, with the trade list's "- Main" names and HOMSys's own
    /// names for the same chains (SVI = SM Group, Robinson = RRHI). Reactivate on TASAF Setup.
    /// </summary>
    public partial class TasafDeactivateUnlisted : Migration
    {
        private const string Areas = """
            ('NCR'), ('North Luzon'), ('South Luzon'), ('Visayas'), ('West Mindanao'),
            ('Central Mindanao'), ('MT/NKA')
            """;

        private const string Chains = """
            ('SVI'), ('SVI - Main'), ('SM Group'), ('Puregold'), ('Puregold - Main'),
            ('Robinson'), ('Robinson - Main'), ('RRHI'), ('Waltermart'), ('Waltermart - Main'),
            ('Landmark'), ('Landmark - Main'), ('Suysing'), ('Suysing - Main'), ('Super 8'), ('Super 8 - Main'),
            ('Citi Mart'), ('LCC'), ('LCC - Main'), ('CSI'), ('CSI - Main'), ('RCS Chain'), ('Savewise'),
            ('Prince'), ('Prince - Main'), ('Metro Gaisano'), ('Metro Gaisano - Main'),
            ('NCCC'), ('NCCC - Main'), ('KCC'), ('KCC - Main'),
            ('Gaisano Grand'), ('Gaisano Capital'), ('Gaisano Mall'),
            ('LKA GMA'), ('LKA North Luzon'), ('LKA South Luzon'), ('LKA Visayas'), ('LKA Mindanao'),
            ('RDP GMA'), ('RDP North Luzon'), ('RDP South Luzon'), ('RDP Visayas'), ('RDP Mindanao')
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE [TasafAreas] SET [IsActive] = 0, [UpdatedAt] = GETUTCDATE(), [UpdatedBy] = 'migration-unlisted'
                WHERE [IsActive] = 1 AND LTRIM(RTRIM([Name])) NOT IN (SELECT n FROM (VALUES {Areas}) v(n));

                UPDATE [TasafChains] SET [IsActive] = 0, [UpdatedAt] = GETUTCDATE(), [UpdatedBy] = 'migration-unlisted'
                WHERE [IsActive] = 1 AND LTRIM(RTRIM([Name])) NOT IN (SELECT n FROM (VALUES {Chains}) v(n));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [TasafAreas]  SET [IsActive] = 1 WHERE [UpdatedBy] = 'migration-unlisted';
                UPDATE [TasafChains] SET [IsActive] = 1 WHERE [UpdatedBy] = 'migration-unlisted';
                """);
        }
    }
}
