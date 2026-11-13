using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ArofInvoiceSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "HomsBalance",
                table: "PostedInvoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostedRfcs_InvoiceNo",
                table: "PostedRfcs",
                column: "InvoiceNo");

            migrationBuilder.CreateIndex(
                name: "IX_PostedInvoices_DocNo",
                table: "PostedInvoices",
                column: "DocNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PostedRfcs_InvoiceNo",
                table: "PostedRfcs");

            migrationBuilder.DropIndex(
                name: "IX_PostedInvoices_DocNo",
                table: "PostedInvoices");

            migrationBuilder.DropColumn(
                name: "HomsBalance",
                table: "PostedInvoices");
        }
    }
}
