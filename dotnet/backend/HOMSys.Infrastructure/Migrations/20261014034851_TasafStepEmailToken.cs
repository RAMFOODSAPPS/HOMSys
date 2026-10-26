using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafStepEmailToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EmailTokenExpires",
                table: "TasafApprovalSteps",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailTokenHash",
                table: "TasafApprovalSteps",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasafApprovalSteps_EmailTokenHash",
                table: "TasafApprovalSteps",
                column: "EmailTokenHash",
                filter: "[EmailTokenHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TasafApprovalSteps_EmailTokenHash",
                table: "TasafApprovalSteps");

            migrationBuilder.DropColumn(
                name: "EmailTokenExpires",
                table: "TasafApprovalSteps");

            migrationBuilder.DropColumn(
                name: "EmailTokenHash",
                table: "TasafApprovalSteps");
        }
    }
}
