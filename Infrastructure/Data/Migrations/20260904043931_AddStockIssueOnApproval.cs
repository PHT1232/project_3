using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStockIssueOnApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Requests_Status",
                table: "Requests");

            migrationBuilder.AddColumn<int>(
                name: "RequestId",
                table: "StockTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_RequestId",
                table: "StockTransactions",
                column: "RequestId");

            // Must run before the narrowed CHECK goes back on, or any surviving row rejects it.
            // Nothing in the application ever set 'Fulfilled' — only the never-wired demo seeder
            // could have — so in practice this updates zero rows. 'Approved' is the correct
            // landing status: it is what the approval transition produces now that approval is
            // also what moves the stock.
            migrationBuilder.Sql(
                "UPDATE [Requests] SET [Status] = 'Approved' WHERE [Status] = 'Fulfilled';");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Requests_Status",
                table: "Requests",
                sql: "[Status] IN ('Draft', 'Pending', 'Approved', 'PartiallyApproved', 'Rejected', 'Withdrawn', 'CancellationPending', 'Cancelled')");

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransactions_Requests_RequestId",
                table: "StockTransactions",
                column: "RequestId",
                principalTable: "Requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockTransactions_Requests_RequestId",
                table: "StockTransactions");

            migrationBuilder.DropIndex(
                name: "IX_StockTransactions_RequestId",
                table: "StockTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Requests_Status",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "StockTransactions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Requests_Status",
                table: "Requests",
                sql: "[Status] IN ('Draft', 'Pending', 'Approved', 'PartiallyApproved', 'Rejected', 'Withdrawn', 'CancellationPending', 'Cancelled', 'Fulfilled')");
        }
    }
}
