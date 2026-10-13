using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafMultipleChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TasafChannelLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TasafId = table.Column<int>(type: "int", nullable: false),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    ChannelId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafChannelLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafChannelLinks_TasafChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "TasafChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TasafChannelLinks_Tasafs_TasafId",
                        column: x => x.TasafId,
                        principalTable: "Tasafs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TasafChannelLinks_ChannelId",
                table: "TasafChannelLinks",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafChannelLinks_TasafId",
                table: "TasafChannelLinks",
                column: "TasafId");

            // The old single Channel becomes the first (only) picked channel.
            migrationBuilder.Sql("""
                INSERT INTO [TasafChannelLinks] ([TasafId], [LineNo], [ChannelId])
                SELECT [Id], 1, [ChannelId] FROM [Tasafs] WHERE [ChannelId] IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Tasafs_TasafChannels_ChannelId",
                table: "Tasafs");

            migrationBuilder.DropIndex(
                name: "IX_Tasafs_ChannelId",
                table: "Tasafs");

            migrationBuilder.DropColumn(
                name: "ChannelId",
                table: "Tasafs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChannelId",
                table: "Tasafs",
                type: "int",
                nullable: true);

            // Keep the first picked channel.
            migrationBuilder.Sql("""
                UPDATE t SET [ChannelId] = c.[ChannelId]
                FROM [Tasafs] t
                CROSS APPLY (SELECT TOP 1 [ChannelId] FROM [TasafChannelLinks] l WHERE l.[TasafId] = t.[Id] ORDER BY l.[LineNo]) c;
                """);

            migrationBuilder.DropTable(
                name: "TasafChannelLinks");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_ChannelId",
                table: "Tasafs",
                column: "ChannelId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasafs_TasafChannels_ChannelId",
                table: "Tasafs",
                column: "ChannelId",
                principalTable: "TasafChannels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
