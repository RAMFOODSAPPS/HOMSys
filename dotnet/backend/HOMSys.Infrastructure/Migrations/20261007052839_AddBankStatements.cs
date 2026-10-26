using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBankStatements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BankAccountId",
                table: "DepositBatches",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BankAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AccountNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankAccountSites",
                columns: table => new
                {
                    BankAccountId = table.Column<int>(type: "int", nullable: false),
                    SiteId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccountSites", x => new { x.BankAccountId, x.SiteId });
                    table.ForeignKey(
                        name: "FK_BankAccountSites_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankAccountSites_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BankStatementUploads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankAccountId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BlobPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LineCount = table.Column<int>(type: "int", nullable: false),
                    NewLineCount = table.Column<int>(type: "int", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankStatementUploads_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankStatementLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankAccountId = table.Column<int>(type: "int", nullable: false),
                    UploadId = table.Column<int>(type: "int", nullable: false),
                    TxnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TxnTime = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BankBranch = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CheckNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Debit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MatchedDepositBatchId = table.Column<int>(type: "int", nullable: true),
                    MatchMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MatchedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MatchedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankStatementLines_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementLines_BankStatementUploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "BankStatementUploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementLines_DepositBatches_MatchedDepositBatchId",
                        column: x => x.MatchedDepositBatchId,
                        principalTable: "DepositBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepositBatches_BankAccountId_DepositDate",
                table: "DepositBatches",
                columns: new[] { "BankAccountId", "DepositDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_BankCode_AccountNo",
                table: "BankAccounts",
                columns: new[] { "BankCode", "AccountNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankAccountSites_SiteId",
                table: "BankAccountSites",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_BankAccountId_Fingerprint",
                table: "BankStatementLines",
                columns: new[] { "BankAccountId", "Fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_BankAccountId_TxnDate",
                table: "BankStatementLines",
                columns: new[] { "BankAccountId", "TxnDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_MatchedDepositBatchId",
                table: "BankStatementLines",
                column: "MatchedDepositBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_UploadId",
                table: "BankStatementLines",
                column: "UploadId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementUploads_BankAccountId_FileHash",
                table: "BankStatementUploads",
                columns: new[] { "BankAccountId", "FileHash" });

            migrationBuilder.AddForeignKey(
                name: "FK_DepositBatches_BankAccounts_BankAccountId",
                table: "DepositBatches",
                column: "BankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DepositBatches_BankAccounts_BankAccountId",
                table: "DepositBatches");

            migrationBuilder.DropTable(
                name: "BankAccountSites");

            migrationBuilder.DropTable(
                name: "BankStatementLines");

            migrationBuilder.DropTable(
                name: "BankStatementUploads");

            migrationBuilder.DropTable(
                name: "BankAccounts");

            migrationBuilder.DropIndex(
                name: "IX_DepositBatches_BankAccountId_DepositDate",
                table: "DepositBatches");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                table: "DepositBatches");
        }
    }
}
