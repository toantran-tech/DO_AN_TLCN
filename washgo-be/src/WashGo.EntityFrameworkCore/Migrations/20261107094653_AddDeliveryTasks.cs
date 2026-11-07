using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "delivery_tasks",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipper_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    task_type = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_tasks", x => x.id);
                    table.ForeignKey(
                        name: "FK_delivery_tasks_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "washgo",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_tasks_order_id",
                schema: "washgo",
                table: "delivery_tasks",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_tasks_shipper_id_status",
                schema: "washgo",
                table: "delivery_tasks",
                columns: new[] { "shipper_id", "status" });

            // Backfill. Completed tasks come from the shipper's own timeline entries (collect = Collected,
            // return = ReadyToReturn or the return of a cancelled order). The assignment time is only known for the
            // order's latest assignment; older ones stay NULL instead of being guessed.
            migrationBuilder.Sql(@"
INSERT INTO washgo.delivery_tasks (id, order_id, shipper_id, merchant_id, task_type, status, assigned_at, assigned_by, completed_at, ended_at)
SELECT gen_random_uuid(), h.order_id, s.id, o.merchant_id, t.task_type, 2,
       CASE WHEN o.shipper_id = s.id AND o.assigned_task_type = t.task_type THEN o.shipper_assigned_at END,
       NULL, to_timestamp(h.created_at), NULL
FROM washgo.order_status_history h
JOIN washgo.shippers s ON s.user_id = h.changed_by
JOIN washgo.orders o ON o.id = h.order_id
CROSS JOIN LATERAL (SELECT CASE WHEN h.to_status = 3 THEN 1 ELSE 2 END AS task_type) t
WHERE h.changed_by_type = 'Shipper'
  AND (h.to_status IN (3, 10) OR h.to_status = 99 AND h.from_status = 99);

INSERT INTO washgo.delivery_tasks (id, order_id, shipper_id, merchant_id, task_type, status, assigned_at, assigned_by, completed_at, ended_at)
SELECT gen_random_uuid(), o.id, o.shipper_id, o.merchant_id, o.assigned_task_type, 1, o.shipper_assigned_at, NULL, NULL, NULL
FROM washgo.orders o
WHERE NOT o.is_deleted AND o.shipper_id IS NOT NULL
  AND (o.status = 2 AND o.assigned_task_type = 1
       OR o.status = 9 AND o.assigned_task_type = 2
       OR o.status = 99 AND o.cancelled_goods_status = 2 AND o.assigned_task_type = 2);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "delivery_tasks",
                schema: "washgo");
        }
    }
}
