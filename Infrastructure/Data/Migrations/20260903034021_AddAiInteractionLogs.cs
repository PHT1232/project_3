using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiInteractionLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiInteractionLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeNumber = table.Column<int>(type: "int", nullable: false),
                    Feature = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LatencyMs = table.Column<int>(type: "int", nullable: false),
                    WasFallback = table.Column<bool>(type: "bit", nullable: false),
                    FallbackReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InputTokens = table.Column<long>(type: "bigint", nullable: true),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: true),
                    UserText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DraftItemCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInteractionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiInteractionLogs_AspNetUsers_EmployeeNumber",
                        column: x => x.EmployeeNumber,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiInteractionLogs_CreatedAtUtc",
                table: "AiInteractionLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AiInteractionLogs_EmployeeNumber_CreatedAtUtc",
                table: "AiInteractionLogs",
                columns: new[] { "EmployeeNumber", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiInteractionLogs");
        }
    }
}
