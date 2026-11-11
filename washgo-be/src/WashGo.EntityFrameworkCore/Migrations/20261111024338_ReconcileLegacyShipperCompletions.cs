using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileLegacyShipperCompletions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve the original task (and assignment time) when a second-granularity event
            // was materialized as another legacy row. Only mutually unique nearest matches qualify.
            migrationBuilder.Sql(@"
CREATE TEMP TABLE shipper_completion_reconciliation ON COMMIT DROP AS
WITH pairs AS (
 SELECT u.id AS original_id,e.id AS duplicate_id,e.completion_event_id,
 row_number() OVER (PARTITION BY u.id ORDER BY abs(extract(epoch FROM (u.completed_at-e.completed_at))),e.id) AS un,
 row_number() OVER (PARTITION BY e.id ORDER BY abs(extract(epoch FROM (u.completed_at-e.completed_at))),u.id) AS en
 FROM washgo.delivery_tasks u JOIN washgo.delivery_tasks e
 ON u.order_id=e.order_id AND u.shipper_id=e.shipper_id AND u.task_type=e.task_type
 WHERE u.status=2 AND u.completion_event_id IS NULL AND u.snapshot_source='LegacyCurrentResource'
 AND e.completed_by_role='Shipper' AND e.snapshot_source='LegacyCurrentResource'
 AND abs(extract(epoch FROM (u.completed_at-e.completed_at)))<=1
 AND NOT EXISTS (SELECT 1 FROM washgo.order_operations op WHERE op.delivery_task_id=e.id)
)
SELECT original_id,duplicate_id,completion_event_id FROM pairs WHERE un=1 AND en=1;
UPDATE washgo.delivery_tasks e SET completion_event_id=NULL
FROM shipper_completion_reconciliation m WHERE e.id=m.duplicate_id;
UPDATE washgo.delivery_tasks u SET completion_event_id=m.completion_event_id,
 completed_by=e.completed_by,completed_by_role=e.completed_by_role,order_status_snapshot=e.order_status_snapshot,
 completion_note=e.completion_note
FROM shipper_completion_reconciliation m JOIN washgo.delivery_tasks e ON e.id=m.duplicate_id
WHERE u.id=m.original_id;
DELETE FROM washgo.delivery_tasks e USING shipper_completion_reconciliation m WHERE e.id=m.duplicate_id;
");


        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data correction is intentionally retained: rollback must not recreate duplicate deliveries.

        }
    }
}
