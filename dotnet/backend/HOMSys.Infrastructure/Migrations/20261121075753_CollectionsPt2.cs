using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CollectionsPt2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeductionIds",
                table: "HomsPostings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeductionsJson",
                table: "HomsPostings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "AttachmentId",
                table: "CollectionDeductions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "CollectionDeductions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HomsSentAt",
                table: "CollectionDeductions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "CollectionDeductions",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "CollectionAttachments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Unspecified");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "CollectionAttachments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionDeductions_AttachmentId",
                table: "CollectionDeductions",
                column: "AttachmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_CollectionDeductions_CollectionAttachments_AttachmentId",
                table: "CollectionDeductions",
                column: "AttachmentId",
                principalTable: "CollectionAttachments",
                principalColumn: "Id");

            // Deductions saved before finance's selection list: the default selection of their type.
            migrationBuilder.Sql(@"UPDATE CollectionDeductions SET Reason = CASE Type
                WHEN 'Ewt2307' THEN 'EWT' WHEN 'Discount' THEN 'UNREFLECTED_DISCOUNT' WHEN 'Penalty' THEN 'CUSTOMER_PENALTY'
                WHEN 'RfcBoRtv' THEN 'RETURN_CUSTOMER' WHEN 'DisplayAllowance' THEN 'DISPLAY_RENTAL'
                WHEN 'ServiceInvoice' THEN 'SERVICE_INVOICE' ELSE 'OTHERS' END
                WHERE Reason IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CollectionDeductions_CollectionAttachments_AttachmentId",
                table: "CollectionDeductions");

            migrationBuilder.DropIndex(
                name: "IX_CollectionDeductions_AttachmentId",
                table: "CollectionDeductions");

            migrationBuilder.DropColumn(
                name: "DeductionIds",
                table: "HomsPostings");

            migrationBuilder.DropColumn(
                name: "DeductionsJson",
                table: "HomsPostings");

            migrationBuilder.DropColumn(
                name: "AttachmentId",
                table: "CollectionDeductions");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "CollectionDeductions");

            migrationBuilder.DropColumn(
                name: "HomsSentAt",
                table: "CollectionDeductions");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "CollectionDeductions");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "CollectionAttachments");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "CollectionAttachments");
        }
    }
}
