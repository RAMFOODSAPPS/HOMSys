using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteTypeFkToSite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SiteTypeId",
                table: "Sites",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sites_SiteTypeId",
                table: "Sites",
                column: "SiteTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sites_SiteTypes_SiteTypeId",
                table: "Sites",
                column: "SiteTypeId",
                principalTable: "SiteTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sites_SiteTypes_SiteTypeId",
                table: "Sites");

            migrationBuilder.DropIndex(
                name: "IX_Sites_SiteTypeId",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "SiteTypeId",
                table: "Sites");
        }
    }
}
