using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCuwhsenosToSite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cuwhsenos",
                table: "Sites",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            // C:\dump\branch.xlsx "cuwhsenos" column, per branch (see ZonePricelistExportService.HonBranchWhseNos).
            migrationBuilder.Sql("UPDATE Sites SET Cuwhsenos = '12,28,24,21,26,25,45,23,87' WHERE Code = 'CDC-B';");
            migrationBuilder.Sql("UPDATE Sites SET Cuwhsenos = '19,79' WHERE Code = 'LUC-B';");
            migrationBuilder.Sql(@"
                INSERT INTO Sites (Name, Code, Address, Phone, ContactPerson, Description, Cuwhsenos, IsActive, CreatedAt, CreatedBy, CompanyId, SiteTypeId)
                VALUES
                ('MEXICO',  'MEX-B', '', '', '', '', '10',        1, GETUTCDATE(), 'system', 1, 1),
                ('DAGUPAN', 'DAG-B', '', '', '', '', '21,26,45,86', 1, GETUTCDATE(), 'system', 1, 1),
                ('NAGA',    'NAG-B', '', '', '', '', '23,45,94',  1, GETUTCDATE(), 'system', 1, 1),
                ('ISABELA', 'ISA-B', '', '', '', '', '26,96',     1, GETUTCDATE(), 'system', 1, 1),
                ('LEGAZPI', 'LEG-B', '', '', '', '', '16,45,36',  1, GETUTCDATE(), 'system', 1, 1);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cuwhsenos",
                table: "Sites");
        }
    }
}
