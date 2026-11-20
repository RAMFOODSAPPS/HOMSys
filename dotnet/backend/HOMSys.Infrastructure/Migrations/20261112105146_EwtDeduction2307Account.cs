using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EwtDeduction2307Account : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AcctNo",
                table: "CollectionDeductions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "With2307",
                table: "CollectionDeductions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // EWTs saved before "2307 att." existed were all posted to HOMS as 10507 (PREPAID CEWIT - WITH CERTIFICATE).
            migrationBuilder.Sql("UPDATE CollectionDeductions SET With2307 = 1, AcctNo = 10507 WHERE [Type] = 'Ewt2307';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcctNo",
                table: "CollectionDeductions");

            migrationBuilder.DropColumn(
                name: "With2307",
                table: "CollectionDeductions");
        }
    }
}
