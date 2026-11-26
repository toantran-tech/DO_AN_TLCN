using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderActualsAndRealtime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "actuals_confirmed_at",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "actuals_confirmed_by",
                schema: "washgo",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "actuals_discount_type_snapshot",
                schema: "washgo",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "actuals_discount_value_snapshot",
                schema: "washgo",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "actuals_max_discount_amount_snapshot",
                schema: "washgo",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "actuals_min_order_amount_snapshot",
                schema: "washgo",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "actuals_note",
                schema: "washgo",
                table: "orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "actual_quantity",
                schema: "washgo",
                table: "order_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "actual_sub_total",
                schema: "washgo",
                table: "order_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "unit_snapshot",
                schema: "washgo",
                table: "order_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(OrderChangeOutboxSql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(OrderChangeOutboxSql.Down);

            migrationBuilder.DropColumn(
                name: "actuals_confirmed_at",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "actuals_confirmed_by",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "actuals_discount_type_snapshot",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "actuals_discount_value_snapshot",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "actuals_max_discount_amount_snapshot",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "actuals_min_order_amount_snapshot",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "actuals_note",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "actual_quantity",
                schema: "washgo",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "actual_sub_total",
                schema: "washgo",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "unit_snapshot",
                schema: "washgo",
                table: "order_items");
        }
    }
}
