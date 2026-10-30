using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafMultipleChains : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TasafChainLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TasafId = table.Column<int>(type: "int", nullable: false),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    ChainId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafChainLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafChainLinks_TasafChains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "TasafChains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TasafChainLinks_Tasafs_TasafId",
                        column: x => x.TasafId,
                        principalTable: "Tasafs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TasafChainLinks_ChainId",
                table: "TasafChainLinks",
                column: "ChainId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafChainLinks_TasafId",
                table: "TasafChainLinks",
                column: "TasafId");

            // The old single Customer / Chain becomes the first (only) picked chain.
            migrationBuilder.Sql("""
                INSERT INTO [TasafChainLinks] ([TasafId], [LineNo], [ChainId])
                SELECT [Id], 1, [ChainId] FROM [Tasafs] WHERE [ChainId] IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Tasafs_TasafChains_ChainId",
                table: "Tasafs");

            migrationBuilder.DropIndex(
                name: "IX_Tasafs_ChainId",
                table: "Tasafs");

            migrationBuilder.DropColumn(
                name: "ChainId",
                table: "Tasafs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChainId",
                table: "Tasafs",
                type: "int",
                nullable: true);

            // Keep the first picked chain.
            migrationBuilder.Sql("""
                UPDATE t SET [ChainId] = c.[ChainId]
                FROM [Tasafs] t
                CROSS APPLY (SELECT TOP 1 [ChainId] FROM [TasafChainLinks] l WHERE l.[TasafId] = t.[Id] ORDER BY l.[LineNo]) c;
                """);

            migrationBuilder.DropTable(
                name: "TasafChainLinks");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_ChainId",
                table: "Tasafs",
                column: "ChainId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasafs_TasafChains_ChainId",
                table: "Tasafs",
                column: "ChainId",
                principalTable: "TasafChains",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
