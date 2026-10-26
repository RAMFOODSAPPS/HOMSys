using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafSalesGroupUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TasafSalesGroupUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesGroupId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafSalesGroupUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafSalesGroupUsers_TasafSalesGroups_SalesGroupId",
                        column: x => x.SalesGroupId,
                        principalTable: "TasafSalesGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TasafSalesGroupUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TasafSalesGroupUsers_SalesGroupId_UserId",
                table: "TasafSalesGroupUsers",
                columns: new[] { "SalesGroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasafSalesGroupUsers_UserId",
                table: "TasafSalesGroupUsers",
                column: "UserId");

            // KAMs per sales group from the trade marketing approval matrix (2026-10-19), by username.
            // Users not found are skipped; edit on TASAF Setup › Sales Groups.
            migrationBuilder.Sql("""
                INSERT INTO [TasafSalesGroupUsers] ([SalesGroupId], [UserId])
                SELECT g.[Id], u.[Id]
                FROM (VALUES
                    ('NKA', '03005603'), ('NKA', '03006003'), ('NKA', '03005978'), ('NKA', '03005995'),
                    ('RKA', '03005949'), ('RKA', '03005977'), ('RKA', '03005699'), ('RKA', '03005944'),
                    ('RKA', '03005975'), ('RKA', '03005991'),
                    ('LKA', '03005701'), ('LKA', '03005911'), ('LKA', '03005903'), ('LKA', '03005697'),
                    ('LKA', '03005623')
                ) v([Group], [Username])
                JOIN [TasafSalesGroups] g ON g.[Code] = v.[Group]
                JOIN [Users] u ON u.[Username] = v.[Username]
                WHERE NOT EXISTS (SELECT 1 FROM [TasafSalesGroupUsers] x WHERE x.[SalesGroupId] = g.[Id] AND x.[UserId] = u.[Id]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TasafSalesGroupUsers");
        }
    }
}
