using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafTparBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BranchName",
                table: "TasafTpars",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SiteId",
                table: "TasafTpars",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasafTpars_SiteId",
                table: "TasafTpars",
                column: "SiteId");

            migrationBuilder.AddForeignKey(
                name: "FK_TasafTpars_Sites_SiteId",
                table: "TasafTpars",
                column: "SiteId",
                principalTable: "Sites",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TasafTpars_Sites_SiteId",
                table: "TasafTpars");

            migrationBuilder.DropIndex(
                name: "IX_TasafTpars_SiteId",
                table: "TasafTpars");

            migrationBuilder.DropColumn(
                name: "BranchName",
                table: "TasafTpars");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "TasafTpars");
        }
    }
}
