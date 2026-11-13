using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageCyclesAndReturnWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_delivery_tasks_order_id",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.AddColumn<decimal>(
                name: "accumulated_storage_fee",
                schema: "washgo",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "active_storage_cycle_id",
                schema: "washgo",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "deposit_box_size",
                schema: "washgo",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "storage_workflow_version",
                schema: "washgo",
                table: "orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "goods_collected_at",
                schema: "washgo",
                table: "delivery_tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "storage_cycle_id",
                schema: "washgo",
                table: "delivery_tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "order_storage_cycles",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    basis = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    shop_address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    source_locker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_box_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    collected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    intake_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dispatched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    transfer_deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    locker_fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    warehouse_fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    intake_actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    intake_receipt = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    exception_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_storage_cycles", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_storage_cycles_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "washgo",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "storage_return_requests",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    accepted_fee_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fee_accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_return_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_storage_return_requests_order_storage_cycles_cycle_id",
                        column: x => x.cycle_id,
                        principalSchema: "washgo",
                        principalTable: "order_storage_cycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_tasks_order_id",
                schema: "washgo",
                table: "delivery_tasks",
                column: "order_id",
                unique: true,
                filter: "status = 1 AND task_type IN (3,4)");

            migrationBuilder.CreateIndex(
                name: "IX_order_storage_cycles_merchant_id_state_created_at",
                schema: "washgo",
                table: "order_storage_cycles",
                columns: new[] { "merchant_id", "state", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_order_storage_cycles_order_id",
                schema: "washgo",
                table: "order_storage_cycles",
                column: "order_id",
                unique: true,
                filter: "state IN (1,2,3,4)");

            migrationBuilder.CreateIndex(
                name: "IX_storage_return_requests_cycle_id",
                schema: "washgo",
                table: "storage_return_requests",
                column: "cycle_id",
                unique: true,
                filter: "state IN (1,2,3)");

            migrationBuilder.CreateIndex(
                name: "IX_storage_return_requests_order_id_requested_at",
                schema: "washgo",
                table: "storage_return_requests",
                columns: new[] { "order_id", "requested_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "storage_return_requests",
                schema: "washgo");

            migrationBuilder.DropTable(
                name: "order_storage_cycles",
                schema: "washgo");

            migrationBuilder.DropIndex(
                name: "IX_delivery_tasks_order_id",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropColumn(
                name: "accumulated_storage_fee",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "active_storage_cycle_id",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "deposit_box_size",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "storage_workflow_version",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "goods_collected_at",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.DropColumn(
                name: "storage_cycle_id",
                schema: "washgo",
                table: "delivery_tasks");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_tasks_order_id",
                schema: "washgo",
                table: "delivery_tasks",
                column: "order_id");
        }
    }
}
