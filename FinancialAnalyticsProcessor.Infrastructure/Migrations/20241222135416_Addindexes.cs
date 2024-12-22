using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinancialAnalyticsProcessor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Addindexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddPrimaryKey(
                         name: "PK_Transactions",
                         table: "Transactions",
                         column: "TransactionId");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Transactions",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Transactions",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_Amount",
                table: "Transactions",
                column: "Amount");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_Category",
                table: "Transactions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_Description",
                table: "Transactions",
                column: "Description");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_UserId_Date",
                table: "Transactions",
                columns: new[] { "UserId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transaction_Amount",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transaction_Category",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transaction_Description",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transaction_UserId_Date",
                table: "Transactions");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
