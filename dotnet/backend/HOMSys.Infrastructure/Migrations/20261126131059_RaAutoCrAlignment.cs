using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RaAutoCrAlignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Ttc",
                table: "RemittanceLines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DealCode",
                table: "CollectionDeductions",
                type: "int",
                nullable: true);

            // Every deduction saved before keeps its EWT line on the OR (only an RA CEWIT without "With TTC" is off it).
            migrationBuilder.AddColumn<bool>(
                name: "OnOr",
                table: "CollectionDeductions",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ttc",
                table: "RemittanceLines");

            migrationBuilder.DropColumn(
                name: "DealCode",
                table: "CollectionDeductions");

            migrationBuilder.DropColumn(
                name: "OnOr",
                table: "CollectionDeductions");
        }
    }
}
