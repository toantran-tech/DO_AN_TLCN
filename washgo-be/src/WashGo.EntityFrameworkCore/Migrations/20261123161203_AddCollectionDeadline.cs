using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionDeadline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "collection_deadline_at",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "collection_deadline_computed_at",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "collection_deadline_state",
                schema: "washgo",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "collection_schedule_snapshot",
                schema: "washgo",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "collection_schedule_version",
                schema: "washgo",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "collection_starts_at",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "opening_schedule",
                schema: "washgo",
                table: "merchants",
                type: "jsonb",
                nullable: true);

            // Existing text that is exactly "HH:mm-HH:mm" becomes the same daily schedule. Anything else stays null:
            // no invented hours and no silent 24/7; those Merchants must be completed by the Admin (orders wait as ScheduleMissing).
            migrationBuilder.Sql(@"
UPDATE washgo.merchants m
SET opening_schedule = jsonb_build_object('timeZone', 'Asia/Ho_Chi_Minh', 'alwaysOpen', false, 'closedDates', '[]'::jsonb,
    'weekly', (SELECT jsonb_agg(jsonb_build_object('dayOfWeek', d, 'shifts', jsonb_build_array(jsonb_build_object('open', x.o, 'close', x.c))) ORDER BY d)
               FROM generate_series(1, 7) d))
FROM (SELECT user_id,
             substring(opening_hours #>> '{}' from '^\s*(\d{2}:\d{2})') AS o,
             substring(opening_hours #>> '{}' from '-\s*(\d{2}:\d{2})\s*$') AS c
      FROM washgo.merchants
      WHERE jsonb_typeof(opening_hours) = 'string'
        AND (opening_hours #>> '{}') ~ '^\s*\d{2}:\d{2}\s*-\s*\d{2}:\d{2}\s*$') x
WHERE m.user_id = x.user_id AND m.opening_schedule IS NULL
  AND substring(x.o, 1, 2) <= '23' AND substring(x.o, 4, 2) <= '59'
  AND (x.c = '24:00' OR (substring(x.c, 1, 2) <= '23' AND substring(x.c, 4, 2) <= '59'))
  AND x.o < x.c;");

            // Orders deposited before this rule keep the deadline they were given: the stored collect task deadline,
            // else DepositedAt + their SLA snapshot (continuous hours, as promised then). Never left null = never overdue.
            migrationBuilder.Sql(@"
UPDATE washgo.orders o
SET collection_deadline_at = COALESCE(
        (SELECT min(t.deadline) FROM washgo.delivery_tasks t WHERE t.order_id = o.id AND t.task_type = 1 AND t.deadline IS NOT NULL),
        o.deposited_at + make_interval(hours => COALESCE(o.collect_sla_hours_snapshot,
            (SELECT s.merchant_collect_sla_hours FROM washgo.system_settings s LIMIT 1), 4))),
    collection_starts_at = o.deposited_at,
    collection_deadline_state = 'Legacy',
    collection_deadline_computed_at = now() AT TIME ZONE 'utc'
WHERE o.deposited_at IS NOT NULL AND o.collection_deadline_at IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "collection_deadline_at",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "collection_deadline_computed_at",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "collection_deadline_state",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "collection_schedule_snapshot",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "collection_schedule_version",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "collection_starts_at",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "opening_schedule",
                schema: "washgo",
                table: "merchants");
        }
    }
}
