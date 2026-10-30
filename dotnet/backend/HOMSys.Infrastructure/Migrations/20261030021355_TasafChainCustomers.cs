using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafChainCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TasafChainCustomers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChainId = table.Column<int>(type: "int", nullable: false),
                    CustKey = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CusName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    AreaName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SiteCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafChainCustomers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafChainCustomers_TasafChains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "TasafChains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TasafChainCustomers_ChainId_CustKey",
                table: "TasafChainCustomers",
                columns: new[] { "ChainId", "CustKey" },
                unique: true);
            // Rows are loaded by migration TasafChainsFromExcel.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TasafChainCustomers");
        }
    }
}
