using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <summary>
    /// New TASAF status "Closed" = Approved + TPAR No. entered (user decision 2026-11-20). Data only:
    /// Approved TASAFs that already carry a TPAR No. become Closed. Down puts them back to Approved.
    /// </summary>
    public partial class TasafClosedByTparNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Tasafs SET Status = 'Closed'
                WHERE Status = 'Approved' AND TparNo IS NOT NULL AND LTRIM(RTRIM(TparNo)) <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Tasafs SET Status = 'Approved' WHERE Status = 'Closed';");
        }
    }
}
