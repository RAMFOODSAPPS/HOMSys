using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <summary>
    /// Sales group per customer chain, from Book1.xlsx "MODERN TRADE VERSION" (NKA / RKA).
    /// LKA Others / RDP Others follow the GT LKA version (LKA / GT). Only chains with no
    /// group yet are set, so assignments made on TASAF Setup are never overwritten.
    /// </summary>
    public partial class TasafChainSalesGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE c SET [SalesGroupId] = g.[Id]
                FROM [TasafChains] c
                JOIN (VALUES
                    ('SVI', 'NKA'), ('Puregold', 'NKA'), ('Robinson', 'NKA'), ('Waltermart', 'NKA'), ('Landmark', 'NKA'),
                    ('Suysing', 'RKA'), ('Super 8', 'RKA'), ('Citi Mart', 'RKA'), ('LCC', 'RKA'), ('CSI', 'RKA'),
                    ('RCS Chain', 'RKA'), ('Savewise', 'RKA'), ('Prince', 'RKA'), ('Metro Gaisano', 'RKA'),
                    ('NCCC', 'RKA'), ('KCC', 'RKA'), ('Gaisano Grand', 'RKA'), ('Gaisano Capital', 'RKA'),
                    ('Gaisano Mall', 'RKA'),
                    ('LKA Others', 'LKA'), ('RDP Others', 'GT')
                ) v([Chain], [Group]) ON c.[Name] = v.[Chain]
                JOIN [TasafSalesGroups] g ON g.[Code] = v.[Group]
                WHERE c.[SalesGroupId] IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data only — the assignments stay (they're editable on TASAF Setup).
        }
    }
}
