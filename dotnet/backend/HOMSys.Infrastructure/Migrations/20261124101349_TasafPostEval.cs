using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafPostEval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TasafPostEvals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TasafId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActualStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PlannedDoors = table.Column<int>(type: "int", nullable: true),
                    ActualDoors = table.Column<int>(type: "int", nullable: true),
                    Explanation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Conclusion = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    PlannedCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ActualCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ApprovalTypeName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CurrentStepSeq = table.Column<int>(type: "int", nullable: true),
                    CurrentApproverUserId = table.Column<int>(type: "int", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafPostEvals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafPostEvals_Tasafs_TasafId",
                        column: x => x.TasafId,
                        principalTable: "Tasafs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TasafPostEvalLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostEvalId = table.Column<int>(type: "int", nullable: false),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    CProdNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    LspPerCase = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PrePromoCs = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PlannedCs = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ActualCs = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PostPromoCs = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafPostEvalLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafPostEvalLines_TasafPostEvals_PostEvalId",
                        column: x => x.PostEvalId,
                        principalTable: "TasafPostEvals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TasafPostEvalSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostEvalId = table.Column<int>(type: "int", nullable: false),
                    Seq = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: true),
                    RoleCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RoleName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafPostEvalSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafPostEvalSteps_TasafPostEvals_PostEvalId",
                        column: x => x.PostEvalId,
                        principalTable: "TasafPostEvals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TasafPostEvalLines_PostEvalId",
                table: "TasafPostEvalLines",
                column: "PostEvalId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafPostEvals_CurrentApproverUserId",
                table: "TasafPostEvals",
                column: "CurrentApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafPostEvals_TasafId",
                table: "TasafPostEvals",
                column: "TasafId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasafPostEvalSteps_PostEvalId",
                table: "TasafPostEvalSteps",
                column: "PostEvalId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafPostEvalSteps_UserId",
                table: "TasafPostEvalSteps",
                column: "UserId");

            // Every TASAF already Completed gets its Draft post evaluation, as one completing from now on does
            // (TasafPostEvalService.StartAsync; no notification for these).
            migrationBuilder.Sql(@"
INSERT INTO TasafPostEvals (TasafId, Kind, Status, Explanation, Conclusion, PlannedCost, ActualCost, PlannedDoors, ApprovalTypeName, CreatedByName, CreatedAt)
SELECT t.Id,
       CASE WHEN LTRIM(ISNULL(pt.Name, N'')) LIKE N'PROMO%' THEN N'Promo' ELSE N'Activity' END,
       N'Draft', N'', N'', t.TotalCost,
       (SELECT ISNULL(SUM(x.Amount), 0) FROM TasafTpars x WHERE x.TasafId = t.Id AND x.Status = N'Approved'),
       (SELECT COUNT(*) FROM TasafCustomers c WHERE c.TasafId = t.Id),
       N'', N'Automatic', SYSUTCDATETIME()
FROM Tasafs t LEFT JOIN TasafProgramTypes pt ON pt.Id = t.ProgramTypeId
WHERE t.Status = N'Completed' AND NOT EXISTS (SELECT 1 FROM TasafPostEvals p WHERE p.TasafId = t.Id);

INSERT INTO TasafPostEvalLines (PostEvalId, [LineNo], CProdNo, Sku, LspPerCase, PrePromoCs, PlannedCs)
SELECT p.Id, ROW_NUMBER() OVER (PARTITION BY p.Id ORDER BY tp.[LineNo], tp.Id), tp.CProdNo, tp.Sku, tp.LspPerCase,
       COALESCE(tp.P3MoAvgCs, tp.SplyCs), tp.PromotedVolCs
FROM TasafPostEvals p JOIN TasafProducts tp ON tp.TasafId = p.TasafId AND tp.CProdNo <> N''
WHERE p.CreatedByName = N'Automatic' AND NOT EXISTS (SELECT 1 FROM TasafPostEvalLines l WHERE l.PostEvalId = p.Id);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TasafPostEvalLines");

            migrationBuilder.DropTable(
                name: "TasafPostEvalSteps");

            migrationBuilder.DropTable(
                name: "TasafPostEvals");
        }
    }
}
