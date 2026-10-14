using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HOMSys.Infrastructure.Migrations
{
    /// <summary>
    /// TASAF product scope: Product Category (multi; Book2 list) → Sub-Category (multi; a product master
    /// category code, e.g. 2a, with its SubCat / GroupDesc snapshotted as Sub-Category / Segment). Copies
    /// existing TASAFs' Product Group / Category picks first, then drops the old columns and TasafCategories.
    /// </summary>
    public partial class TasafProductHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TasafProductCategoryLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TasafId = table.Column<int>(type: "int", nullable: false),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    ProductGroupId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafProductCategoryLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafProductCategoryLinks_TasafProductGroups_ProductGroupId",
                        column: x => x.ProductGroupId,
                        principalTable: "TasafProductGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TasafProductCategoryLinks_Tasafs_TasafId",
                        column: x => x.TasafId,
                        principalTable: "Tasafs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TasafSubCategoryLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TasafId = table.Column<int>(type: "int", nullable: false),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Segment = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafSubCategoryLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafSubCategoryLinks_Tasafs_TasafId",
                        column: x => x.TasafId,
                        principalTable: "Tasafs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 10,
                column: "IsActive",
                value: false);

            migrationBuilder.InsertData(
                table: "TasafProductGroups",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "Description", "IsActive", "Name", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 11, "10", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "", true, "Non Food & Toll Pack", 11, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_TasafProductCategoryLinks_ProductGroupId",
                table: "TasafProductCategoryLinks",
                column: "ProductGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafProductCategoryLinks_TasafId",
                table: "TasafProductCategoryLinks",
                column: "TasafId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafSubCategoryLinks_TasafId",
                table: "TasafSubCategoryLinks",
                column: "TasafId");

            // Before the old columns go: carry every TASAF's product scope over.
            migrationBuilder.Sql("""
                -- Product Categories = the old Product Group + Secondary Product Group.
                INSERT INTO [TasafProductCategoryLinks] ([TasafId], [LineNo], [ProductGroupId])
                SELECT t.[Id], ROW_NUMBER() OVER (PARTITION BY t.[Id] ORDER BY v.[n]), v.[g]
                FROM [Tasafs] t
                CROSS APPLY (VALUES (1, t.[ProductGroupId]), (2, t.[ProductGroup2Id])) v([n], [g])
                WHERE v.[g] IS NOT NULL AND NOT (v.[n] = 2 AND v.[g] = t.[ProductGroupId]);

                -- Sub-Categories = the old Category + Secondary Category, by code (1A → product master
                -- category 1a), with its Sub-Category (SubCat) and Segment (GroupDesc) names as a snapshot.
                -- An old whole-group category (03, 07, 09) or CORP has no code there = the whole category.
                INSERT INTO [TasafSubCategoryLinks] ([TasafId], [LineNo], [Code], [Name], [Segment])
                SELECT t.[Id], ROW_NUMBER() OVER (PARTITION BY t.[Id] ORDER BY MIN(v.[n])),
                       RTRIM(pc.[CategoryCode]), RTRIM(MAX(pc.[SubCat])), RTRIM(MAX(pc.[GroupDesc]))
                FROM [Tasafs] t
                CROSS APPLY (VALUES (1, t.[CategoryId]), (2, t.[Category2Id])) v([n], [cid])
                JOIN [TasafCategories] c ON c.[Id] = v.[cid]
                JOIN [ProductCategories] pc ON RTRIM(pc.[CategoryCode]) = RTRIM(c.[Code])
                GROUP BY t.[Id], pc.[CategoryCode];
                """);

            // Saved TASAF form layout (Form Designer): keep the admin's placements under the new field
            // keys — productGroup → productCategory, productGroup2 → subCategory, category → segment
            // (category2 is gone; the layout ignores keys it no longer knows).
            migrationBuilder.Sql("""
                UPDATE [FormLayouts] SET [LayoutJson] =
                    REPLACE(REPLACE(REPLACE([LayoutJson],
                        '"key":"productGroup2"', '"key":"subCategory"'),
                        '"key":"productGroup"', '"key":"productCategory"'),
                        '"key":"category"', '"key":"segment"')
                WHERE [FormKey] = 'tasaf';
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Tasafs_TasafCategories_Category2Id",
                table: "Tasafs");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasafs_TasafCategories_CategoryId",
                table: "Tasafs");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasafs_TasafProductGroups_ProductGroup2Id",
                table: "Tasafs");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasafs_TasafProductGroups_ProductGroupId",
                table: "Tasafs");

            migrationBuilder.DropTable(
                name: "TasafCategories");

            migrationBuilder.DropIndex(
                name: "IX_Tasafs_Category2Id",
                table: "Tasafs");

            migrationBuilder.DropIndex(
                name: "IX_Tasafs_CategoryId",
                table: "Tasafs");

            migrationBuilder.DropIndex(
                name: "IX_Tasafs_ProductGroup2Id",
                table: "Tasafs");

            migrationBuilder.DropIndex(
                name: "IX_Tasafs_ProductGroupId",
                table: "Tasafs");

            migrationBuilder.DropColumn(
                name: "Category2Id",
                table: "Tasafs");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Tasafs");

            migrationBuilder.DropColumn(
                name: "ProductGroup2Id",
                table: "Tasafs");

            migrationBuilder.DropColumn(
                name: "ProductGroupId",
                table: "Tasafs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TasafProductCategoryLinks");

            migrationBuilder.DropTable(
                name: "TasafSubCategoryLinks");

            migrationBuilder.DeleteData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.AddColumn<int>(
                name: "Category2Id",
                table: "Tasafs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "Tasafs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductGroup2Id",
                table: "Tasafs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductGroupId",
                table: "Tasafs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TasafCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductGroupId = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasafCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasafCategories_TasafProductGroups_ProductGroupId",
                        column: x => x.ProductGroupId,
                        principalTable: "TasafProductGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "TasafCategories",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "Description", "IsActive", "Name", "ProductGroupId", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "1A", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "01 -Tom Sauce/Paste- 1a -Tomato Sauce - Tomato Sauce", true, "Tomato Sauce", 1, 1, null, null },
                    { 2, "1B", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "01 -Tom Sauce/Paste- 1b -Tomato Paste -Tomato Paste", true, "Tomato Paste", 1, 2, null, null },
                    { 3, "2A", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "02 -Peas & Beans- 2a -Peas -Green Peas", true, "Green Peas", 2, 3, null, null },
                    { 4, "2B", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "02 -Peas & Beans- 2b -Peas -Garbanzos", true, "Garbanzos", 2, 4, null, null },
                    { 5, "2C", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "02 -Peas & Beans- 2c -Beans -Black Beans", true, "Black Beans", 2, 5, null, null },
                    { 6, "03", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "03 -Pasta & Sauces", true, "Pasta & Sauces", 3, 6, null, null },
                    { 7, "04", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "04 -Mitra", true, "Mitra", 4, 7, null, null },
                    { 8, "5A", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "05 -Raisins- 5a -Raisins -Ram Raisins", true, "Ram Raisins", 5, 8, null, null },
                    { 9, "5B", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "05 -Raisins- 5b -Prunes -Prunes", true, "Prunes", 5, 9, null, null },
                    { 10, "6A", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "06 -Pickles- 6a -Pickles -Ram Pickles", true, "Ram Pickles", 6, 10, null, null },
                    { 11, "07", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "07 -Fruits & Vegetables", true, "Fruits & Vegetables", 7, 11, null, null },
                    { 12, "8A", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "08 -Baking & Dry Goods- 8a -Baking Goods -Bread Crumbs", true, "Bread Crumbs", 8, 12, null, null },
                    { 13, "09", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "09 -Condiments", true, "Condiments", 9, 13, null, null },
                    { 14, "CORP", new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "TOTAL RAM / CORPORATE PROGRAM", true, "TOTAL RAM / CORPORATE PROGRAM", 10, 14, null, null }
                });

            migrationBuilder.UpdateData(
                table: "TasafProductGroups",
                keyColumn: "Id",
                keyValue: 10,
                column: "IsActive",
                value: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_Category2Id",
                table: "Tasafs",
                column: "Category2Id");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_CategoryId",
                table: "Tasafs",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_ProductGroup2Id",
                table: "Tasafs",
                column: "ProductGroup2Id");

            migrationBuilder.CreateIndex(
                name: "IX_Tasafs_ProductGroupId",
                table: "Tasafs",
                column: "ProductGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TasafCategories_Code",
                table: "TasafCategories",
                column: "Code",
                unique: true,
                filter: "[Code] != ''");

            migrationBuilder.CreateIndex(
                name: "IX_TasafCategories_Name",
                table: "TasafCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasafCategories_ProductGroupId",
                table: "TasafCategories",
                column: "ProductGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasafs_TasafCategories_Category2Id",
                table: "Tasafs",
                column: "Category2Id",
                principalTable: "TasafCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasafs_TasafCategories_CategoryId",
                table: "Tasafs",
                column: "CategoryId",
                principalTable: "TasafCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasafs_TasafProductGroups_ProductGroup2Id",
                table: "Tasafs",
                column: "ProductGroup2Id",
                principalTable: "TasafProductGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasafs_TasafProductGroups_ProductGroupId",
                table: "Tasafs",
                column: "ProductGroupId",
                principalTable: "TasafProductGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
