using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMSys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BankAccountEditDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "BankAccounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "BankAccounts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomsBankAc",
                table: "BankAccounts",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            // Synced accounts so far carry BANKS4W's number unchanged: that is also HOMS's (DEPBANK).
            migrationBuilder.Sql("UPDATE BankAccounts SET HomsBankAc = AccountNo WHERE Synced = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "HomsBankAc",
                table: "BankAccounts");
        }
    }
}
