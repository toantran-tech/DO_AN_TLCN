using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations;

public partial class PaymentSessionConcurrency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "session_state", schema: "washgo", table: "payment_transactions",
            type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<bool>(name: "is_active_session", schema: "washgo", table: "payment_transactions",
            type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<DateTime>(name: "gateway_verified_at", schema: "washgo", table: "payment_transactions",
            type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "requires_reconciliation", schema: "washgo", table: "payment_transactions",
            type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>(name: "reconciliation_reason", schema: "washgo", table: "payment_transactions",
            type: "character varying(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "reconciled_at", schema: "washgo", table: "payment_transactions",
            type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "is_excess_payment", schema: "washgo", table: "refund_requests",
            type: "boolean", nullable: false, defaultValue: false);
        // Legacy rows are retained unchanged. Multiple old Pending references are
        // surfaced by the reconciliation query and block new sessions at runtime.
        migrationBuilder.CreateIndex(name: "ux_payment_active_session", schema: "washgo", table: "payment_transactions",
            columns: new[] { "order_id", "purpose" }, unique: true,
            filter: "is_active_session = true AND is_deleted = false");
        migrationBuilder.CreateIndex(name: "ux_refund_excess_payment", schema: "washgo", table: "refund_requests",
            columns: new[] { "original_payment_transaction_id", "is_excess_payment" }, unique: true,
            filter: "is_excess_payment = true AND is_deleted = false");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Dropping financial evidence requires an explicit backup/rollback decision.
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM washgo.payment_transactions WHERE requires_reconciliation)
                   OR EXISTS (SELECT 1 FROM washgo.refund_requests WHERE is_excess_payment) THEN
                    RAISE EXCEPTION 'Cannot drop payment reconciliation evidence; export/review it before rollback.';
                END IF;
            END $$;
            """);
        migrationBuilder.DropIndex(name: "ux_payment_active_session", schema: "washgo", table: "payment_transactions");
        migrationBuilder.DropIndex(name: "ux_refund_excess_payment", schema: "washgo", table: "refund_requests");
        foreach (var column in new[] { "session_state", "is_active_session", "gateway_verified_at", "requires_reconciliation", "reconciliation_reason", "reconciled_at" })
            migrationBuilder.DropColumn(name: column, schema: "washgo", table: "payment_transactions");
        migrationBuilder.DropColumn(name: "is_excess_payment", schema: "washgo", table: "refund_requests");
    }
}
