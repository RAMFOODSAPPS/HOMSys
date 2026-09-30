using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOffshoreEncoder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AcceptsOffshoreOrders",
                table: "Sites",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ForBranch",
                table: "SalesOrders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OffshoreError",
                table: "SalesOrders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OffshoreReceivedAt",
                table: "SalesOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OffshoreUploadedAt",
                table: "SalesOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginBranch",
                table: "SalesOrders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OffshoreTransfers",
                columns: table => new
                {
                    SoId = table.Column<int>(type: "int", nullable: false),
                    HeaderJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LinesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DiscountsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OffshoreTransfers", x => x.SoId);
                    table.ForeignKey(
                        name: "FK_OffshoreTransfers_SalesOrders_SoId",
                        column: x => x.SoId,
                        principalTable: "SalesOrders",
                        principalColumn: "SoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Description", "Key", "Name" },
                values: new object[] { 15, "Encode sales orders For Branch — processed at this branch's BMS, then handed off to the selected branch", "offshore-encode", "Offshore Encoding" });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[] { 15, 1 });

            // Every existing order was encoded at the branch that owns it. EXEC defers
            // column binding so this also runs in an idempotent script's single batch.
            migrationBuilder.Sql("EXEC('UPDATE SalesOrders SET OriginBranch = Branch WHERE OriginBranch IS NULL')");

            // The HON / LKA-HO encoder role: encode + For Branch + analytics.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'Offshore Encoder')
                    INSERT INTO Roles (Name, Description, CreatedAt, CreatedBy)
                    VALUES ('Offshore Encoder', 'Encodes sales orders For another branch (HON / LKA-HO): processed at the encoder''s BMS, then handed off to the selected branch', GETUTCDATE(), 'system');

                INSERT INTO RolePermissions (RoleId, PermissionId)
                SELECT r.Id, p.Id FROM Roles r CROSS JOIN Permissions p
                WHERE r.Name = 'Offshore Encoder' AND p.Id IN (8, 13, 15)
                  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = r.Id AND x.PermissionId = p.Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The role is kept (it may have users); only its offshore grant goes with the permission.
            migrationBuilder.Sql("DELETE FROM RolePermissions WHERE PermissionId = 15 AND RoleId <> 1");

            migrationBuilder.DropTable(
                name: "OffshoreTransfers");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 15, 1 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DropColumn(
                name: "AcceptsOffshoreOrders",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "ForBranch",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "OffshoreError",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "OffshoreReceivedAt",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "OffshoreUploadedAt",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "OriginBranch",
                table: "SalesOrders");
        }
    }
}
