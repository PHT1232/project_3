using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SupplierRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByEmployeeNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierRequests_AspNetUsers_CreatedByEmployeeNumber",
                        column: x => x.CreatedByEmployeeNumber,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierRequests_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierRequestItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierRequestId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitCostSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierRequestItems", x => x.Id);
                    table.CheckConstraint("CK_SupplierRequestItems_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_SupplierRequestItems_StationeryItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "StationeryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierRequestItems_SupplierRequests_SupplierRequestId",
                        column: x => x.SupplierRequestId,
                        principalTable: "SupplierRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierRequestItems_ItemId",
                table: "SupplierRequestItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierRequestItems_SupplierRequestId_ItemId",
                table: "SupplierRequestItems",
                columns: new[] { "SupplierRequestId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierRequests_CreatedAtUtc",
                table: "SupplierRequests",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierRequests_CreatedByEmployeeNumber",
                table: "SupplierRequests",
                column: "CreatedByEmployeeNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierRequests_SupplierId",
                table: "SupplierRequests",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierRequestItems");

            migrationBuilder.DropTable(
                name: "SupplierRequests");
        }
    }
}
