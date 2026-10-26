using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHomsWriteBack : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PostedToBmsAt",
                table: "Collections",
                newName: "PostedToHomsAt");

            migrationBuilder.AddColumn<int>(
                name: "CustWhseNo",
                table: "PostedInvoices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmpNo",
                table: "PostedInvoices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Salesman",
                table: "PostedInvoices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AltAccountNo",
                table: "BankAccounts",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAbbr",
                table: "BankAccounts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomsAcctNo",
                table: "BankAccounts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Synced",
                table: "BankAccounts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "HomsPostings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Table = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    MonthKey = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    DocId = table.Column<int>(type: "int", nullable: false),
                    DocNo = table.Column<int>(type: "int", nullable: true),
                    HeaderJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LinesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    File = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomsPostings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomsPostings_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_HomsAcctNo",
                table: "BankAccounts",
                column: "HomsAcctNo",
                unique: true,
                filter: "[HomsAcctNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HomsPostings_CollectionId_Kind",
                table: "HomsPostings",
                columns: new[] { "CollectionId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_HomsPostings_Status_Id",
                table: "HomsPostings",
                columns: new[] { "Status", "Id" });

            // A complete PDC is Pending (check on hand, not yet collected), not Collected.
            migrationBuilder.Sql("UPDATE Collections SET PaymentStatus = 'Pending' WHERE PaymentStatus = 'Collected' AND PaymentMode = 'PostDatedCheck'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Collections SET PaymentStatus = 'Collected' WHERE PaymentStatus = 'Pending'");

            migrationBuilder.DropTable(
                name: "HomsPostings");

            migrationBuilder.DropIndex(
                name: "IX_BankAccounts_HomsAcctNo",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "CustWhseNo",
                table: "PostedInvoices");

            migrationBuilder.DropColumn(
                name: "EmpNo",
                table: "PostedInvoices");

            migrationBuilder.DropColumn(
                name: "Salesman",
                table: "PostedInvoices");

            migrationBuilder.DropColumn(
                name: "AltAccountNo",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "BankAbbr",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "HomsAcctNo",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "Synced",
                table: "BankAccounts");

            migrationBuilder.RenameColumn(
                name: "PostedToHomsAt",
                table: "Collections",
                newName: "PostedToBmsAt");
        }
    }
}
