using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRfcWidgetsToSalesOverview : Migration
    {
        // RFC (Return From Customer) widgets for the "Sales Overview" system
        // dashboard (Home): KPI row, by-branch / top-customer bars and a list of
        // orders with RFCs. APPENDED to the existing widget array (never replaces
        // the definition, so admin customisations survive) and idempotent — skipped
        // if an "rfc1" widget is already there. Uses the sales_orders measures
        // rfcAmt (VAT-inclusive, SPAMT+TAX), netInvAmt and the hasRfc flag.
        private const string Widgets = """
            [
             {"id":"rfc1","title":"RFC Amount (period)","w":3,"h":2,"spec":{"v":1,"dataset":"sales_orders","type":"kpi","dimensions":[],"measures":[{"field":"rfcAmt"}],"filters":[],"compare":"previousPeriod"}},
             {"id":"rfc2","title":"Net Invoiced (period)","w":3,"h":2,"spec":{"v":1,"dataset":"sales_orders","type":"kpi","dimensions":[],"measures":[{"field":"netInvAmt"}],"filters":[],"compare":"previousPeriod"}},
             {"id":"rfc3","title":"Orders with RFC (period)","w":3,"h":2,"spec":{"v":1,"dataset":"sales_orders","type":"kpi","dimensions":[],"measures":[{"field":"orders"}],"filters":[{"field":"hasRfc","op":"eq","values":["true"]}],"compare":"previousPeriod"}},
             {"id":"rfc4","title":"Full RFC Orders (period)","w":3,"h":2,"spec":{"v":1,"dataset":"sales_orders","type":"kpi","dimensions":[],"measures":[{"field":"orders"}],"filters":[{"field":"workflowStatus","op":"in","values":["Full RFC"]}],"compare":"previousPeriod"}},
             {"id":"rfc5","title":"RFC Amount by Branch","w":6,"h":4,"spec":{"v":1,"dataset":"sales_orders","type":"bar","dimensions":[{"field":"branch"}],"measures":[{"field":"rfcAmt"}],"filters":[{"field":"hasRfc","op":"eq","values":["true"]}],"horizontal":true}},
             {"id":"rfc6","title":"Top Customers by RFC Amount","w":6,"h":4,"spec":{"v":1,"dataset":"sales_orders","type":"bar","dimensions":[{"field":"custKey"}],"measures":[{"field":"rfcAmt"}],"filters":[{"field":"hasRfc","op":"eq","values":["true"]}],"horizontal":true,"limit":10,"others":true}},
             {"id":"rfc7","title":"Orders with RFC Returns (period)","w":12,"h":5,"spec":{"v":1,"dataset":"sales_orders","type":"table","dimensions":[{"field":"soNo"},{"field":"invNo"},{"field":"custKey"},{"field":"branch"},{"field":"workflowStatus"}],"measures":[{"field":"invAmt"},{"field":"rfcAmt"},{"field":"netInvAmt"}],"filters":[{"field":"hasRfc","op":"eq","values":["true"]}],"sort":{"by":"m1","dir":"desc"}}}
            ]
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DECLARE @w nvarchar(max) = N'{Widgets.Replace("'", "''")}';
                UPDATE SavedReports
                   SET DefinitionJson = JSON_MODIFY(DefinitionJson, '$.widgets',
                           JSON_QUERY('[' + (SELECT STRING_AGG(value, ',') WITHIN GROUP (ORDER BY o, k)
                                             FROM (SELECT value, 0 AS o, CAST([key] AS int) AS k FROM OPENJSON(DefinitionJson, '$.widgets')
                                                   UNION ALL
                                                   SELECT value, 1, CAST([key] AS int) FROM OPENJSON(@w)) x) + ']'))
                 WHERE IsSystem = 1 AND Kind = 'dashboard' AND Name = 'Sales Overview'
                   AND NOT EXISTS (SELECT 1 FROM OPENJSON(DefinitionJson, '$.widgets') WHERE JSON_VALUE(value, '$.id') = 'rfc1');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE SavedReports
                   SET DefinitionJson = JSON_MODIFY(DefinitionJson, '$.widgets',
                           JSON_QUERY('[' + ISNULL((SELECT STRING_AGG(value, ',') WITHIN GROUP (ORDER BY CAST([key] AS int))
                                                    FROM OPENJSON(DefinitionJson, '$.widgets')
                                                    WHERE JSON_VALUE(value, '$.id') NOT LIKE 'rfc[1-7]'), '') + ']'))
                 WHERE IsSystem = 1 AND Kind = 'dashboard' AND Name = 'Sales Overview';
                """);
        }
    }
}
