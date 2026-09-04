using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierOrderArrivalStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedAtUtc",
                table: "SupplierRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReceivedByEmployeeNumber",
                table: "SupplierRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "SupplierRequests",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "PendingArrival");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierRequests_ReceivedByEmployeeNumber",
                table: "SupplierRequests",
                column: "ReceivedByEmployeeNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierRequests_Status",
                table: "SupplierRequests",
                column: "Status");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SupplierRequests_Status",
                table: "SupplierRequests",
                sql: "[Status] IN ('PendingArrival', 'Received')");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierRequests_AspNetUsers_ReceivedByEmployeeNumber",
                table: "SupplierRequests",
                column: "ReceivedByEmployeeNumber",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Backfill: orders that already existed were raised under the old flow, where stock was
            // taken in separately through POST /inventory/{itemId}/receive (removed in this change).
            // Leaving them Pending Arrival would let a Business Manager "confirm" them and post a
            // SECOND receipt for goods already counted. They are closed as Received instead, stamped
            // with their creation time; ReceivedByEmployeeNumber stays NULL because nobody actually
            // confirmed them under the new workflow.
            migrationBuilder.Sql(@"
UPDATE [SupplierRequests]
SET [Status] = 'Received',
    [ReceivedAtUtc] = [CreatedAtUtc]
WHERE [Status] = 'PendingArrival';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierRequests_AspNetUsers_ReceivedByEmployeeNumber",
                table: "SupplierRequests");

            migrationBuilder.DropIndex(
                name: "IX_SupplierRequests_ReceivedByEmployeeNumber",
                table: "SupplierRequests");

            migrationBuilder.DropIndex(
                name: "IX_SupplierRequests_Status",
                table: "SupplierRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SupplierRequests_Status",
                table: "SupplierRequests");

            migrationBuilder.DropColumn(
                name: "ReceivedAtUtc",
                table: "SupplierRequests");

            migrationBuilder.DropColumn(
                name: "ReceivedByEmployeeNumber",
                table: "SupplierRequests");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SupplierRequests");
        }
    }
}
