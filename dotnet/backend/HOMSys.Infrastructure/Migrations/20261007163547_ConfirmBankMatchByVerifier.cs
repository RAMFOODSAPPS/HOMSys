using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConfirmBankMatchByVerifier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MatchConfirmedAt",
                table: "DepositBatches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MatchConfirmedBy",
                table: "DepositBatches",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // EXEC: the new columns don't exist yet when an idempotent script's batch is compiled.
            // 1) Existing matches finance made by hand, or whose collection a verifier already verified, count as approved.
            migrationBuilder.Sql("""
                EXEC(N'UPDATE b SET MatchConfirmedAt = ISNULL(b.MatchedAt, SYSUTCDATETIME()), MatchConfirmedBy = ISNULL(l.MatchedBy, ''migration'')
                FROM DepositBatches b JOIN BankStatementLines l ON l.Id = b.MatchedBankLineId
                WHERE l.MatchMethod = ''Manual''
                   OR EXISTS (SELECT 1 FROM Collections c WHERE c.DepositBatchId = b.Id AND c.VerificationStatus = ''Verified'')');
                """);
            // 2) Remaining automatic matches on a slip with an unsubmitted collection are dropped (re-matched on submit).
            migrationBuilder.Sql("""
                EXEC(N'UPDATE l SET MatchedDepositBatchId = NULL, MatchMethod = NULL, MatchedAt = NULL, MatchedBy = NULL
                FROM BankStatementLines l JOIN DepositBatches b ON b.Id = l.MatchedDepositBatchId
                WHERE b.MatchConfirmedAt IS NULL AND EXISTS (SELECT 1 FROM Collections c WHERE c.DepositBatchId = b.Id
                      AND c.VerificationStatus IN (''Draft'', ''Returned''));
                UPDATE b SET MatchedBankLineId = NULL, MatchedAt = NULL
                FROM DepositBatches b
                WHERE b.MatchConfirmedAt IS NULL AND b.MatchedBankLineId IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM BankStatementLines l WHERE l.Id = b.MatchedBankLineId AND l.MatchedDepositBatchId = b.Id)');
                """);
            // 3) Unapproved matches no longer make a collection Deposited; incomplete ones become Draft.
            migrationBuilder.Sql("""
                EXEC(N'UPDATE c SET PaymentStatus = ''Collected''
                FROM Collections c LEFT JOIN DepositBatches b ON b.Id = c.DepositBatchId
                WHERE c.PaymentStatus = ''Deposited'' AND (b.Id IS NULL OR b.MatchConfirmedAt IS NULL);
                UPDATE c SET PaymentStatus = ''Draft''
                FROM Collections c
                WHERE c.VerificationStatus <> ''Voided'' AND c.PaymentStatus = ''Collected''
                  AND (NOT EXISTS (SELECT 1 FROM CollectionAttachments a WHERE a.CollectionId = c.Id)
                       OR (c.ReceiptType = ''CR'' AND c.DepositBatchId IS NULL))');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Collections SET PaymentStatus = 'Collected' WHERE PaymentStatus = 'Draft'");

            migrationBuilder.DropColumn(
                name: "MatchConfirmedAt",
                table: "DepositBatches");

            migrationBuilder.DropColumn(
                name: "MatchConfirmedBy",
                table: "DepositBatches");
        }
    }
}
