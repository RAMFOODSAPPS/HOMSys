using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TasafProgramDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                table: "Tasafs",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "Tasafs",
                type: "date",
                nullable: true);

            // Month/year → dates: start = 1st of the month, end = last day of the month.
            migrationBuilder.Sql("""
                UPDATE Tasafs SET
                    StartDate = CASE WHEN StartYear BETWEEN 2000 AND 2100 AND StartMonth BETWEEN 1 AND 12
                                     THEN DATEFROMPARTS(StartYear, StartMonth, 1) END,
                    EndDate   = CASE WHEN EndYear BETWEEN 2000 AND 2100 AND EndMonth BETWEEN 1 AND 12
                                     THEN EOMONTH(DATEFROMPARTS(EndYear, EndMonth, 1)) END;
                """);

            migrationBuilder.DropColumn(
                name: "EndMonth",
                table: "Tasafs");

            migrationBuilder.DropColumn(
                name: "EndYear",
                table: "Tasafs");

            migrationBuilder.DropColumn(
                name: "StartMonth",
                table: "Tasafs");

            migrationBuilder.DropColumn(
                name: "StartYear",
                table: "Tasafs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Tasafs");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Tasafs");

            migrationBuilder.AddColumn<int>(
                name: "EndMonth",
                table: "Tasafs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EndYear",
                table: "Tasafs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StartMonth",
                table: "Tasafs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StartYear",
                table: "Tasafs",
                type: "int",
                nullable: true);
        }
    }
}
