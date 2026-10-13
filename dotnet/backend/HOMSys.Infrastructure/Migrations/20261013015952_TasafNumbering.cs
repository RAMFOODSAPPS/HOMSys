using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafNumbering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RefSeq",
                table: "Tasafs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TasafNumberings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    NextNumber = table.Column<int>(type: "int", nullable: false),
                    MinDigits = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafNumberings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "TasafNumberings",
                columns: new[] { "Id", "MinDigits", "NextNumber", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 1, 2, 1, null, null });

            // Backfill RefSeq from existing RefNos (number after the last '-'). If an older
            // per-prefix series produced the same number twice, only the first keeps it.
            migrationBuilder.Sql("""
                WITH parsed AS (
                    SELECT Id, TRY_CAST(RIGHT(RefNo, CHARINDEX('-', REVERSE(RefNo)) - 1) AS int) AS Seq
                    FROM Tasafs
                    WHERE RefNo IS NOT NULL AND CHARINDEX('-', RefNo) > 0
                ), ranked AS (
                    SELECT Id, Seq, ROW_NUMBER() OVER (PARTITION BY Seq ORDER BY Id) AS rn
                    FROM parsed WHERE Seq IS NOT NULL
                )
                UPDATE t SET RefSeq = r.Seq
                FROM Tasafs t JOIN ranked r ON r.Id = t.Id
                WHERE r.rn = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_RefSeq",
                table: "Tasafs",
                column: "RefSeq",
                unique: true,
                filter: "[RefSeq] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TasafNumberings");

            migrationBuilder.DropIndex(
                name: "IX_Tasafs_RefSeq",
                table: "Tasafs");

            migrationBuilder.DropColumn(
                name: "RefSeq",
                table: "Tasafs");
        }
    }
}
