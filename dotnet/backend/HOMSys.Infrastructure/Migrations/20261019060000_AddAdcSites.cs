using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <summary>
    /// The four ADC sites (2026-10-19). Skipped when a site with the same name or code already
    /// exists, so nothing is overwritten. No Cuwhsenos yet (fill in on the Sites page).
    /// </summary>
    public partial class AddAdcSites : Migration
    {
        private const string Sites = """
            ('ADC - North Luzon',  'NL-ADC'),
            ('ADC - South Luzon',  'SL-ADC'),
            ('ADC - Visayas',      'VIS-ADC'),
            ('ADC - Office Sales', 'OS-ADC')
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                INSERT INTO [Sites] ([Name], [Code], [Address], [Phone], [ContactPerson], [Description], [Cuwhsenos],
                                     [PricesOffHon], [AcceptsOffshoreOrders], [IsActive], [CreatedAt], [CreatedBy], [CompanyId], [SiteTypeId])
                SELECT v.[Name], v.[Code], '', '', '', '', '', 0, 0, 1, GETUTCDATE(), 'system', 1, 1
                FROM (VALUES {Sites}) v([Name], [Code])
                WHERE NOT EXISTS (SELECT 1 FROM [Sites] s WHERE s.[CompanyId] = 1 AND (s.[Name] = v.[Name] OR s.[Code] = v.[Code]));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DELETE s FROM [Sites] s
                JOIN (VALUES {Sites}) v([Name], [Code]) ON s.[Code] = v.[Code] AND s.[Name] = v.[Name]
                WHERE s.[CreatedBy] = 'system';
                """);
        }
    }
}
