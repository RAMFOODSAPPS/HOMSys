using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafMultipleAreas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TasafAreaLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TasafId = table.Column<int>(type: "int", nullable: false),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    AreaId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafAreaLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafAreaLinks_TasafAreas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "TasafAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TasafAreaLinks_Tasafs_TasafId",
                        column: x => x.TasafId,
                        principalTable: "Tasafs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TasafAreaLinks_AreaId",
                table: "TasafAreaLinks",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafAreaLinks_TasafId",
                table: "TasafAreaLinks",
                column: "TasafId");

            // The old single Area becomes the first (only) picked area.
            migrationBuilder.Sql("""
                INSERT INTO [TasafAreaLinks] ([TasafId], [LineNo], [AreaId])
                SELECT [Id], 1, [AreaId] FROM [Tasafs] WHERE [AreaId] IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Tasafs_TasafAreas_AreaId",
                table: "Tasafs");

            migrationBuilder.DropIndex(
                name: "IX_Tasafs_AreaId",
                table: "Tasafs");

            migrationBuilder.DropColumn(
                name: "AreaId",
                table: "Tasafs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AreaId",
                table: "Tasafs",
                type: "int",
                nullable: true);

            // Keep the first picked area.
            migrationBuilder.Sql("""
                UPDATE t SET [AreaId] = a.[AreaId]
                FROM [Tasafs] t
                CROSS APPLY (SELECT TOP 1 [AreaId] FROM [TasafAreaLinks] l WHERE l.[TasafId] = t.[Id] ORDER BY l.[LineNo]) a;
                """);

            migrationBuilder.DropTable(
                name: "TasafAreaLinks");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_AreaId",
                table: "Tasafs",
                column: "AreaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasafs_TasafAreas_AreaId",
                table: "Tasafs",
                column: "AreaId",
                principalTable: "TasafAreas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
