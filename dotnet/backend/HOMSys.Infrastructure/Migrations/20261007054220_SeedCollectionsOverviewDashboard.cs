using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedCollectionsOverviewDashboard : Migration
    {
        // "Collections Overview" — the Finance Manager's starting dashboard over the
        // collections / posted_invoices / bank_lines datasets (finance-only). Not a
        // system item (those show to every analytics user): owned by admin, shared with
        // the Finance Manager role, so finance can copy and customise it.
        private const string Definition = """
            {"v":1,"filters":[{"field":"receiptDate","op":"preset","values":["mtd"]},{"field":"branch","op":"in","values":[]}],"widgets":[
             {"id":"c1","title":"Collected (period)","w":3,"h":2,"spec":{"v":1,"dataset":"collections","type":"kpi","dimensions":[],"measures":[{"field":"amount"}],"filters":[],"compare":"previousPeriod"}},
             {"id":"c2","title":"Waiting Verification (all dates)","w":3,"h":2,"spec":{"v":1,"dataset":"collections","type":"kpi","dimensions":[],"measures":[{"field":"forVerificationAmt"}],"filters":[{"field":"receiptDate","op":"any","values":[]}]}},
             {"id":"c3","title":"Not Yet on Bank Statement (all dates)","w":3,"h":2,"spec":{"v":1,"dataset":"collections","type":"kpi","dimensions":[],"measures":[{"field":"notDepositedAmt"}],"filters":[{"field":"receiptDate","op":"any","values":[]}]}},
             {"id":"c4","title":"PDCs Not Yet Due (all dates)","w":3,"h":2,"spec":{"v":1,"dataset":"collections","type":"kpi","dimensions":[],"measures":[{"field":"immaturePdcAmt"}],"filters":[{"field":"receiptDate","op":"any","values":[]}]}},
             {"id":"c5","title":"Verified (period)","w":3,"h":2,"spec":{"v":1,"dataset":"collections","type":"kpi","dimensions":[],"measures":[{"field":"verifiedAmt"}],"filters":[],"compare":"previousPeriod"}},
             {"id":"c6","title":"On-account / Unapplied (period)","w":3,"h":2,"spec":{"v":1,"dataset":"collections","type":"kpi","dimensions":[],"measures":[{"field":"unapplied"}],"filters":[]}},
             {"id":"c7","title":"Bounced PDCs (period)","w":3,"h":2,"spec":{"v":1,"dataset":"collections","type":"kpi","dimensions":[],"measures":[{"field":"bouncedAmt"}],"filters":[],"compare":"previousPeriod"}},
             {"id":"c8","title":"Open Balance — Synced Invoices","w":3,"h":2,"spec":{"v":1,"dataset":"posted_invoices","type":"kpi","dimensions":[],"measures":[{"field":"balance"}],"filters":[{"field":"invDate","op":"any","values":[]}]}},
             {"id":"c9","title":"Collections Trend","w":8,"h":4,"spec":{"v":1,"dataset":"collections","type":"line","dimensions":[{"field":"receiptDate","bucket":"day"}],"measures":[{"field":"amount"}],"filters":[],"fill":true}},
             {"id":"c10","title":"Receipts by Verification Status","w":4,"h":4,"spec":{"v":1,"dataset":"collections","type":"bar","dimensions":[{"field":"verificationStatus"}],"measures":[{"field":"collections"}],"filters":[]}},
             {"id":"c11","title":"Collected by Branch","w":6,"h":4,"spec":{"v":1,"dataset":"collections","type":"bar","dimensions":[{"field":"branch"}],"measures":[{"field":"amount"}],"filters":[],"horizontal":true}},
             {"id":"c12","title":"Top Customers by Collection","w":6,"h":4,"spec":{"v":1,"dataset":"collections","type":"bar","dimensions":[{"field":"custKey"}],"measures":[{"field":"amount"}],"filters":[],"horizontal":true,"limit":10,"others":true}},
             {"id":"c13","title":"Open Balance by Aging","w":6,"h":4,"spec":{"v":1,"dataset":"posted_invoices","type":"bar","dimensions":[{"field":"aging"}],"measures":[{"field":"balance"}],"filters":[{"field":"invDate","op":"any","values":[]},{"field":"open","op":"eq","values":["true"]}]}},
             {"id":"c14","title":"PDC Pipeline (all dates)","w":6,"h":4,"spec":{"v":1,"dataset":"collections","type":"bar","dimensions":[{"field":"pdcState"}],"measures":[{"field":"amount","label":"Amount"}],"filters":[{"field":"receiptDate","op":"any","values":[]},{"field":"isPdc","op":"eq","values":["true"]},{"field":"voided","op":"eq","values":["false"]}]}},
             {"id":"c15","title":"Waiting Verification","w":12,"h":5,"spec":{"v":1,"dataset":"collections","type":"table","dimensions":[{"field":"branch"},{"field":"receiptNo"},{"field":"custKey"},{"field":"submittedAt","bucket":"day"}],"measures":[{"field":"forVerificationAmt"},{"field":"waitingDays"}],"filters":[{"field":"receiptDate","op":"any","values":[]},{"field":"verificationStatus","op":"in","values":["ForVerification"]}],"sort":{"by":"m1","dir":"desc"}}},
             {"id":"c16","title":"Top Overdue Customers","w":6,"h":5,"spec":{"v":1,"dataset":"posted_invoices","type":"table","dimensions":[{"field":"custKey"},{"field":"branch"}],"measures":[{"field":"overdueBalance"},{"field":"daysPastDue"}],"filters":[{"field":"invDate","op":"any","values":[]},{"field":"open","op":"eq","values":["true"]}],"sort":{"by":"m0","dir":"desc"},"limit":20}},
             {"id":"c17","title":"Unidentified Bank Credits","w":6,"h":5,"spec":{"v":1,"dataset":"bank_lines","type":"table","dimensions":[{"field":"account"},{"field":"txnDate","bucket":"day"},{"field":"description"}],"measures":[{"field":"unmatchedCredit"}],"filters":[{"field":"isCredit","op":"eq","values":["true"]},{"field":"matched","op":"eq","values":["false"]}],"sort":{"by":"m0","dir":"desc"}}}
            ]}
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                IF NOT EXISTS (SELECT 1 FROM SavedReports WHERE Kind = 'dashboard' AND Name = 'Collections Overview' AND CreatedBy = 'system')
                INSERT INTO SavedReports (Kind, Name, Description, DatasetKey, Visual, DefinitionJson, OwnerUserId, IsSystem, SharedRoleIds, CreatedAt, CreatedBy)
                VALUES ('dashboard', 'Collections Overview',
                        'Collected, waiting verification, not yet on the bank statement, PDC pipeline, open balances and unidentified bank credits.',
                        NULL, NULL, N'{Definition.Replace("'", "''")}', 1, 0,
                        ISNULL((SELECT CAST(Id AS nvarchar(10)) FROM Roles WHERE Name = 'Finance Manager'), ''),
                        SYSUTCDATETIME(), 'system');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM SavedReports WHERE Kind = 'dashboard' AND Name = 'Collections Overview' AND CreatedBy = 'system'");
        }
    }
}
