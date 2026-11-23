using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PdcDepositoryAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DepositoryAccountId",
                table: "Collections",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Collections_DepositoryAccountId",
                table: "Collections",
                column: "DepositoryAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Collections_BankAccounts_DepositoryAccountId",
                table: "Collections",
                column: "DepositoryAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id");

            // PDCs already deposited: their depository is the account they went into.
            migrationBuilder.Sql("""
                EXEC(N'UPDATE c SET DepositoryAccountId = b.BankAccountId
                FROM Collections c JOIN DepositBatches b ON b.Id = c.DepositBatchId
                WHERE c.PaymentMode = ''PostDatedCheck'' AND c.DepositoryAccountId IS NULL AND b.BankAccountId IS NOT NULL');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Collections_BankAccounts_DepositoryAccountId",
                table: "Collections");

            migrationBuilder.DropIndex(
                name: "IX_Collections_DepositoryAccountId",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "DepositoryAccountId",
                table: "Collections");
        }
    }
}
