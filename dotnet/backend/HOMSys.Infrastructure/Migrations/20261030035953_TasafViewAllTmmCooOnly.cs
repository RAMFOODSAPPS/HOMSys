using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <summary>
    /// TASAF View All only for TMM, COO and Admin (user decision 2026-10-30). GKAM / NSM / NSD keep "TASAF" and see
    /// only the TASAFs they file or are on the approval chain of. Other roles holding View All (none today) are
    /// left alone; the Authorization page can still change any of this.
    /// </summary>
    public partial class TasafViewAllTmmCooOnly : Migration
    {
        private const string Roles = "('GKAM'), ('NSM'), ('NSD')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DELETE rp
                FROM [RolePermissions] rp
                JOIN [Roles] r ON r.[Id] = rp.[RoleId]
                JOIN [Permissions] p ON p.[Id] = rp.[PermissionId]
                WHERE p.[Key] = 'tasaf-view-all' AND r.[Name] IN (SELECT v.[Name] FROM (VALUES {Roles}) v([Name]));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                INSERT INTO [RolePermissions] ([RoleId], [PermissionId])
                SELECT r.[Id], p.[Id]
                FROM (VALUES {Roles}) v([Name])
                JOIN [Roles] r ON r.[Name] = v.[Name]
                JOIN [Permissions] p ON p.[Key] = 'tasaf-view-all'
                WHERE NOT EXISTS (SELECT 1 FROM [RolePermissions] x WHERE x.[RoleId] = r.[Id] AND x.[PermissionId] = p.[Id]);
                """);
        }
    }
}
