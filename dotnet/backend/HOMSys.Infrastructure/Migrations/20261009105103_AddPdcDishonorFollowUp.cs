using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPdcDishonorFollowUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LegalEndorsedBy",
                table: "Collections",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LegalEndorsedDate",
                table: "Collections",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalRemarks",
                table: "Collections",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LodCopyAttachmentId",
                table: "Collections",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LodDueDate",
                table: "Collections",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LodIssuedBy",
                table: "Collections",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LodIssuedDate",
                table: "Collections",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LodReceivedBy",
                table: "Collections",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LodReceivedDate",
                table: "Collections",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LodSettleDays",
                table: "Collections",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SettledAt",
                table: "Collections",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettledBy",
                table: "Collections",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SettledByCollectionId",
                table: "Collections",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Collections_SettledByCollectionId",
                table: "Collections",
                column: "SettledByCollectionId",
                unique: true,
                filter: "[SettledByCollectionId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Collections_Collections_SettledByCollectionId",
                table: "Collections",
                column: "SettledByCollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Collections_Collections_SettledByCollectionId",
                table: "Collections");

            migrationBuilder.DropIndex(
                name: "IX_Collections_SettledByCollectionId",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "LegalEndorsedBy",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "LegalEndorsedDate",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "LegalRemarks",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "LodCopyAttachmentId",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "LodDueDate",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "LodIssuedBy",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "LodIssuedDate",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "LodReceivedBy",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "LodReceivedDate",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "LodSettleDays",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "SettledAt",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "SettledBy",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "SettledByCollectionId",
                table: "Collections");
        }
    }
}
