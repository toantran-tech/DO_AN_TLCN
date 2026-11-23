using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddPerShipperTaskLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "create_request_hash",
                schema: "washgo",
                table: "shippers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_active_tasks",
                schema: "washgo",
                table: "shippers",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            // Keep the predicates aligned with DeliveryTaskRules.ActiveDeliveries at this migration's schema version.
            migrationBuilder.Sql("""
                WITH legacy_limit AS (
                    SELECT COALESCE((
                        SELECT max_active_tasks_per_shipper FROM washgo.system_settings
                        WHERE id = '5e771165-0000-4000-8000-000000000001'
                    ), 3) AS task_limit
                ),
                active_counts AS (
                    SELECT shipper_id, count(*)::integer AS active_count
                    FROM washgo.orders
                    WHERE NOT is_deleted AND shipper_id IS NOT NULL AND (
                        (status = 17 AND assigned_task_type = 1) OR
                        (status = 19 AND assigned_task_type = 2) OR
                        (status IN (10, 14) AND assigned_task_type = 3) OR
                        (status IN (13, 15) AND assigned_task_type = 4) OR
                        (status = 99 AND cancelled_goods_status = 2 AND assigned_task_type = 2)
                    )
                    GROUP BY shipper_id
                ),
                shipper_limits AS (
                    SELECT s.id, GREATEST(l.task_limit, COALESCE(a.active_count, 0)) AS task_limit
                    FROM washgo.shippers s
                    CROSS JOIN legacy_limit l
                    LEFT JOIN active_counts a ON a.shipper_id = s.id
                )
                UPDATE washgo.shippers s
                SET max_active_tasks = l.task_limit
                FROM shipper_limits l
                WHERE s.id = l.id AND s.max_active_tasks <> l.task_limit;

                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM washgo.shippers WHERE max_active_tasks NOT BETWEEN 1 AND 20) THEN
                        RAISE EXCEPTION 'Cannot migrate Shipper capacity: at least one computed limit is outside 1–20. Review the legacy setting and open tasks before retrying.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_shippers_max_active_tasks",
                schema: "washgo",
                table: "shippers",
                sql: "max_active_tasks BETWEEN 1 AND 20");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_shippers_max_active_tasks",
                schema: "washgo",
                table: "shippers");

            migrationBuilder.DropColumn(
                name: "create_request_hash",
                schema: "washgo",
                table: "shippers");

            migrationBuilder.DropColumn(
                name: "max_active_tasks",
                schema: "washgo",
                table: "shippers");
        }
    }
}
