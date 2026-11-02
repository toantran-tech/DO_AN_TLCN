using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddQrPinDeliveryActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "customer_deposit_lockout_until",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "customer_deposit_pin_attempt_count",
                schema: "washgo",
                table: "orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "customer_pickup_lockout_until",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "customer_pickup_pin_attempt_count",
                schema: "washgo",
                table: "orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "deposited_at",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "locker_overdue_fee_at_storage_transfer",
                schema: "washgo",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "paid_at",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "pickup_session_expires_at",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pickup_session_token_hash",
                schema: "washgo",
                table: "orders",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "shipper_task_pin_code",
                schema: "washgo",
                table: "orders",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "shipper_task_qr_string",
                schema: "washgo",
                table: "orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "task_pin_attempt_count",
                schema: "washgo",
                table: "orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "task_pin_lockout_until",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "customer_deposit_lockout_until",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "customer_deposit_pin_attempt_count",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "customer_pickup_lockout_until",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "customer_pickup_pin_attempt_count",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "deposited_at",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "locker_overdue_fee_at_storage_transfer",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "paid_at",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "pickup_session_expires_at",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "pickup_session_token_hash",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "shipper_task_pin_code",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "shipper_task_qr_string",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "task_pin_attempt_count",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "task_pin_lockout_until",
                schema: "washgo",
                table: "orders");
        }
    }
}
