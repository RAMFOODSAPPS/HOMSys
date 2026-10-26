using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <summary>
    /// The BMS widgets on saved dashboards (Collections Overview bm1–bm5) count only BMS ORs that were encoded in
    /// HOMSys, so they tally with the HOMSys collection widgets (user 2026-10-07). Adds the dataset's existing
    /// "inHomsys" flag as a filter to every bms_collections widget that doesn't have it yet; other edits are kept.
    /// </summary>
    public partial class FilterBmsWidgetsToHomsys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @id int, @i int, @p nvarchar(100);
                DECLARE w CURSOR LOCAL STATIC FOR
                    SELECT r.Id, CAST(x.[key] AS int)
                      FROM SavedReports r CROSS APPLY OPENJSON(r.DefinitionJson, '$.widgets') x
                     WHERE r.Kind = 'dashboard' AND JSON_VALUE(x.value, '$.spec.dataset') = 'bms_collections'
                       AND NOT EXISTS (SELECT 1 FROM OPENJSON(x.value, '$.spec.filters') f WHERE JSON_VALUE(f.value, '$.field') = 'inHomsys');
                OPEN w;
                FETCH NEXT FROM w INTO @id, @i;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @p = CONCAT('append $.widgets[', @i, '].spec.filters');
                    UPDATE SavedReports
                       SET DefinitionJson = JSON_MODIFY(DefinitionJson, @p, JSON_QUERY(N'{"field":"inHomsys","op":"eq","values":["true"]}'))
                     WHERE Id = @id;
                    FETCH NEXT FROM w INTO @id, @i;
                END
                CLOSE w; DEALLOCATE w;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @id int, @i int, @p nvarchar(100), @rest nvarchar(max);
                DECLARE w CURSOR LOCAL STATIC FOR
                    SELECT r.Id, CAST(x.[key] AS int)
                      FROM SavedReports r CROSS APPLY OPENJSON(r.DefinitionJson, '$.widgets') x
                     WHERE r.Kind = 'dashboard' AND JSON_VALUE(x.value, '$.spec.dataset') = 'bms_collections'
                       AND EXISTS (SELECT 1 FROM OPENJSON(x.value, '$.spec.filters') f WHERE JSON_VALUE(f.value, '$.field') = 'inHomsys');
                OPEN w;
                FETCH NEXT FROM w INTO @id, @i;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @p = CONCAT('$.widgets[', @i, '].spec.filters');
                    SELECT @rest = CONCAT('[', STRING_AGG(f.value, ','), ']')
                      FROM SavedReports r CROSS APPLY OPENJSON(r.DefinitionJson, @p) f
                     WHERE r.Id = @id AND ISNULL(JSON_VALUE(f.value, '$.field'), '') <> 'inHomsys';
                    UPDATE SavedReports SET DefinitionJson = JSON_MODIFY(DefinitionJson, @p, JSON_QUERY(ISNULL(@rest, '[]'))) WHERE Id = @id;
                    FETCH NEXT FROM w INTO @id, @i;
                END
                CLOSE w; DEALLOCATE w;
                """);
        }
    }
}
