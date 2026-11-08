using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddAdministrativeOperationsAndOrderPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "operational_policy_basis",
                schema: "washgo",
                table: "orders",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "operational_policy_captured_at",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "operational_settings_json",
                schema: "washgo",
                table: "orders",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "administrative_operations",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    action = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    resource_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "jsonb", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_administrative_operations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_administrative_operations_actor_id_module_completed_at",
                schema: "washgo",
                table: "administrative_operations",
                columns: new[] { "actor_id", "module", "completed_at" });
            // Declare a migration-time baseline for legacy orders. Historical rates are not reconstructed.
            // Existing deposit deadlines, payments and assessed fees remain untouched.
            migrationBuilder.Sql("""
                WITH baseline AS (
                  SELECT '{"DepositWindowMinutes":30,"MerchantCollectSlaHours":4,"MerchantReturnSlaHours":24,"FreePickupHours":24,"DefaultCommissionRate":20,"OverdueFeeDay1":10000,"OverdueFeeDay2":20000,"OverdueFeeDay3":50000,"StorageFeePerDay":100000,"CancelLatePendingPercent":30,"CancelDepositedPercent":30,"CancelCollectedPercent":50,"CompensationLightPercent":30,"CompensationMediumPercent":70,"CompensationSeverePercent":100,"CompensationLostPercent":200}'::jsonb
                    || COALESCE((SELECT jsonb_object_agg(
                        (SELECT string_agg(upper(left(part,1)) || substr(part,2), '' ORDER BY ord)
                           FROM unnest(string_to_array(j.key,'_')) WITH ORDINALITY AS words(part,ord)), j.value)
                        FROM washgo.system_settings s CROSS JOIN LATERAL jsonb_each(to_jsonb(s)) j
                        WHERE s.id='5e771165-0000-4000-8000-000000000001'), '{}'::jsonb) AS policy
                )
                UPDATE washgo.orders o SET operational_settings_json=b.policy,
                    operational_policy_basis='migration-baseline', operational_policy_captured_at=now(),
                    deposit_deadline=COALESCE(o.deposit_deadline, to_timestamp(o.created_on) + make_interval(mins => (b.policy->>'DepositWindowMinutes')::int))
                FROM baseline b WHERE o.operational_settings_json IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "administrative_operations",
                schema: "washgo");

            migrationBuilder.DropColumn(
                name: "operational_policy_basis",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "operational_policy_captured_at",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "operational_settings_json",
                schema: "washgo",
                table: "orders");
        }
    }
}
