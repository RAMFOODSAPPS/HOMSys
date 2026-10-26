using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafMultipleCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TasafCustomers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TasafId = table.Column<int>(type: "int", nullable: false),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    CustKey = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CusName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafCustomers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafCustomers_Tasafs_TasafId",
                        column: x => x.TasafId,
                        principalTable: "Tasafs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TasafCustomers_CustKey",
                table: "TasafCustomers",
                column: "CustKey");

            migrationBuilder.CreateIndex(
                name: "IX_TasafCustomers_TasafId",
                table: "TasafCustomers",
                column: "TasafId");

            // The old single Cust Key becomes the first (only) participating store.
            migrationBuilder.Sql("""
                INSERT INTO [TasafCustomers] ([TasafId], [LineNo], [CustKey], [CusName])
                SELECT Id, 1, CustKey, ISNULL(CustomerName, '')
                FROM Tasafs
                WHERE CustKey IS NOT NULL AND LTRIM(RTRIM(CustKey)) <> '';
                """);

            migrationBuilder.DropColumn(
                name: "CustKey",
                table: "Tasafs");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "Tasafs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TasafCustomers");

            migrationBuilder.AddColumn<string>(
                name: "CustKey",
                table: "Tasafs",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "Tasafs",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);
        }
    }
}
