using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DepositSlipRefOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DepositBatches_SiteId_Bank_SlipRefNo",
                table: "DepositBatches");

            migrationBuilder.CreateIndex(
                name: "IX_DepositBatches_SiteId_Bank_SlipRefNo",
                table: "DepositBatches",
                columns: new[] { "SiteId", "Bank", "SlipRefNo" },
                unique: true,
                filter: "[SlipRefNo] <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DepositBatches_SiteId_Bank_SlipRefNo",
                table: "DepositBatches");

            migrationBuilder.CreateIndex(
                name: "IX_DepositBatches_SiteId_Bank_SlipRefNo",
                table: "DepositBatches",
                columns: new[] { "SiteId", "Bank", "SlipRefNo" },
                unique: true);
        }
    }
}
