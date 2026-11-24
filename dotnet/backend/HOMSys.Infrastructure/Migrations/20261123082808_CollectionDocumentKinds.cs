using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CollectionDocumentKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollectionDocumentKinds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ReceiptTypes = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionDocumentKinds", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "CollectionDocumentKinds",
                columns: new[] { "Id", "Code", "IsActive", "IsSystem", "Label", "ReceiptTypes", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "CollectionReceipt", true, false, "Collection Receipt (CR)", "CR", 10, null, null },
                    { 2, "ProvisionalReceipt", true, false, "Provisional Receipt (PR)", "PR", 20, null, null },
                    { 3, "AcknowledgementReceipt", true, false, "Acknowledgement Receipt (AR)", "AR", 30, null, null },
                    { 4, "Dcr", true, false, "DCR (Daily Collection Report)", "", 40, null, null },
                    { 5, "DepositSlip", true, false, "Deposit Slip", "", 50, null, null },
                    { 6, "CustomerSlip", true, false, "Customer Slip", "", 60, null, null },
                    { 7, "ServiceInvoice", true, true, "Service Invoice", "", 70, null, null },
                    { 8, "Form2307", true, false, "2307", "", 80, null, null },
                    { 9, "Check", true, false, "Check", "", 90, null, null },
                    { 10, "RemittanceAdvice", true, false, "Remittance Advice", "", 100, null, null },
                    { 11, "Other", true, true, "Other", "", 990, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionDocumentKinds_Code",
                table: "CollectionDocumentKinds",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollectionDocumentKinds");
        }
    }
}
