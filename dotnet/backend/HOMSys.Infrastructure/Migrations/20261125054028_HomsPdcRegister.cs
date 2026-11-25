using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HomsPdcRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RegisterJson",
                table: "HomsPostings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");   // existing postings: no register (the catch-up queues Register postings for them)
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegisterJson",
                table: "HomsPostings");
        }
    }
}
