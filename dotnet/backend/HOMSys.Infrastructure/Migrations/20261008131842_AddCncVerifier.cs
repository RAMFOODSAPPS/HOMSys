using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCncVerifier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Description", "Key", "Name" },
                values: new object[] { 26, "Confirm a matured PDC's deposit against the bank statement (CNC Verifier) — posts its JV to HOMS", "pdc-confirm", "Matured PDC Confirmation" });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[] { 26, 1 });

            // CNC Verifier (user 2026-10-08): confirms matured PDCs on the PDC Monitor (posts the JV), records PDC
            // deposits, tags Succeeded / Bounced. Idempotent like the AddCollections roles.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'CNC Verifier')
                    INSERT INTO Roles (Name, Description, CreatedAt, CreatedBy)
                    VALUES ('CNC Verifier', 'Confirms matured PDCs against the bank statement on the PDC Monitor (posts the JV to HOMS), records PDC deposits and tags PDC outcomes', GETUTCDATE(), 'system');

                INSERT INTO RolePermissions (RoleId, PermissionId)
                SELECT r.Id, p.Id FROM Roles r
                JOIN (VALUES ('CNC Verifier', 26), ('CNC Verifier', 24), ('CNC Verifier', 25)) g(RoleName, PermId) ON g.RoleName = r.Name
                JOIN Permissions p ON p.Id = g.PermId
                WHERE NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = r.Id AND x.PermissionId = p.Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM RolePermissions WHERE PermissionId = 26 AND RoleId <> 1");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 26, 1 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 26);
        }
    }
}
