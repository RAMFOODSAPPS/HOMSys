using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecNoToPerBranchTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecNo",
                table: "ZoneAddOns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RecNo",
                table: "Zone2AddOns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RecNo",
                table: "CustomerBranchZones",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ZoneAddOns_Branch_RecNo",
                table: "ZoneAddOns",
                columns: new[] { "Branch", "RecNo" });

            migrationBuilder.CreateIndex(
                name: "IX_Zone2AddOns_Branch_RecNo",
                table: "Zone2AddOns",
                columns: new[] { "Branch", "RecNo" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBranchZones_Branch_RecNo",
                table: "CustomerBranchZones",
                columns: new[] { "Branch", "RecNo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ZoneAddOns_Branch_RecNo",
                table: "ZoneAddOns");

            migrationBuilder.DropIndex(
                name: "IX_Zone2AddOns_Branch_RecNo",
                table: "Zone2AddOns");

            migrationBuilder.DropIndex(
                name: "IX_CustomerBranchZones_Branch_RecNo",
                table: "CustomerBranchZones");

            migrationBuilder.DropColumn(
                name: "RecNo",
                table: "ZoneAddOns");

            migrationBuilder.DropColumn(
                name: "RecNo",
                table: "Zone2AddOns");

            migrationBuilder.DropColumn(
                name: "RecNo",
                table: "CustomerBranchZones");
        }
    }
}
