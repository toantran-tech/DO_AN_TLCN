using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceLedgerSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "commission_rate_snapshot",
                schema: "washgo",
                table: "refund_requests",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "financial_snapshot_basis",
                schema: "washgo",
                table: "refund_requests",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "merchant_id_snapshot",
                schema: "washgo",
                table: "refund_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "original_payment_transaction_id",
                schema: "washgo",
                table: "refund_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "commission_rate_snapshot",
                schema: "washgo",
                table: "payment_transactions",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "financial_snapshot_basis",
                schema: "washgo",
                table: "payment_transactions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "merchant_id_snapshot",
                schema: "washgo",
                table: "payment_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "breakdown_snapshot_json",
                schema: "washgo",
                table: "merchant_settlements",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_refund_requests_original_payment_transaction_id",
                schema: "washgo",
                table: "refund_requests",
                column: "original_payment_transaction_id");
            // Freeze the observable legacy relationship without inventing a missing commission rate.
            migrationBuilder.Sql("""
                UPDATE washgo.payment_transactions p SET merchant_id_snapshot=o.merchant_id,
                    commission_rate_snapshot=o.commission_rate_applied, financial_snapshot_basis='migration-baseline'
                FROM washgo.orders o WHERE p.order_id=o.id AND p.financial_snapshot_basis IS NULL;
                UPDATE washgo.refund_requests r SET merchant_id_snapshot=o.merchant_id,
                    commission_rate_snapshot=o.commission_rate_applied, financial_snapshot_basis='migration-baseline'
                FROM washgo.orders o WHERE r.order_id=o.id AND r.financial_snapshot_basis IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refund_requests_original_payment_transaction_id",
                schema: "washgo",
                table: "refund_requests");

            migrationBuilder.DropColumn(
                name: "commission_rate_snapshot",
                schema: "washgo",
                table: "refund_requests");

            migrationBuilder.DropColumn(
                name: "financial_snapshot_basis",
                schema: "washgo",
                table: "refund_requests");

            migrationBuilder.DropColumn(
                name: "merchant_id_snapshot",
                schema: "washgo",
                table: "refund_requests");

            migrationBuilder.DropColumn(
                name: "original_payment_transaction_id",
                schema: "washgo",
                table: "refund_requests");

            migrationBuilder.DropColumn(
                name: "commission_rate_snapshot",
                schema: "washgo",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "financial_snapshot_basis",
                schema: "washgo",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "merchant_id_snapshot",
                schema: "washgo",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "breakdown_snapshot_json",
                schema: "washgo",
                table: "merchant_settlements");
        }
    }
}
