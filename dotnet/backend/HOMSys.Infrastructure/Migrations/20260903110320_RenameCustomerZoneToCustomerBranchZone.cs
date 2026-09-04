using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameCustomerZoneToCustomerBranchZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "CustomerZones",
                newName: "CustomerBranchZones");

            migrationBuilder.RenameIndex(
                name: "IX_CustomerZones_Branch",
                table: "CustomerBranchZones",
                newName: "IX_CustomerBranchZones_Branch");

            migrationBuilder.RenameIndex(
                name: "IX_CustomerZones_CustKey",
                table: "CustomerBranchZones",
                newName: "IX_CustomerBranchZones_CustKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "CustomerBranchZones",
                newName: "CustomerZones");

            migrationBuilder.RenameIndex(
                name: "IX_CustomerBranchZones_Branch",
                table: "CustomerZones",
                newName: "IX_CustomerZones_Branch");

            migrationBuilder.RenameIndex(
                name: "IX_CustomerBranchZones_CustKey",
                table: "CustomerZones",
                newName: "IX_CustomerZones_CustKey");
        }
    }
}
