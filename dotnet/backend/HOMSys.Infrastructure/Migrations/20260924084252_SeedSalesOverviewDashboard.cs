using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedSalesOverviewDashboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "Sales Overview" system dashboard — replaces the fixed Sales Analytics
            // page (/sales-order-analytics) with Data Analytics widgets: real InvAmt
            // instead of today's-price estimates, lifecycle-ordered status, full
            // rankings, zero-filled trend, Manila "today". Widgets embed their specs.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM SavedReports WHERE IsSystem = 1 AND Kind = 'dashboard' AND Name = 'Sales Overview')
                INSERT INTO SavedReports (Kind, Name, Description, DatasetKey, Visual, DefinitionJson, OwnerUserId, IsSystem, SharedRoleIds, CreatedAt, CreatedBy)
                VALUES ('dashboard', 'Sales Overview', 'Orders, invoiced sales, fulfilment and undelivered invoices (replaces Sales Analytics).',
                        NULL, NULL, N'{"v":1,"filters":[{"field":"orderDate","op":"preset","values":["mtd"]},{"field":"branch","op":"in","values":[]}],"widgets":[{"id":"so1","title":"Orders Today","w":3,"h":2,"spec":{"v":1,"dataset":"sales_orders","type":"kpi","dimensions":[],"measures":[{"field":"orders"}],"filters":[{"field":"orderDate","op":"preset","values":["today"]}],"compare":"previousPeriod"}},{"id":"so2","title":"Orders (period)","w":3,"h":2,"spec":{"v":1,"dataset":"sales_orders","type":"kpi","dimensions":[],"measures":[{"field":"orders"}],"filters":[],"compare":"previousPeriod"}},{"id":"so3","title":"Invoiced Sales (period)","w":2,"h":2,"spec":{"v":1,"dataset":"sales_orders","type":"kpi","dimensions":[],"measures":[{"field":"invAmt"}],"filters":[],"compare":"previousPeriod"}},{"id":"so4","title":"Pending Bridge Sync (all dates)","w":2,"h":2,"spec":{"v":1,"dataset":"sales_orders","type":"kpi","dimensions":[],"measures":[{"field":"orders"}],"filters":[{"field":"orderDate","op":"any","values":[]},{"field":"pendingSync","op":"eq","values":["true"]}]}},{"id":"so5","title":"Undelivered Invoices (all dates)","w":2,"h":2,"spec":{"v":1,"dataset":"sales_orders","type":"kpi","dimensions":[],"measures":[{"field":"orders"}],"filters":[{"field":"orderDate","op":"any","values":[]},{"field":"invoiced","op":"eq","values":["true"]},{"field":"delivered","op":"eq","values":["false"]}]}},{"id":"so6","title":"Orders Trend","w":8,"h":4,"spec":{"v":1,"dataset":"sales_orders","type":"line","dimensions":[{"field":"orderDate","bucket":"day"}],"measures":[{"field":"orders"}],"filters":[],"fill":true}},{"id":"so7","title":"Orders by Status","w":4,"h":4,"spec":{"v":1,"dataset":"sales_orders","type":"bar","dimensions":[{"field":"workflowStatus"}],"measures":[{"field":"orders"}],"filters":[]}},{"id":"so8","title":"Invoiced Sales by Branch","w":6,"h":4,"spec":{"v":1,"dataset":"sales_orders","type":"bar","dimensions":[{"field":"branch"}],"measures":[{"field":"invAmt"}],"filters":[],"horizontal":true}},{"id":"so9","title":"Top Customers (Invoiced)","w":6,"h":4,"spec":{"v":1,"dataset":"sales_orders","type":"bar","dimensions":[{"field":"custKey"}],"measures":[{"field":"invAmt"}],"filters":[],"horizontal":true,"limit":10,"others":true}},{"id":"so10","title":"Top Products (Ordered Cases)","w":6,"h":4,"spec":{"v":1,"dataset":"sales_lines","type":"bar","dimensions":[{"field":"cProdNo"}],"measures":[{"field":"orderedCases"}],"filters":[],"horizontal":true,"limit":10,"others":true}},{"id":"so11","title":"Top OOS Products (Cases)","w":6,"h":4,"spec":{"v":1,"dataset":"sales_lines","type":"bar","dimensions":[{"field":"cProdNo"}],"measures":[{"field":"oosCases"}],"filters":[{"field":"oosSynced","op":"eq","values":["true"]}],"horizontal":true,"limit":10,"others":true}},{"id":"so12","title":"Undelivered Invoices","w":12,"h":5,"spec":{"v":1,"dataset":"sales_orders","type":"table","dimensions":[{"field":"soNo"},{"field":"invNo"},{"field":"custKey"},{"field":"branch"},{"field":"invDate","bucket":"day"}],"measures":[{"field":"daysOutstanding"}],"filters":[{"field":"orderDate","op":"any","values":[]},{"field":"invoiced","op":"eq","values":["true"]},{"field":"delivered","op":"eq","values":["false"]}],"sort":{"by":"m0","dir":"desc"}}}]}', 1, 1, '', SYSUTCDATETIME(), 'system');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM SavedReports WHERE IsSystem = 1 AND Kind = 'dashboard' AND Name = 'Sales Overview' AND CreatedBy = 'system'");
        }
    }
}
