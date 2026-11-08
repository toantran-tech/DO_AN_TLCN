using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderSlaSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "collect_sla_hours_snapshot",
                schema: "washgo",
                table: "orders",
                type: "integer",
                nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "return_sla_hours_snapshot",
                schema: "washgo",
                table: "orders",
                type: "integer",
                nullable: true);
            migrationBuilder.Sql("""
                UPDATE washgo.orders SET collect_sla_hours_snapshot=(operational_settings_json->>'MerchantCollectSlaHours')::int,
                    return_sla_hours_snapshot=(operational_settings_json->>'MerchantReturnSlaHours')::int
                WHERE operational_settings_json IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "collect_sla_hours_snapshot",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "return_sla_hours_snapshot",
                schema: "washgo",
                table: "orders");
        }
    }
}
