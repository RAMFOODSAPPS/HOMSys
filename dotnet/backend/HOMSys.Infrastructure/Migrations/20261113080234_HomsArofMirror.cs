using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HomsArofMirror : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HomsArofBlocks",
                columns: table => new
                {
                    BlockNo = table.Column<int>(type: "int", nullable: false),
                    FirstRecNo = table.Column<int>(type: "int", nullable: false),
                    RecordCount = table.Column<int>(type: "int", nullable: false),
                    Hash = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomsArofBlocks", x => x.BlockNo);
                });

            migrationBuilder.CreateTable(
                name: "HomsArofFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Path = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    RecordCount = table.Column<int>(type: "int", nullable: false),
                    RecordLength = table.Column<int>(type: "int", nullable: false),
                    HeaderLength = table.Column<int>(type: "int", nullable: false),
                    BlockSize = table.Column<int>(type: "int", nullable: false),
                    FieldList = table.Column<string>(type: "varchar(4000)", unicode: false, maxLength: 4000, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FileWrittenAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FullScanAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomsArofFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HomsArofRows",
                columns: table => new
                {
                    RecNo = table.Column<int>(type: "int", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    AdjId = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    PaymentId = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    TrDate = table.Column<DateOnly>(type: "date", nullable: true),
                    WhseNo = table.Column<int>(type: "int", nullable: false),
                    EmpNo = table.Column<int>(type: "int", nullable: false),
                    CSman = table.Column<string>(type: "varchar(4)", unicode: false, maxLength: 4, nullable: false),
                    Salesman = table.Column<int>(type: "int", nullable: false),
                    TCode = table.Column<int>(type: "int", nullable: false),
                    AcctNo = table.Column<int>(type: "int", nullable: false),
                    CKey = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: false),
                    CustName = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Term = table.Column<int>(type: "int", nullable: false),
                    DocId = table.Column<int>(type: "int", nullable: false),
                    DocDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DocType = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    DocNo = table.Column<int>(type: "int", nullable: false),
                    RefId = table.Column<int>(type: "int", nullable: false),
                    RefNo = table.Column<int>(type: "int", nullable: false),
                    RefDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RefType = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    DcrNo = table.Column<int>(type: "int", nullable: false),
                    DcrDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BadCqf = table.Column<bool>(type: "bit", nullable: false),
                    BadChq = table.Column<bool>(type: "bit", nullable: false),
                    DrAmt = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    CrAmt = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Bank = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Location = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    CheckNo = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    CheckDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Remarks = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    RefId2 = table.Column<int>(type: "int", nullable: false),
                    RefNo2 = table.Column<int>(type: "int", nullable: false),
                    RefDate2 = table.Column<DateOnly>(type: "date", nullable: true),
                    RefType2 = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    TrType = table.Column<int>(type: "int", nullable: false),
                    DssNo = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false),
                    Reference = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    DusCKey = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: false),
                    DCusName = table.Column<string>(type: "varchar(35)", unicode: false, maxLength: 35, nullable: false),
                    UserName = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    DocBal = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    BatchNo = table.Column<long>(type: "bigint", nullable: false),
                    Transfer = table.Column<bool>(type: "bit", nullable: false),
                    SplitNo = table.Column<int>(type: "int", nullable: false),
                    OrigAmt = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    SysDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Process = table.Column<bool>(type: "bit", nullable: false),
                    Expln1 = table.Column<string>(type: "varchar(75)", unicode: false, maxLength: 75, nullable: false),
                    Expln2 = table.Column<string>(type: "varchar(75)", unicode: false, maxLength: 75, nullable: false),
                    Expln3 = table.Column<string>(type: "varchar(75)", unicode: false, maxLength: 75, nullable: false),
                    BCode = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: false),
                    SName = table.Column<string>(type: "varchar(35)", unicode: false, maxLength: 35, nullable: false),
                    ChkRcvd = table.Column<bool>(type: "bit", nullable: false),
                    CustKey = table.Column<string>(type: "varchar(7)", unicode: false, maxLength: 7, nullable: false),
                    Sman4Soa = table.Column<string>(type: "varchar(6)", unicode: false, maxLength: 6, nullable: false),
                    PayGroup1 = table.Column<string>(type: "varchar(22)", unicode: false, maxLength: 22, nullable: false),
                    PayGroup2 = table.Column<string>(type: "varchar(22)", unicode: false, maxLength: 22, nullable: false),
                    PurgDate = table.Column<DateOnly>(type: "date", nullable: true),
                    HoPosted = table.Column<DateOnly>(type: "date", nullable: true),
                    RsrNo = table.Column<int>(type: "int", nullable: false),
                    RsrDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CrsrNo = table.Column<int>(type: "int", nullable: false),
                    CrsrDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ArofId = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    PoNum = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    PoDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomsArofRows", x => x.RecNo);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HomsArofRows_ArofId",
                table: "HomsArofRows",
                column: "ArofId");

            migrationBuilder.CreateIndex(
                name: "IX_HomsArofRows_DocNo_DocId",
                table: "HomsArofRows",
                columns: new[] { "DocNo", "DocId" });

            migrationBuilder.CreateIndex(
                name: "IX_HomsArofRows_RefNo_RefId",
                table: "HomsArofRows",
                columns: new[] { "RefNo", "RefId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HomsArofBlocks");

            migrationBuilder.DropTable(
                name: "HomsArofFiles");

            migrationBuilder.DropTable(
                name: "HomsArofRows");
        }
    }
}
