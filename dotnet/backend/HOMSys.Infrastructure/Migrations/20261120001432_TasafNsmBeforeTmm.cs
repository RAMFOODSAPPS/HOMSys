using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafNsmBeforeTmm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "TasafApprovalRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "SortOrder",
                value: 3);

            migrationBuilder.UpdateData(
                table: "TasafApprovalRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "SortOrder",
                value: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "TasafApprovalRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "SortOrder",
                value: 2);

            migrationBuilder.UpdateData(
                table: "TasafApprovalRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "SortOrder",
                value: 3);
        }
    }
}
