using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafApprovalTypesSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAccountSupport",
                table: "TasafProgramTypes");

            migrationBuilder.DropColumn(
                name: "AlwaysForAccountSupport",
                table: "TasafApprovalRoles");

            migrationBuilder.DropColumn(
                name: "MinAmount",
                table: "TasafApprovalRoles");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalTypeName",
                table: "Tasafs",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ApprovalTypeId",
                table: "TasafProgramTypes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TasafApprovalTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafApprovalTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TasafApprovalRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApprovalTypeId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    OverAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafApprovalRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafApprovalRules_TasafApprovalRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "TasafApprovalRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TasafApprovalRules_TasafApprovalTypes_ApprovalTypeId",
                        column: x => x.ApprovalTypeId,
                        principalTable: "TasafApprovalTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "TasafApprovalTypes",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "Description", "IsActive", "Name", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "PROMO", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "By amount. Include CTS as a metric; promo packs must be within 8%.", true, "Promo / Near Ex Moveout", 1, null, null },
                    { 2, "SUPPORT", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Must go to NSD and COO regardless of amount.", true, "Listing Fees, Conventions, Other Account Support", 2, null, null }
                });

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 1,
                column: "ApprovalTypeId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 2,
                column: "ApprovalTypeId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 3,
                column: "ApprovalTypeId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 4,
                column: "ApprovalTypeId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 5,
                column: "ApprovalTypeId",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 6,
                column: "ApprovalTypeId",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 7,
                column: "ApprovalTypeId",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 8,
                column: "ApprovalTypeId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 9,
                column: "ApprovalTypeId",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 10,
                column: "ApprovalTypeId",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 11,
                column: "ApprovalTypeId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 12,
                column: "ApprovalTypeId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 13,
                column: "ApprovalTypeId",
                value: 1);

            migrationBuilder.InsertData(
                table: "TasafApprovalRules",
                columns: new[] { "Id", "ApprovalTypeId", "OverAmount", "RoleId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, 1, null, 1, null, null },
                    { 2, 1, null, 2, null, null },
                    { 3, 1, 20000m, 3, null, null },
                    { 4, 1, 50000m, 4, null, null },
                    { 5, 1, 100000m, 5, null, null },
                    { 6, 2, null, 1, null, null },
                    { 7, 2, null, 2, null, null },
                    { 8, 2, 20000m, 3, null, null },
                    { 9, 2, null, 4, null, null },
                    { 10, 2, null, 5, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_TasafProgramTypes_ApprovalTypeId",
                table: "TasafProgramTypes",
                column: "ApprovalTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafApprovalRules_ApprovalTypeId_RoleId",
                table: "TasafApprovalRules",
                columns: new[] { "ApprovalTypeId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasafApprovalRules_RoleId",
                table: "TasafApprovalRules",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafApprovalTypes_Code",
                table: "TasafApprovalTypes",
                column: "Code",
                unique: true,
                filter: "[Code] != ''");

            migrationBuilder.CreateIndex(
                name: "IX_TasafApprovalTypes_Name",
                table: "TasafApprovalTypes",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TasafProgramTypes_TasafApprovalTypes_ApprovalTypeId",
                table: "TasafProgramTypes",
                column: "ApprovalTypeId",
                principalTable: "TasafApprovalTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TasafProgramTypes_TasafApprovalTypes_ApprovalTypeId",
                table: "TasafProgramTypes");

            migrationBuilder.DropTable(
                name: "TasafApprovalRules");

            migrationBuilder.DropTable(
                name: "TasafApprovalTypes");

            migrationBuilder.DropIndex(
                name: "IX_TasafProgramTypes_ApprovalTypeId",
                table: "TasafProgramTypes");

            migrationBuilder.DropColumn(
                name: "ApprovalTypeName",
                table: "Tasafs");

            migrationBuilder.DropColumn(
                name: "ApprovalTypeId",
                table: "TasafProgramTypes");

            migrationBuilder.AddColumn<bool>(
                name: "IsAccountSupport",
                table: "TasafProgramTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AlwaysForAccountSupport",
                table: "TasafApprovalRoles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MinAmount",
                table: "TasafApprovalRoles",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "TasafApprovalRoles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AlwaysForAccountSupport", "MinAmount" },
                values: new object[] { false, 0m });

            migrationBuilder.UpdateData(
                table: "TasafApprovalRoles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "AlwaysForAccountSupport", "MinAmount" },
                values: new object[] { false, 0m });

            migrationBuilder.UpdateData(
                table: "TasafApprovalRoles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "AlwaysForAccountSupport", "MinAmount" },
                values: new object[] { false, 20000m });

            migrationBuilder.UpdateData(
                table: "TasafApprovalRoles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "AlwaysForAccountSupport", "MinAmount" },
                values: new object[] { true, 50000m });

            migrationBuilder.UpdateData(
                table: "TasafApprovalRoles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "AlwaysForAccountSupport", "MinAmount" },
                values: new object[] { true, 100000m });

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsAccountSupport",
                value: false);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsAccountSupport",
                value: false);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 3,
                column: "IsAccountSupport",
                value: false);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 4,
                column: "IsAccountSupport",
                value: false);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 5,
                column: "IsAccountSupport",
                value: true);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 6,
                column: "IsAccountSupport",
                value: true);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 7,
                column: "IsAccountSupport",
                value: true);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 8,
                column: "IsAccountSupport",
                value: false);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 9,
                column: "IsAccountSupport",
                value: true);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 10,
                column: "IsAccountSupport",
                value: true);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 11,
                column: "IsAccountSupport",
                value: false);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 12,
                column: "IsAccountSupport",
                value: false);

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 13,
                column: "IsAccountSupport",
                value: false);
        }
    }
}
