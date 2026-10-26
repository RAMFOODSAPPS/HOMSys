using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafApprovalMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TasafSalesGroups_Users_GkamUserId",
                table: "TasafSalesGroups");

            migrationBuilder.DropTable(
                name: "TasafApprovalLevels");

            migrationBuilder.DropIndex(
                name: "IX_TasafSalesGroups_GkamUserId",
                table: "TasafSalesGroups");

            migrationBuilder.DropIndex(
                name: "IX_Tasafs_ReviewerUserId",
                table: "Tasafs");

            migrationBuilder.DropColumn(
                name: "GkamUserId",
                table: "TasafSalesGroups");

            migrationBuilder.DropColumn(
                name: "ApprovalLevelId",
                table: "Tasafs");

            migrationBuilder.DropColumn(
                name: "ApprovalLevelName",
                table: "Tasafs");

            migrationBuilder.DropColumn(
                name: "ApproverName",
                table: "Tasafs");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "Tasafs");

            migrationBuilder.DropColumn(
                name: "ReviewerName",
                table: "Tasafs");

            migrationBuilder.RenameColumn(
                name: "ReviewerUserId",
                table: "Tasafs",
                newName: "CurrentStepSeq");

            migrationBuilder.RenameColumn(
                name: "ApproverUserId",
                table: "Tasafs",
                newName: "CurrentApproverUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Tasafs_ApproverUserId",
                table: "Tasafs",
                newName: "IX_Tasafs_CurrentApproverUserId");

            migrationBuilder.AddColumn<bool>(
                name: "IsAccountSupport",
                table: "TasafProgramTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxCtsPercent",
                table: "TasafProgramTypes",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TasafApprovalRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MinAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AlwaysForAccountSupport = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_TasafApprovalRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TasafApprovalSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TasafId = table.Column<int>(type: "int", nullable: false),
                    Seq = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: true),
                    RoleCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RoleName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafApprovalSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafApprovalSteps_Tasafs_TasafId",
                        column: x => x.TasafId,
                        principalTable: "Tasafs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TasafApprovers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesGroupId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafApprovers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafApprovers_TasafApprovalRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "TasafApprovalRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TasafApprovers_TasafSalesGroups_SalesGroupId",
                        column: x => x.SalesGroupId,
                        principalTable: "TasafSalesGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TasafApprovers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "TasafApprovalRoles",
                columns: new[] { "Id", "AlwaysForAccountSupport", "Code", "CreatedAt", "CreatedBy", "Description", "IsActive", "MinAmount", "Name", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, false, "GKAM", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, 0m, "Group Key Account Manager", 1, null, null },
                    { 2, false, "TMM", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, 0m, "Trade Marketing Manager", 2, null, null },
                    { 3, false, "NSM", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, 20000m, "National Sales Manager", 3, null, null },
                    { 4, true, "NSD", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, 50000m, "National Sales Director", 4, null, null },
                    { 5, true, "COO", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, 100000m, "Chief Operating Officer", 5, null, null }
                });

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "IsAccountSupport", "MaxCtsPercent" },
                values: new object[] { false, 8m });

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "IsAccountSupport", "MaxCtsPercent" },
                values: new object[] { false, 8m });

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "IsAccountSupport", "MaxCtsPercent" },
                values: new object[] { false, 8m });

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "IsAccountSupport", "MaxCtsPercent" },
                values: new object[] { false, 8m });

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "IsAccountSupport", "MaxCtsPercent" },
                values: new object[] { true, null });

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "IsAccountSupport", "MaxCtsPercent" },
                values: new object[] { true, null });

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "IsAccountSupport", "MaxCtsPercent" },
                values: new object[] { true, null });

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "IsAccountSupport", "MaxCtsPercent" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "IsAccountSupport", "MaxCtsPercent" },
                values: new object[] { true, null });

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "IsAccountSupport", "MaxCtsPercent" },
                values: new object[] { true, null });

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "IsAccountSupport", "MaxCtsPercent" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "IsAccountSupport", "MaxCtsPercent" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "TasafProgramTypes",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "IsAccountSupport", "MaxCtsPercent" },
                values: new object[] { false, null });

            migrationBuilder.CreateIndex(
                name: "IX_TasafApprovalRoles_Code",
                table: "TasafApprovalRoles",
                column: "Code",
                unique: true,
                filter: "[Code] != ''");

            migrationBuilder.CreateIndex(
                name: "IX_TasafApprovalRoles_Name",
                table: "TasafApprovalRoles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasafApprovalSteps_TasafId",
                table: "TasafApprovalSteps",
                column: "TasafId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafApprovalSteps_UserId",
                table: "TasafApprovalSteps",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafApprovers_RoleId",
                table: "TasafApprovers",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafApprovers_SalesGroupId_RoleId",
                table: "TasafApprovers",
                columns: new[] { "SalesGroupId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasafApprovers_UserId",
                table: "TasafApprovers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TasafApprovalSteps");

            migrationBuilder.DropTable(
                name: "TasafApprovers");

            migrationBuilder.DropTable(
                name: "TasafApprovalRoles");

            migrationBuilder.DropColumn(
                name: "IsAccountSupport",
                table: "TasafProgramTypes");

            migrationBuilder.DropColumn(
                name: "MaxCtsPercent",
                table: "TasafProgramTypes");

            migrationBuilder.RenameColumn(
                name: "CurrentStepSeq",
                table: "Tasafs",
                newName: "ReviewerUserId");

            migrationBuilder.RenameColumn(
                name: "CurrentApproverUserId",
                table: "Tasafs",
                newName: "ApproverUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Tasafs_CurrentApproverUserId",
                table: "Tasafs",
                newName: "IX_Tasafs_ApproverUserId");

            migrationBuilder.AddColumn<int>(
                name: "GkamUserId",
                table: "TasafSalesGroups",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalLevelId",
                table: "Tasafs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalLevelName",
                table: "Tasafs",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ApproverName",
                table: "Tasafs",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "Tasafs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewerName",
                table: "Tasafs",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "TasafApprovalLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApproverUserId = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafApprovalLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafApprovalLevels_Users_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "TasafApprovalLevels",
                columns: new[] { "Id", "ApproverUserId", "Code", "CreatedAt", "CreatedBy", "Description", "IsActive", "MaxAmount", "Name", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, null, "L1", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, 50000m, "Trade Marketing Manager", 1, null, null },
                    { 2, null, "L2", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, 100000m, "COO", 2, null, null },
                    { 3, null, "L3", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, null, "President & Chairman (VVK)", 3, null, null }
                });

            migrationBuilder.UpdateData(
                table: "TasafSalesGroups",
                keyColumn: "Id",
                keyValue: 1,
                column: "GkamUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TasafSalesGroups",
                keyColumn: "Id",
                keyValue: 2,
                column: "GkamUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TasafSalesGroups",
                keyColumn: "Id",
                keyValue: 3,
                column: "GkamUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TasafSalesGroups",
                keyColumn: "Id",
                keyValue: 4,
                column: "GkamUserId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_TasafSalesGroups_GkamUserId",
                table: "TasafSalesGroups",
                column: "GkamUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_ReviewerUserId",
                table: "Tasafs",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafApprovalLevels_ApproverUserId",
                table: "TasafApprovalLevels",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafApprovalLevels_Code",
                table: "TasafApprovalLevels",
                column: "Code",
                unique: true,
                filter: "[Code] != ''");

            migrationBuilder.CreateIndex(
                name: "IX_TasafApprovalLevels_Name",
                table: "TasafApprovalLevels",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TasafSalesGroups_Users_GkamUserId",
                table: "TasafSalesGroups",
                column: "GkamUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
