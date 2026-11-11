using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddShipperTaskSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_delivery_tasks_orders_order_id",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.AddColumn<Guid>(
                name: "delivery_task_id",
                schema: "washgo",
                table: "order_operations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "result_json",
                schema: "washgo",
                table: "order_operations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "box_id_snapshot",
                schema: "washgo",
                table: "delivery_tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "box_number_snapshot",
                schema: "washgo",
                table: "delivery_tasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "completed_by",
                schema: "washgo",
                table: "delivery_tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "completed_by_role",
                schema: "washgo",
                table: "delivery_tasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "completion_event_id",
                schema: "washgo",
                table: "delivery_tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "completion_note",
                schema: "washgo",
                table: "delivery_tasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deadline",
                schema: "washgo",
                table: "delivery_tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "locker_address_snapshot",
                schema: "washgo",
                table: "delivery_tasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "locker_id_snapshot",
                schema: "washgo",
                table: "delivery_tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "locker_latitude_snapshot",
                schema: "washgo",
                table: "delivery_tasks",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "locker_longitude_snapshot",
                schema: "washgo",
                table: "delivery_tasks",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "locker_name_snapshot",
                schema: "washgo",
                table: "delivery_tasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "merchant_name_snapshot",
                schema: "washgo",
                table: "delivery_tasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "merchant_order_code_snapshot",
                schema: "washgo",
                table: "delivery_tasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "order_code_snapshot",
                schema: "washgo",
                table: "delivery_tasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "order_status_snapshot",
                schema: "washgo",
                table: "delivery_tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sla_hours",
                schema: "washgo",
                table: "delivery_tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "sla_started_at",
                schema: "washgo",
                table: "delivery_tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "snapshot_source",
                schema: "washgo",
                table: "delivery_tasks",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_tasks_completed_by_completed_at_id",
                schema: "washgo",
                table: "delivery_tasks",
                columns: new[] { "completed_by", "completed_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_tasks_completion_event_id",
                schema: "washgo",
                table: "delivery_tasks",
                column: "completion_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_tasks_shipper_id_status_assigned_at",
                schema: "washgo",
                table: "delivery_tasks",
                columns: new[] { "shipper_id", "status", "assigned_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_delivery_tasks_orders_order_id",
                schema: "washgo",
                table: "delivery_tasks",
                column: "order_id",
                principalSchema: "washgo",
                principalTable: "orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Match legacy tasks/events one-to-one, including multiple events in the same second.
            migrationBuilder.Sql(@"
WITH events AS (
 SELECT h.id, h.order_id, s.id AS shipper_id, to_timestamp(h.created_at) AS completed_at,
        CASE WHEN h.to_status=3 THEN 1 ELSE 2 END AS task_type,
        row_number() OVER (PARTITION BY h.order_id,s.id,CASE WHEN h.to_status=3 THEN 1 ELSE 2 END ORDER BY h.created_at,h.id) AS n
 FROM washgo.order_status_history h JOIN washgo.shippers s ON s.user_id=h.changed_by
 WHERE h.changed_by_type='Shipper' AND (h.to_status IN (3,10) OR h.to_status=99 AND h.from_status=99)
), legacy AS (
 SELECT id,order_id,shipper_id,completed_at,task_type,
        row_number() OVER (PARTITION BY order_id,shipper_id,task_type ORDER BY completed_at,id) AS n
 FROM washgo.delivery_tasks WHERE status=2 AND completion_event_id IS NULL
)
UPDATE washgo.delivery_tasks t SET completion_event_id=e.id
FROM legacy l JOIN events e ON l.order_id=e.order_id AND l.shipper_id=e.shipper_id
 AND abs(extract(epoch FROM (l.completed_at-e.completed_at)))<=1 AND l.task_type=e.task_type AND l.n=e.n WHERE t.id=l.id;

-- Retain old timeline events even if an older recorder did not create a DeliveryTask.
INSERT INTO washgo.delivery_tasks (id,order_id,shipper_id,merchant_id,task_type,status,completed_at,completion_event_id)
SELECT gen_random_uuid(),h.order_id,s.id,o.merchant_id,CASE WHEN h.to_status=3 THEN 1 ELSE 2 END,2,to_timestamp(h.created_at),h.id
FROM washgo.order_status_history h JOIN washgo.shippers s ON s.user_id=h.changed_by JOIN washgo.orders o ON o.id=h.order_id
WHERE h.changed_by_type='Shipper' AND (h.to_status IN (3,10) OR h.to_status=99 AND h.from_status=99)
 AND NOT EXISTS (SELECT 1 FROM washgo.delivery_tasks t WHERE t.completion_event_id=h.id);

UPDATE washgo.delivery_tasks t SET completed_by=h.changed_by,completed_by_role=h.changed_by_type,
 order_status_snapshot=h.to_status,completion_note=h.note
FROM washgo.order_status_history h WHERE t.completion_event_id=h.id;

-- These locations are current resource values, never labelled as historical observations.
UPDATE washgo.delivery_tasks t SET snapshot_source=CASE WHEN t.status=2 THEN 'LegacyCurrentResource' END,
 order_code_snapshot=o.order_code,merchant_order_code_snapshot=o.merchant_order_code,merchant_name_snapshot=o.merchant_name,
 locker_id_snapshot=CASE WHEN t.task_type=1 THEN o.locker_id ELSE COALESCE(o.return_locker_id,o.locker_id) END,
 box_id_snapshot=CASE WHEN t.task_type=1 THEN o.locker_slot_id ELSE o.return_box_id END,
 box_number_snapshot=CASE WHEN t.task_type=1 THEN o.slot_number ELSE o.return_box_number END,
 sla_started_at=CASE WHEN t.task_type=1 THEN o.deposited_at WHEN o.status<>99 THEN o.paid_at END,
 sla_hours=CASE WHEN t.task_type=1 THEN o.collect_sla_hours_snapshot ELSE o.return_sla_hours_snapshot END
FROM washgo.orders o WHERE t.order_id=o.id;
UPDATE washgo.delivery_tasks t SET locker_name_snapshot=l.name,
 locker_address_snapshot=concat_ws(', ',NULLIF(l.address,''),NULLIF(l.ward,''),NULLIF(l.district,''),NULLIF(l.city,'')),
 locker_latitude_snapshot=l.latitude,locker_longitude_snapshot=l.longitude
FROM washgo.lockers l WHERE t.locker_id_snapshot=l.id;
UPDATE washgo.delivery_tasks SET deadline=sla_started_at + make_interval(hours=>sla_hours)
WHERE sla_started_at IS NOT NULL AND sla_hours IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_delivery_tasks_orders_order_id",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropIndex(
                name: "IX_delivery_tasks_completed_by_completed_at_id",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropIndex(
                name: "IX_delivery_tasks_completion_event_id",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropIndex(
                name: "IX_delivery_tasks_shipper_id_status_assigned_at",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropColumn(
                name: "delivery_task_id",
                schema: "washgo",
                table: "order_operations");

            migrationBuilder.DropColumn(
                name: "result_json",
                schema: "washgo",
                table: "order_operations");

            migrationBuilder.DropColumn(
                name: "box_id_snapshot",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropColumn(
                name: "box_number_snapshot",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropColumn(
                name: "completed_by",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropColumn(
                name: "completed_by_role",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropColumn(
                name: "completion_event_id",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropColumn(
                name: "completion_note",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropColumn(
                name: "deadline",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropColumn(
                name: "locker_address_snapshot",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropColumn(
                name: "locker_id_snapshot",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropColumn(
                name: "locker_latitude_snapshot",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropColumn(
                name: "locker_longitude_snapshot",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropColumn(
                name: "locker_name_snapshot",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropColumn(
                name: "merchant_name_snapshot",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropColumn(
                name: "merchant_order_code_snapshot",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropColumn(
                name: "order_code_snapshot",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropColumn(
                name: "order_status_snapshot",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropColumn(
                name: "sla_hours",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropColumn(
                name: "sla_started_at",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropColumn(
                name: "snapshot_source",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.AddForeignKey(
                name: "FK_delivery_tasks_orders_order_id",
                schema: "washgo",
                table: "delivery_tasks",
                column: "order_id",
                principalSchema: "washgo",
                principalTable: "orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
