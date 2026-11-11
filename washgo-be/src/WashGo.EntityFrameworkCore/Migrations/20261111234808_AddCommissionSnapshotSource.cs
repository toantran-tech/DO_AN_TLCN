using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddCommissionSnapshotSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "commission_snapshot_source",
                schema: "washgo",
                table: "orders",
                type: "text",
                nullable: true);

            // Rates are only kept where they are evidenced. A legacy 0% (locked before the nullable-snapshot migration) cannot be
            // told apart from "never set", so unless it is already part of a settlement it becomes missing and is listed for review.
            migrationBuilder.Sql(@"
UPDATE washgo.orders SET commission_snapshot_source = 'Assignment'
 WHERE commission_rate_applied IS NOT NULL AND commission_locked_at >= TIMESTAMPTZ '2026-11-08 13:09:42+00';
UPDATE washgo.orders o SET commission_snapshot_source = 'LegacyUnverified', commission_rate_applied = NULL
 WHERE o.commission_rate_applied = 0 AND o.commission_snapshot_source IS NULL
   AND NOT EXISTS (SELECT 1 FROM washgo.payment_transactions p WHERE p.order_id = o.id AND p.settlement_id IS NOT NULL)
   AND NOT EXISTS (SELECT 1 FROM washgo.refund_requests r WHERE r.order_id = o.id AND r.settlement_id IS NOT NULL);
UPDATE washgo.payment_transactions p SET commission_rate_snapshot = NULL
 WHERE p.settlement_id IS NULL AND EXISTS (SELECT 1 FROM washgo.orders o WHERE o.id = p.order_id AND o.commission_snapshot_source = 'LegacyUnverified');
UPDATE washgo.refund_requests r SET commission_rate_snapshot = NULL
 WHERE r.settlement_id IS NULL AND EXISTS (SELECT 1 FROM washgo.orders o WHERE o.id = r.order_id AND o.commission_snapshot_source = 'LegacyUnverified');
UPDATE washgo.orders SET commission_snapshot_source = 'Legacy'
 WHERE commission_rate_applied IS NOT NULL AND commission_snapshot_source IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "commission_snapshot_source",
                schema: "washgo",
                table: "orders");
        }
    }
}
