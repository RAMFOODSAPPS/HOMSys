using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPricesOffHonAndVisminSites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PricesOffHon",
                table: "Sites",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // The 7 sub-branches already seeded (AddCuwhsenosToSite) plus HONDURAS
            // (AddHondurasSite) share hon's ZONE/ZONE2 tables directly ("in HON" = TRUE
            // on the branch/whseno/cuwhsenos master sheet) — mark them so
            // ZonePricelistExportService.GetHonBranchesAsync() keeps resolving them to
            // the "hon" pricing folder now that non-HON branches also get Site rows.
            migrationBuilder.Sql(@"
                UPDATE Sites SET PricesOffHon = 1
                WHERE Code IN ('CDC-B','MEX-B','DAG-B','NAG-B','ISA-B','LUC-B','LEG-B','HON-B');
            ");

            // Vismin branches (in HON = blank on the master sheet) — each has its own
            // F:\AUTOPROG\ADDON\{branch} folder, so PricesOffHon stays false (default).
            // Cuwhsenos is carried over from the same master sheet for reference even
            // though the Pricelist-by-Zone feature doesn't need it for these branches.
            migrationBuilder.Sql(@"
                INSERT INTO Sites (Name, Code, Address, Phone, ContactPerson, Description, Cuwhsenos, PricesOffHon, IsActive, CreatedAt, CreatedBy, CompanyId, SiteTypeId)
                SELECT v.Name, v.Code, '', '', '', '', v.Cuwhsenos, 0, 1, GETUTCDATE(), 'system', 1, 1
                FROM (VALUES
                    ('CEBU',               'CEB-B', '41,81'),
                    ('ILOILO',             'ILO-B', '42,82'),
                    ('BACOLOD',            'BAC-B', '43,83'),
                    ('TACLOBAN',           'TAC-B', '44,84'),
                    ('CAGAYAN DE ORO',     'CDO-B', '61,65,91'),
                    ('DAVAO',              'DAV-B', '62,92'),
                    ('ZAMBOANGA',          'ZAM-B', '63,93'),
                    ('DUMAGUETE',          'DUM-B', '67,99'),
                    ('GEN. SANTOS',        'GEN-B', '64,97'),
                    ('BUTUAN',             'BUT-B', '69,73'),
                    ('KIDAPAWAN',          'KID-B', '54,89'),
                    ('ROXAS',              'ROX-B', '57,37'),
                    ('TAGUM',              'TGM-B', '48,33'),
                    ('OZAMIZ',             'OZA-B', '65,38'),
                    ('PANABO',             'PAN-B', '55,32'),
                    ('VALENCIA',           'VAL-B', '58,18'),
                    ('SAN VICENTE SALES',  'SVS-B', '46')
                ) AS v(Name, Code, Cuwhsenos)
                WHERE NOT EXISTS (SELECT 1 FROM Sites s WHERE s.Code = v.Code);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM Sites WHERE Code IN
                ('CEB-B','ILO-B','BAC-B','TAC-B','CDO-B','DAV-B','ZAM-B','DUM-B',
                 'GEN-B','BUT-B','KID-B','ROX-B','TGM-B','OZA-B','PAN-B','VAL-B','SVS-B');
            ");

            migrationBuilder.DropColumn(
                name: "PricesOffHon",
                table: "Sites");
        }
    }
}
