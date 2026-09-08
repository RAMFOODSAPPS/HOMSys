using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHondurasSite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // HONDURAS is the base "hon" pricing folder itself, not one of its carved-out
            // sub-branches, so it has no cuwhsenos entry — it sees hon's full unfiltered
            // zone list. Without this row it had no Site at all and dropped out of the
            // Pricelist-by-Zone branch picker once that picker started sourcing from Sites.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM Sites WHERE Code = 'HON-B')
                INSERT INTO Sites (Name, Code, Address, Phone, ContactPerson, Description, Cuwhsenos, IsActive, CreatedAt, CreatedBy, CompanyId, SiteTypeId)
                VALUES ('HONDURAS', 'HON-B', '', '', '', '', '', 1, GETUTCDATE(), 'system', 1, 1);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM Sites WHERE Code = 'HON-B';");
        }
    }
}
