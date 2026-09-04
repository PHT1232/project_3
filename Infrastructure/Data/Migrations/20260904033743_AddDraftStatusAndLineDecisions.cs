using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDraftStatusAndLineDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Requests_Status",
                table: "Requests");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Requests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Draft",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Pending");

            migrationBuilder.AddColumn<int>(
                name: "ApprovedQuantity",
                table: "RequestItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Decision",
                table: "RequestItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Requests_Status",
                table: "Requests",
                sql: "[Status] IN ('Draft', 'Pending', 'Approved', 'PartiallyApproved', 'Rejected', 'Withdrawn', 'CancellationPending', 'Cancelled', 'Fulfilled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RequestItems_Decision",
                table: "RequestItems",
                sql: "[Decision] IS NULL OR [Decision] IN ('approved', 'rejected', 'modified')");

            // Data fix: before Draft existed, an unsubmitted request was a Pending row with no
            // "Pending -> Pending" history entry (the submit marker the old UI keyed on). Those
            // rows are drafts and must not sit in an approver's queue. Rows that DO carry the
            // marker were genuinely submitted and stay Pending.
            migrationBuilder.Sql(@"
UPDATE [Requests]
SET [Status] = 'Draft'
WHERE [Status] = 'Pending'
  AND NOT EXISTS (
      SELECT 1 FROM [RequestStatusHistory] h
      WHERE h.[RequestId] = [Requests].[Id]
        AND h.[FromStatus] = 'Pending'
        AND h.[ToStatus] = 'Pending');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse of the Up() data fix: the pre-Draft schema's CHECK does not allow 'Draft'.
            migrationBuilder.Sql("UPDATE [Requests] SET [Status] = 'Pending' WHERE [Status] = 'Draft';");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Requests_Status",
                table: "Requests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RequestItems_Decision",
                table: "RequestItems");

            migrationBuilder.DropColumn(
                name: "ApprovedQuantity",
                table: "RequestItems");

            migrationBuilder.DropColumn(
                name: "Decision",
                table: "RequestItems");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Requests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Draft");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Requests_Status",
                table: "Requests",
                sql: "[Status] IN ('Pending', 'Approved', 'PartiallyApproved', 'Rejected', 'Withdrawn', 'CancellationPending', 'Cancelled', 'Fulfilled')");
        }
    }
}
