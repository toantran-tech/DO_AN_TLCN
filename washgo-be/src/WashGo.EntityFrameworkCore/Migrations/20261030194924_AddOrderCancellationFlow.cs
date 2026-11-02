using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCancellationFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "cancel_fee_paid_amount",
                schema: "washgo",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "cancellation_from_status",
                schema: "washgo",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cancellation_refund_amount",
                schema: "washgo",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_goods_returned_at",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cancelled_goods_status",
                schema: "washgo",
                table: "orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancel_fee_paid_amount",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "cancellation_from_status",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "cancellation_refund_amount",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "cancelled_goods_returned_at",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "cancelled_goods_status",
                schema: "washgo",
                table: "orders");
        }
    }
}
