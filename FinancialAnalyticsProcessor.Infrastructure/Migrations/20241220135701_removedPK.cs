using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinancialAnalyticsProcessor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class removedPK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            
            migrationBuilder.DropPrimaryKey(
                name: "PK_Transactions", 
                table: "Transactions"
            );

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "Date",
                table: "Transactions",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddPrimaryKey(
                           name: "PK_Transactions", 
                           table: "Transactions", 
                           column: "TransactionId" 
                           );

            migrationBuilder.AlterColumn<DateTime>(
                name: "Date",
                table: "Transactions",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");
        }
    }
}
