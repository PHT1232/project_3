using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWithdrawalPendingStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Requests_Status",
                table: "Requests");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Requests_Status",
                table: "Requests",
                sql: "[Status] IN ('Draft', 'Pending', 'Approved', 'PartiallyApproved', 'Rejected', 'WithdrawalPending', 'Withdrawn', 'CancellationPending', 'Cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Requests_Status",
                table: "Requests");

            // Rolling back removes 'WithdrawalPending' from the vocabulary, so any request parked
            // there must go somewhere legal first or the constraint below cannot be re-created.
            // Pending is the correct landing place: it is the only status a request can reach
            // WithdrawalPending from, and the old schema's withdrawal was a single unilateral
            // step, so an unresolved *request* to withdraw simply did not exist.
            migrationBuilder.Sql(
                "UPDATE [Requests] SET [Status] = 'Pending' WHERE [Status] = 'WithdrawalPending';");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Requests_Status",
                table: "Requests",
                sql: "[Status] IN ('Draft', 'Pending', 'Approved', 'PartiallyApproved', 'Rejected', 'Withdrawn', 'CancellationPending', 'Cancelled')");
        }
    }
}
