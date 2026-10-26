using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBmsPostedCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BmsPostedCollections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceFile = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    DocId = table.Column<int>(type: "int", nullable: false),
                    DocNo = table.Column<int>(type: "int", nullable: false),
                    DocDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DcrNo = table.Column<int>(type: "int", nullable: false),
                    DcrDate = table.Column<DateOnly>(type: "date", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Payor = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TCode = table.Column<int>(type: "int", nullable: false),
                    Posted = table.Column<DateOnly>(type: "date", nullable: true),
                    StatDesc = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    DeptCode = table.Column<int>(type: "int", nullable: false),
                    SiteId = table.Column<int>(type: "int", nullable: true),
                    CustKey = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    ArApplied = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Ewt = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Received = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CheckNo = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    CheckDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DraweeBank = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DepBank = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Bounced = table.Column<bool>(type: "bit", nullable: false),
                    Cancelled = table.Column<bool>(type: "bit", nullable: false),
                    Removed = table.Column<bool>(type: "bit", nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CollectionId = table.Column<int>(type: "int", nullable: true),
                    LinkMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LinkedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BmsPostedCollections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BmsPostedCollections_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BmsPostedCollections_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BmsPostedCollectionLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BmsPostedCollectionId = table.Column<int>(type: "int", nullable: false),
                    WhseNo = table.Column<int>(type: "int", nullable: false),
                    CustKey = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    CKey = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    RefId = table.Column<int>(type: "int", nullable: false),
                    RefNo = table.Column<int>(type: "int", nullable: false),
                    RefDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DrAmt = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CrAmt = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AcctNo = table.Column<int>(type: "int", nullable: false),
                    DraweeBank = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CheckNo = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CheckDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BadCheck = table.Column<bool>(type: "bit", nullable: false),
                    DepBank = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PaymentId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecBank = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TrType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BmsPostedCollectionLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BmsPostedCollectionLines_BmsPostedCollections_BmsPostedCollectionId",
                        column: x => x.BmsPostedCollectionId,
                        principalTable: "BmsPostedCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BmsPostedCollectionLines_BmsPostedCollectionId",
                table: "BmsPostedCollectionLines",
                column: "BmsPostedCollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_BmsPostedCollectionLines_RefNo_AcctNo",
                table: "BmsPostedCollectionLines",
                columns: new[] { "RefNo", "AcctNo" });

            migrationBuilder.CreateIndex(
                name: "IX_BmsPostedCollections_CollectionId",
                table: "BmsPostedCollections",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_BmsPostedCollections_CustKey",
                table: "BmsPostedCollections",
                column: "CustKey");

            migrationBuilder.CreateIndex(
                name: "IX_BmsPostedCollections_DocNo",
                table: "BmsPostedCollections",
                column: "DocNo");

            migrationBuilder.CreateIndex(
                name: "IX_BmsPostedCollections_SiteId",
                table: "BmsPostedCollections",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_BmsPostedCollections_SourceFile_DocId_DocNo",
                table: "BmsPostedCollections",
                columns: new[] { "SourceFile", "DocId", "DocNo" },
                unique: true);

            // Management view on the Collections Overview dashboard: who pays (BMS posted ORs). APPENDED
            // to the widget array and skipped if "bm1" is already there, so finance's edits survive.
            migrationBuilder.Sql($"""
                DECLARE @w nvarchar(max) = N'{BmsWidgets.Replace("'", "''")}';
                UPDATE SavedReports
                   SET DefinitionJson = JSON_MODIFY(DefinitionJson, '$.widgets',
                           JSON_QUERY('[' + (SELECT STRING_AGG(value, ',') WITHIN GROUP (ORDER BY o, k)
                                             FROM (SELECT value, 0 AS o, CAST([key] AS int) AS k FROM OPENJSON(DefinitionJson, '$.widgets')
                                                   UNION ALL
                                                   SELECT value, 1, CAST([key] AS int) FROM OPENJSON(@w)) x) + ']'))
                 WHERE Kind = 'dashboard' AND Name = 'Collections Overview' AND CreatedBy = 'system'
                   AND NOT EXISTS (SELECT 1 FROM OPENJSON(DefinitionJson, '$.widgets') WHERE JSON_VALUE(value, '$.id') = 'bm1');
                """);
        }

        private const string BmsWidgets = """
            [
             {"id":"bm1","title":"Posted in BMS — Collected (month to date)","w":4,"h":2,"spec":{"v":1,"dataset":"bms_collections","type":"kpi","dimensions":[],"measures":[{"field":"received"}],"filters":[{"field":"docDate","op":"preset","values":["mtd"]}],"compare":"previousPeriod"}},
             {"id":"bm2","title":"Posted in BMS — CEWT 2307 (month to date)","w":4,"h":2,"spec":{"v":1,"dataset":"bms_collections","type":"kpi","dimensions":[],"measures":[{"field":"ewt"}],"filters":[{"field":"docDate","op":"preset","values":["mtd"]}]}},
             {"id":"bm3","title":"BMS Bounced Checks (12 months)","w":4,"h":2,"spec":{"v":1,"dataset":"bms_collections","type":"kpi","dimensions":[],"measures":[{"field":"bouncedAmt"}],"filters":[{"field":"docDate","op":"preset","values":["last12m"]}]}},
             {"id":"bm4","title":"Biggest Paying Customers — BMS posted (12 months)","w":6,"h":5,"spec":{"v":1,"dataset":"bms_collections","type":"table","dimensions":[{"field":"custKey"},{"field":"branch"}],"measures":[{"field":"received"},{"field":"receipts"},{"field":"bouncedAmt"}],"filters":[{"field":"docDate","op":"preset","values":["last12m"]}],"sort":{"by":"m0","dir":"desc"},"limit":25}},
             {"id":"bm5","title":"BMS Collections by Month","w":6,"h":5,"spec":{"v":1,"dataset":"bms_collections","type":"bar","dimensions":[{"field":"docDate","bucket":"month"}],"measures":[{"field":"received"}],"filters":[{"field":"docDate","op":"preset","values":["last12m"]}],"fill":true}}
            ]
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE SavedReports
                   SET DefinitionJson = JSON_MODIFY(DefinitionJson, '$.widgets',
                           JSON_QUERY('[' + ISNULL((SELECT STRING_AGG(value, ',') WITHIN GROUP (ORDER BY CAST([key] AS int))
                                                    FROM OPENJSON(DefinitionJson, '$.widgets')
                                                    WHERE JSON_VALUE(value, '$.id') NOT LIKE 'bm[1-5]'), '') + ']'))
                 WHERE Kind = 'dashboard' AND Name = 'Collections Overview' AND CreatedBy = 'system';
                """);

            migrationBuilder.DropTable(
                name: "BmsPostedCollectionLines");

            migrationBuilder.DropTable(
                name: "BmsPostedCollections");
        }
    }
}
