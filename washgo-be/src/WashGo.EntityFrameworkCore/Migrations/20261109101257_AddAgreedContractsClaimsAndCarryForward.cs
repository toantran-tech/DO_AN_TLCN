using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddAgreedContractsClaimsAndCarryForward : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "settlement_id",
                schema: "washgo",
                table: "refund_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "settlement_id",
                schema: "washgo",
                table: "payment_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "carried_to_settlement_id",
                schema: "washgo",
                table: "merchant_settlements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "carry_forward_amount",
                schema: "washgo",
                table: "merchant_settlements",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "period_payout_amount",
                schema: "washgo",
                table: "merchant_settlements",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "compensation_paid_by",
                schema: "washgo",
                table: "incidents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "compensation_proof_image_urls",
                schema: "washgo",
                table: "incidents",
                type: "text[]",
                nullable: false,
                defaultValue: new List<string>());

            migrationBuilder.AddColumn<decimal>(
                name: "declared_item_value",
                schema: "washgo",
                table: "incidents",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_refund_requests_settlement_id",
                schema: "washgo",
                table: "refund_requests",
                column: "settlement_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_settlement_id",
                schema: "washgo",
                table: "payment_transactions",
                column: "settlement_id");

            migrationBuilder.CreateIndex(
                name: "IX_merchant_settlements_carried_to_settlement_id",
                schema: "washgo",
                table: "merchant_settlements",
                column: "carried_to_settlement_id");
            migrationBuilder.Sql("""
                UPDATE washgo.merchant_settlements SET period_payout_amount=payout_amount;
                -- Never infer who paid an old incident or invent a customer-declared value.
                -- Frozen source IDs take precedence over legacy date-based reconciliation.
                UPDATE washgo.payment_transactions p SET settlement_id = (
                    SELECT s.id FROM washgo.merchant_settlements s
                    WHERE NOT s.is_deleted AND EXISTS (
                        SELECT 1 FROM jsonb_array_elements(COALESCE(s.breakdown_snapshot_json->'Lines','[]'::jsonb)) line
                        CROSS JOIN LATERAL jsonb_array_elements_text(COALESCE(line->'PaymentTransactionIds','[]'::jsonb)) tx(value)
                        WHERE tx.value=p.id::text) ORDER BY s.created_on,s.id LIMIT 1)
                WHERE p.settlement_id IS NULL;
                UPDATE washgo.refund_requests r SET settlement_id = (
                    SELECT s.id FROM washgo.merchant_settlements s
                    WHERE NOT s.is_deleted AND EXISTS (
                        SELECT 1 FROM jsonb_array_elements(COALESCE(s.breakdown_snapshot_json->'Lines','[]'::jsonb)) line
                        CROSS JOIN LATERAL jsonb_array_elements_text(COALESCE(line->'RefundRequestIds','[]'::jsonb)) tx(value)
                        WHERE tx.value=r.id::text) ORDER BY s.created_on,s.id LIMIT 1)
                WHERE r.settlement_id IS NULL;
                -- For periods without source IDs, only reconcile records already recorded when that period was created.
                UPDATE washgo.payment_transactions p SET settlement_id = (
                    SELECT s.id FROM washgo.merchant_settlements s JOIN washgo.orders o ON o.id=p.order_id
                    WHERE NOT s.is_deleted AND s.merchant_id=COALESCE(p.merchant_id_snapshot,o.merchant_id)
                        AND p.purpose=1 AND p.status IN(2,4) AND p.completed_at BETWEEN s.period_from AND s.period_to
                        AND p.created_on<=s.created_on AND (s.breakdown_snapshot_json IS NULL OR NOT EXISTS (
                            SELECT 1 FROM jsonb_array_elements(s.breakdown_snapshot_json->'Lines') line WHERE line ? 'PaymentTransactionIds'))
                    ORDER BY s.created_on,s.id LIMIT 1)
                WHERE p.settlement_id IS NULL;
                UPDATE washgo.refund_requests r SET settlement_id = (
                    SELECT s.id FROM washgo.merchant_settlements s JOIN washgo.orders o ON o.id=r.order_id
                    WHERE NOT s.is_deleted AND s.merchant_id=COALESCE(r.merchant_id_snapshot,o.merchant_id)
                        AND r.status=4 AND r.refunded_at BETWEEN s.period_from AND s.period_to
                        AND r.created_on<=s.created_on AND (s.breakdown_snapshot_json IS NULL OR NOT EXISTS (
                            SELECT 1 FROM jsonb_array_elements(s.breakdown_snapshot_json->'Lines') line WHERE line ? 'RefundRequestIds'))
                    ORDER BY s.created_on,s.id LIMIT 1)
                WHERE r.settlement_id IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refund_requests_settlement_id",
                schema: "washgo",
                table: "refund_requests");

            migrationBuilder.DropIndex(
                name: "IX_payment_transactions_settlement_id",
                schema: "washgo",
                table: "payment_transactions");

            migrationBuilder.DropIndex(
                name: "IX_merchant_settlements_carried_to_settlement_id",
                schema: "washgo",
                table: "merchant_settlements");

            migrationBuilder.DropColumn(
                name: "settlement_id",
                schema: "washgo",
                table: "refund_requests");

            migrationBuilder.DropColumn(
                name: "settlement_id",
                schema: "washgo",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "carried_to_settlement_id",
                schema: "washgo",
                table: "merchant_settlements");

            migrationBuilder.DropColumn(
                name: "carry_forward_amount",
                schema: "washgo",
                table: "merchant_settlements");

            migrationBuilder.DropColumn(
                name: "period_payout_amount",
                schema: "washgo",
                table: "merchant_settlements");

            migrationBuilder.DropColumn(
                name: "compensation_paid_by",
                schema: "washgo",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "compensation_proof_image_urls",
                schema: "washgo",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "declared_item_value",
                schema: "washgo",
                table: "incidents");
        }
    }
}
