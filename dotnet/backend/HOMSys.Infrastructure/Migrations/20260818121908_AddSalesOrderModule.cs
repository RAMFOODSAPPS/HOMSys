using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesOrderModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustKey = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    CKey = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CusName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AddrLn1 = table.Column<string>(type: "nvarchar(75)", maxLength: 75, nullable: false),
                    AddrLn2 = table.Column<string>(type: "nvarchar(75)", maxLength: 75, nullable: false),
                    DelAddrLn1 = table.Column<string>(type: "nvarchar(75)", maxLength: 75, nullable: false),
                    DelAddrLn2 = table.Column<string>(type: "nvarchar(75)", maxLength: 75, nullable: false),
                    DelArea = table.Column<string>(type: "nvarchar(75)", maxLength: 75, nullable: false),
                    WhseNo = table.Column<int>(type: "int", nullable: false),
                    CustWhse = table.Column<int>(type: "int", nullable: false),
                    Salesman = table.Column<int>(type: "int", nullable: false),
                    CsMan = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Term = table.Column<int>(type: "int", nullable: false),
                    TermDays = table.Column<int>(type: "int", nullable: false),
                    CZone = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    VatId = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    Subd = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Tpc = table.Column<bool>(type: "bit", nullable: false),
                    Offshore = table.Column<bool>(type: "bit", nullable: false),
                    ExBranch = table.Column<bool>(type: "bit", nullable: false),
                    CCode = table.Column<int>(type: "int", nullable: false),
                    OldCCode = table.Column<int>(type: "int", nullable: false),
                    IEffDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BlockInv = table.Column<bool>(type: "bit", nullable: false),
                    Tin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AliasKey = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    ConsoMax2 = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CProdNo = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    ProdNo = table.Column<int>(type: "int", nullable: false),
                    ProdDesc = table.Column<string>(type: "nvarchar(75)", maxLength: 75, nullable: false),
                    PackSize = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Pieces = table.Column<int>(type: "int", nullable: false),
                    QtyPerPc = table.Column<int>(type: "int", nullable: false),
                    InnerQty = table.Column<int>(type: "int", nullable: false),
                    Um = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Supplier = table.Column<int>(type: "int", nullable: false),
                    PriceList = table.Column<bool>(type: "bit", nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(6,4)", precision: 6, scale: 4, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrders",
                columns: table => new
                {
                    SoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SoNo = table.Column<int>(type: "int", nullable: true),
                    DocNo = table.Column<int>(type: "int", nullable: true),
                    CustKey = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    CusName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CKey = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CCode = table.Column<int>(type: "int", nullable: false),
                    WhseNo = table.Column<int>(type: "int", nullable: false),
                    CustWhse = table.Column<int>(type: "int", nullable: false),
                    ShipToLn1 = table.Column<string>(type: "nvarchar(75)", maxLength: 75, nullable: false),
                    ShipToLn2 = table.Column<string>(type: "nvarchar(75)", maxLength: 75, nullable: false),
                    DelArea = table.Column<string>(type: "nvarchar(75)", maxLength: 75, nullable: false),
                    Term = table.Column<int>(type: "int", nullable: false),
                    TermDays = table.Column<int>(type: "int", nullable: false),
                    Salesman = table.Column<int>(type: "int", nullable: false),
                    CsMan = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    PoNum = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    PoDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InvRem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Tpc = table.Column<bool>(type: "bit", nullable: false),
                    Offshore = table.Column<bool>(type: "bit", nullable: false),
                    ExBranch = table.Column<bool>(type: "bit", nullable: false),
                    VatId = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    ExpectDel = table.Column<bool>(type: "bit", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SysDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OrNo = table.Column<int>(type: "int", nullable: true),
                    ChkDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OrAmt = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    SoTymStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SoTymEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SoElapsed = table.Column<string>(type: "nvarchar(75)", maxLength: 75, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InvNo = table.Column<int>(type: "int", nullable: true),
                    InvDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InvAmt = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    InvTax = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    TransInv = table.Column<int>(type: "int", nullable: true),
                    RsrNo = table.Column<int>(type: "int", nullable: true),
                    RsrDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OrigSo = table.Column<int>(type: "int", nullable: true),
                    RFlag = table.Column<bool>(type: "bit", nullable: true),
                    DcsNo = table.Column<int>(type: "int", nullable: true),
                    MjReqNo = table.Column<int>(type: "int", nullable: true),
                    Promodel = table.Column<bool>(type: "bit", nullable: true),
                    PrintFccor = table.Column<bool>(type: "bit", nullable: true),
                    DrNo = table.Column<int>(type: "int", nullable: true),
                    PoNo = table.Column<int>(type: "int", nullable: true),
                    GrNo = table.Column<int>(type: "int", nullable: true),
                    BirNo = table.Column<int>(type: "int", nullable: true),
                    DpiNo = table.Column<int>(type: "int", nullable: true),
                    DpiDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Depot = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    TransTag = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    RTotCs = table.Column<int>(type: "int", nullable: true),
                    RTotPc = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    CrAuthor = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    StatDesc = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    CauseFail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Transmit = table.Column<DateOnly>(type: "date", nullable: true),
                    PickListed = table.Column<DateOnly>(type: "date", nullable: true),
                    PickNo = table.Column<int>(type: "int", nullable: true),
                    PickTDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ServeDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FccrDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PassDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Delivered = table.Column<DateOnly>(type: "date", nullable: true),
                    RrNo = table.Column<int>(type: "int", nullable: true),
                    RrDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PlateNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Trucker = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: true),
                    Driver = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: true),
                    VsNo = table.Column<int>(type: "int", nullable: true),
                    VsDate = table.Column<DateOnly>(type: "date", nullable: true),
                    VsRemarks = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BirFrom = table.Column<int>(type: "int", nullable: true),
                    BirTo = table.Column<int>(type: "int", nullable: true),
                    Cewit = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    OldCsMan = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    OldWhseNo = table.Column<int>(type: "int", nullable: true),
                    OldSMan = table.Column<int>(type: "int", nullable: true),
                    TrType = table.Column<int>(type: "int", nullable: true),
                    ServeWh = table.Column<int>(type: "int", nullable: true),
                    DssNo = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    DssAmt = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    DssDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Allocate = table.Column<bool>(type: "bit", nullable: true),
                    FccosNo = table.Column<int>(type: "int", nullable: true),
                    FccosAmt = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Cl = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    CurSlAmt = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    CurrentSo = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    CurrPay = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    PendingSo = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    OrNo1 = table.Column<int>(type: "int", nullable: true),
                    OrDate1 = table.Column<DateOnly>(type: "date", nullable: true),
                    OrAmt1 = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    CheckNo1 = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CheckDate1 = table.Column<DateOnly>(type: "date", nullable: true),
                    Invoice1 = table.Column<int>(type: "int", nullable: true),
                    ChainCl = table.Column<int>(type: "int", nullable: true),
                    ChainCode = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(75)", maxLength: 75, nullable: true),
                    ReasonCode = table.Column<int>(type: "int", nullable: true),
                    WithVat = table.Column<bool>(type: "bit", nullable: true),
                    SubdSMan = table.Column<int>(type: "int", nullable: true),
                    SubdName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RefRgwNo = table.Column<int>(type: "int", nullable: true),
                    RefRgwDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RefRgwAmt = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Rfc4Rgw = table.Column<int>(type: "int", nullable: true),
                    JobberWhse = table.Column<int>(type: "int", nullable: true),
                    JobberCKey = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    Rem1 = table.Column<string>(type: "nvarchar(75)", maxLength: 75, nullable: true),
                    Rem2 = table.Column<string>(type: "nvarchar(75)", maxLength: 75, nullable: true),
                    Rem3 = table.Column<string>(type: "nvarchar(75)", maxLength: 75, nullable: true),
                    VhType = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    ChargeType = table.Column<int>(type: "int", nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Drops = table.Column<int>(type: "int", nullable: true),
                    DocClass = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    RefSoReq = table.Column<int>(type: "int", nullable: true),
                    VisMin = table.Column<bool>(type: "bit", nullable: true),
                    Vessel = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    WaybillNo = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Voyage = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Van = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    BlNo = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Edd = table.Column<DateOnly>(type: "date", nullable: true),
                    Eda2 = table.Column<DateOnly>(type: "date", nullable: true),
                    DelWhse = table.Column<int>(type: "int", nullable: true),
                    Moe = table.Column<bool>(type: "bit", nullable: true),
                    TimeStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TimeEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Elapsed = table.Column<string>(type: "nvarchar(75)", maxLength: 75, nullable: true),
                    IsPsuedoSo = table.Column<bool>(type: "bit", nullable: true),
                    ExtWhse = table.Column<bool>(type: "bit", nullable: true),
                    DrpCust = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrders", x => x.SoId);
                });

            migrationBuilder.CreateTable(
                name: "PoLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PoNum = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    PoDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SoNo = table.Column<int>(type: "int", nullable: true),
                    SoId = table.Column<int>(type: "int", nullable: true),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CustKey = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    CusName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsSeeded = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoLogs_SalesOrders_SoId",
                        column: x => x.SoId,
                        principalTable: "SalesOrders",
                        principalColumn: "SoId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrderLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SoId = table.Column<int>(type: "int", nullable: false),
                    DocNo = table.Column<int>(type: "int", nullable: true),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    CProdNo = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    ProdNo = table.Column<int>(type: "int", nullable: false),
                    ProdDesc = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PackSize = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    QtyCs = table.Column<int>(type: "int", nullable: false),
                    QtyPc = table.Column<int>(type: "int", nullable: false),
                    Pieces = table.Column<int>(type: "int", nullable: false),
                    QtyPerPc = table.Column<int>(type: "int", nullable: false),
                    Um = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Supplier = table.Column<int>(type: "int", nullable: false),
                    CSupplier = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PriceList = table.Column<bool>(type: "bit", nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(6,4)", precision: 6, scale: 4, nullable: false),
                    Class = table.Column<int>(type: "int", nullable: false),
                    FreeGoods = table.Column<bool>(type: "bit", nullable: false),
                    InvNo = table.Column<int>(type: "int", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    ZPrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Amt = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    NetAmt = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Taxable = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Tax = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Cost = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Uc = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    OldAmt = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    OldCost = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    F10430 = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Discount1 = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Discount2 = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Discount3 = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Discount4 = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Discount1S = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Discount1C = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Discount2S = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Discount2C = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Discount3S = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Discount3C = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Cash2S = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Cash2C = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Free1S = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Free1C = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    FreeQCs1 = table.Column<decimal>(type: "decimal(9,3)", precision: 9, scale: 3, nullable: true),
                    FreeQPc1 = table.Column<int>(type: "int", nullable: true),
                    FreeQCs2 = table.Column<decimal>(type: "decimal(9,3)", precision: 9, scale: 3, nullable: true),
                    FreeQPc2 = table.Column<int>(type: "int", nullable: true),
                    FreeAdd = table.Column<bool>(type: "bit", nullable: true),
                    FreeProd = table.Column<bool>(type: "bit", nullable: true),
                    AutoFree = table.Column<bool>(type: "bit", nullable: true),
                    FgDiscType = table.Column<DateOnly>(type: "date", nullable: true),
                    FgQtyCs = table.Column<int>(type: "int", nullable: true),
                    FgQtyPc = table.Column<int>(type: "int", nullable: true),
                    EpCs = table.Column<int>(type: "int", nullable: true),
                    StkFlag = table.Column<int>(type: "int", nullable: true),
                    OrigQtyCs = table.Column<int>(type: "int", nullable: true),
                    OrigQtyPc = table.Column<int>(type: "int", nullable: true),
                    Weight = table.Column<int>(type: "int", nullable: true),
                    JobArea = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    DpAgeNo = table.Column<int>(type: "int", nullable: true),
                    Batch = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    RefSoReq = table.Column<int>(type: "int", nullable: true),
                    RrNo = table.Column<int>(type: "int", nullable: true),
                    RrDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DrpCust = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesOrderLines_SalesOrders_SoId",
                        column: x => x.SoId,
                        principalTable: "SalesOrders",
                        principalColumn: "SoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Description", "Key", "Name" },
                values: new object[] { 8, "Encode and view sales orders", "sales-orders", "Sales Order Encoding" });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[] { 8, 1 });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CustKey",
                table: "Customers",
                column: "CustKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PoLogs_PoNum",
                table: "PoLogs",
                column: "PoNum");

            migrationBuilder.CreateIndex(
                name: "IX_PoLogs_SoId",
                table: "PoLogs",
                column: "SoId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CProdNo",
                table: "Products",
                column: "CProdNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderLines_SoId_LineNo",
                table: "SalesOrderLines",
                columns: new[] { "SoId", "LineNo" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CustKey",
                table: "SalesOrders",
                column: "CustKey");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_OrderDate",
                table: "SalesOrders",
                column: "OrderDate");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_PoNum",
                table: "SalesOrders",
                column: "PoNum");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_SoNo",
                table: "SalesOrders",
                column: "SoNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "PoLogs");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "SalesOrderLines");

            migrationBuilder.DropTable(
                name: "SalesOrders");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 8, 1 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 8);
        }
    }
}
