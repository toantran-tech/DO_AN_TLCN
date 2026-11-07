using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "receipt_issued_at",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "receipt_number",
                schema: "washgo",
                table: "orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_receipt_number",
                schema: "washgo",
                table: "orders",
                column: "receipt_number",
                unique: true,
                filter: "receipt_number IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_receipt_number",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "receipt_issued_at",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "receipt_number",
                schema: "washgo",
                table: "orders");
        }
    }
}
