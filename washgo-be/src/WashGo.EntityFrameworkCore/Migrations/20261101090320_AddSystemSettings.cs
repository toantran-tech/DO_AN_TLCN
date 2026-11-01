using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_settings",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    deposit_window_minutes = table.Column<int>(type: "integer", nullable: false),
                    merchant_collect_sla_hours = table.Column<int>(type: "integer", nullable: false),
                    merchant_return_sla_hours = table.Column<int>(type: "integer", nullable: false),
                    free_pickup_hours = table.Column<int>(type: "integer", nullable: false),
                    default_commission_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    overdue_fee_day1 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    overdue_fee_day2 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    overdue_fee_day3 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    storage_fee_per_day = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cancel_late_pending_percent = table.Column<int>(type: "integer", nullable: false),
                    cancel_deposited_percent = table.Column<int>(type: "integer", nullable: false),
                    cancel_collected_percent = table.Column<int>(type: "integer", nullable: false),
                    compensation_light_percent = table.Column<int>(type: "integer", nullable: false),
                    compensation_medium_percent = table.Column<int>(type: "integer", nullable: false),
                    compensation_severe_percent = table.Column<int>(type: "integer", nullable: false),
                    compensation_lost_percent = table.Column<int>(type: "integer", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.id);
                });

            migrationBuilder.InsertData(
                schema: "washgo",
                table: "system_settings",
                columns: new[] { "id", "cancel_collected_percent", "cancel_deposited_percent", "cancel_late_pending_percent", "compensation_light_percent", "compensation_lost_percent", "compensation_medium_percent", "compensation_severe_percent", "default_commission_rate", "deposit_window_minutes", "free_pickup_hours", "merchant_collect_sla_hours", "merchant_return_sla_hours", "overdue_fee_day1", "overdue_fee_day2", "overdue_fee_day3", "storage_fee_per_day", "updated_at", "updated_by" },
                values: new object[] { new Guid("5e771165-0000-4000-8000-000000000001"), 50, 30, 30, 30, 200, 70, 100, 20m, 30, 24, 4, 24, 10000m, 20000m, 50000m, 100000m, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_settings",
                schema: "washgo");
        }
    }
}
