using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafWorksheetHeader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Account",
                table: "TasafWorksheets",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "TasafWorksheets",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "TasafWorksheets",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SalesGroup",
                table: "TasafWorksheets",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            // Existing worksheets: the header = their first row (rows held these per line before).
            migrationBuilder.Sql(@"
UPDATE w SET SalesGroup = l.SalesGroup, Channel = l.Channel, Account = l.Account, Category = l.Category
FROM TasafWorksheets w
CROSS APPLY (SELECT TOP 1 SalesGroup, Channel, Account, Category FROM TasafListingFeeLines
             WHERE WorksheetId = w.Id ORDER BY [LineNo]) l
WHERE w.Type = 'ListingFee';

UPDATE w SET SalesGroup = l.SalesGroup, Account = l.Account, Category = l.Category
FROM TasafWorksheets w
CROSS APPLY (SELECT TOP 1 SalesGroup, Account, Category FROM TasafNearExLines
             WHERE WorksheetId = w.Id ORDER BY [LineNo]) l
WHERE w.Type = 'NearEx';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Account",
                table: "TasafWorksheets");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "TasafWorksheets");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "TasafWorksheets");

            migrationBuilder.DropColumn(
                name: "SalesGroup",
                table: "TasafWorksheets");
        }
    }
}
