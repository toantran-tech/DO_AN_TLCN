using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class NullableCommissionSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "commission_rate_applied",
                schema: "washgo",
                table: "orders",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)",
                oldPrecision: 5,
                oldScale: 2);

            migrationBuilder.AddColumn<DateTime>(
                name: "commission_locked_at",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            // Until now 0 also meant "never locked". Keep 0 only where it can be a real rate (the Merchant's own rate is 0);
            // otherwise mark the order as missing a snapshot instead of inventing a rate. Lock time is set for orders
            // assigned after snapshots existed (the assignment entry on the order history).
            migrationBuilder.Sql(@"
UPDATE washgo.orders o SET commission_rate_applied = NULL
WHERE o.commission_rate_applied = 0
  AND (o.merchant_id IS NULL OR NOT EXISTS (SELECT 1 FROM washgo.merchants m WHERE m.user_id = o.merchant_id AND m.commission_rate = 0));
UPDATE washgo.orders o SET commission_locked_at = to_timestamp(h.created_at)
FROM (SELECT order_id, MAX(created_at) AS created_at FROM washgo.order_status_history
      WHERE changed_by_type = 'Admin' AND note LIKE 'Admin phân công%' GROUP BY order_id) h
WHERE h.order_id = o.id AND o.commission_rate_applied IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "commission_locked_at",
                schema: "washgo",
                table: "orders");

            migrationBuilder.AlterColumn<decimal>(
                name: "commission_rate_applied",
                schema: "washgo",
                table: "orders",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)",
                oldPrecision: 5,
                oldScale: 2,
                oldNullable: true);
        }
    }
}
