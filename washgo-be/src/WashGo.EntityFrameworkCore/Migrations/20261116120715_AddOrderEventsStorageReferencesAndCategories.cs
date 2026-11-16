using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderEventsStorageReferencesAndCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ DECLARE bad_services bigint; bad_proposals bigint;
                BEGIN
                  SELECT count(*) INTO bad_services FROM washgo.services WHERE category NOT IN (1,2,3,4,5) OR category IS NULL;
                  SELECT count(*) INTO bad_proposals FROM washgo.service_change_requests
                    WHERE status=1 AND (jsonb_typeof(proposed_data) <> 'object' OR COALESCE(proposed_data->>'serviceType','') NOT IN ('1','2','3','4','5'));
                  IF bad_services > 0 OR bad_proposals > 0 THEN
                    RAISE EXCEPTION 'Category preflight failed: % services, % pending proposals require review; no arbitrary category backfill.', bad_services, bad_proposals;
                  END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_order_status_history_order_id",
                schema: "washgo",
                table: "order_status_history");

            migrationBuilder.AddColumn<Guid>(
                name: "category_id",
                schema: "washgo",
                table: "services",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "merchant_name",
                schema: "washgo",
                table: "order_storage_cycles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reference_basis",
                schema: "washgo",
                table: "order_storage_cycles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reference_issued_at",
                schema: "washgo",
                table: "order_storage_cycles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "storage_reference",
                schema: "washgo",
                table: "order_storage_cycles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "amount",
                schema: "washgo",
                table: "order_status_history",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "assignment_mode",
                schema: "washgo",
                table: "order_status_history",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "basis",
                schema: "washgo",
                table: "order_status_history",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Legacy");

            migrationBuilder.AddColumn<string>(
                name: "event_type",
                schema: "washgo",
                table: "order_status_history",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Legacy");

            migrationBuilder.AddColumn<Guid>(
                name: "merchant_id",
                schema: "washgo",
                table: "order_status_history",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "merchant_name",
                schema: "washgo",
                table: "order_status_history",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "payment_purpose",
                schema: "washgo",
                table: "order_status_history",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "previous_shipper_id",
                schema: "washgo",
                table: "order_status_history",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "return_request_id",
                schema: "washgo",
                table: "order_status_history",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "sequence",
                schema: "washgo",
                table: "order_status_history",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "shipper_id",
                schema: "washgo",
                table: "order_status_history",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "shipper_name",
                schema: "washgo",
                table: "order_status_history",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "storage_cycle_id",
                schema: "washgo",
                table: "order_status_history",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "task_id",
                schema: "washgo",
                table: "order_status_history",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "task_type",
                schema: "washgo",
                table: "order_status_history",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "category_code_snapshot",
                schema: "washgo",
                table: "order_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "category_id_snapshot",
                schema: "washgo",
                table: "order_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "category_name_snapshot",
                schema: "washgo",
                table: "order_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "processing_type_snapshot",
                schema: "washgo",
                table: "order_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "detected_at",
                schema: "washgo",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "due_at",
                schema: "washgo",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "event_id",
                schema: "washgo",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "notification_deliveries",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    lease_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "FK_notification_deliveries_notifications_notification_id",
                        column: x => x.notification_id,
                        principalSchema: "washgo",
                        principalTable: "notifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_categories",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_on = table.Column<long>(type: "bigint", nullable: false),
                    modified_by = table.Column<string>(type: "text", nullable: true),
                    modified_on = table.Column<long>(type: "bigint", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_by = table.Column<string>(type: "text", nullable: true),
                    deleted_on = table.Column<long>(type: "bigint", nullable: true),
                    created = table.Column<string>(type: "jsonb", nullable: true),
                    modified = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "storage_reference_claims",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_reference_claims", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_services_category_id",
                schema: "washgo",
                table: "services",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_storage_cycles_storage_reference",
                schema: "washgo",
                table: "order_storage_cycles",
                column: "storage_reference",
                unique: true,
                filter: "storage_reference IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_order_status_history_order_id_sequence",
                schema: "washgo",
                table: "order_status_history",
                columns: new[] { "order_id", "sequence" },
                unique: true,
                filter: "sequence IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_event_id_user_id",
                schema: "washgo",
                table: "notifications",
                columns: new[] { "event_id", "user_id" },
                unique: true,
                filter: "event_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notification_deliveries_notification_id",
                schema: "washgo",
                table: "notification_deliveries",
                column: "notification_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_deliveries_state_next_attempt_at",
                schema: "washgo",
                table: "notification_deliveries",
                columns: new[] { "state", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "IX_service_categories_code",
                schema: "washgo",
                table: "service_categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_categories_is_active_sort_order",
                schema: "washgo",
                table: "service_categories",
                columns: new[] { "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_service_categories_normalized_name",
                schema: "washgo",
                table: "service_categories",
                column: "normalized_name",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_storage_reference_claims_code",
                schema: "washgo",
                table: "storage_reference_claims",
                column: "code",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO washgo.service_categories(id,code,name,normalized_name,is_active,sort_order,revision,created_on,modified_on,is_deleted)
                SELECT id::uuid,code,name,upper(name),true,position,1,extract(epoch FROM now())::bigint,extract(epoch FROM now())::bigint,false
                FROM (VALUES
                  ('ca000000-0000-0000-0000-000000000001','STANDARD_WASH','Giặt thường',1),
                  ('ca000000-0000-0000-0000-000000000002','DRY_CLEAN','Giặt khô',2),
                  ('ca000000-0000-0000-0000-000000000003','IRON_ONLY','Ủi',3),
                  ('ca000000-0000-0000-0000-000000000004','BLANKET_WASH','Giặt chăn',4),
                  ('ca000000-0000-0000-0000-000000000005','SHOE_CARE','Vệ sinh giày',5)
                ) AS seeds(id,code,name,position);
                UPDATE washgo.services SET category_id = CASE category
                  WHEN 1 THEN 'ca000000-0000-0000-0000-000000000001'::uuid
                  WHEN 2 THEN 'ca000000-0000-0000-0000-000000000002'::uuid
                  WHEN 3 THEN 'ca000000-0000-0000-0000-000000000003'::uuid
                  WHEN 4 THEN 'ca000000-0000-0000-0000-000000000004'::uuid
                  WHEN 5 THEN 'ca000000-0000-0000-0000-000000000005'::uuid END;
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM washgo.services s LEFT JOIN washgo.service_categories c ON c.id=s.category_id WHERE c.id IS NULL) THEN
                    RAISE EXCEPTION 'Category backfill count mismatch; refusing FK activation.';
                  END IF;
                  RAISE NOTICE 'Category backfill validated; proposal raw JSON and historical order snapshots preserved.';
                END $$;
                """);
            migrationBuilder.AddForeignKey(
                name: "FK_services_service_categories_category_id",
                schema: "washgo",
                table: "services",
                column: "category_id",
                principalSchema: "washgo",
                principalTable: "service_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM washgo.order_status_history WHERE sequence IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM washgo.storage_reference_claims)
                    OR EXISTS (SELECT 1 FROM washgo.notification_deliveries)
                    OR EXISTS (SELECT 1 FROM washgo.order_items WHERE category_id_snapshot IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM washgo.service_categories WHERE revision <> 1 OR code NOT IN ('STANDARD_WASH','DRY_CLEAN','IRON_ONLY','BLANKET_WASH','SHOE_CARE')) THEN
                    RAISE EXCEPTION 'Workflow/category evidence exists. Reconcile and archive before schema rollback.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_services_service_categories_category_id",
                schema: "washgo",
                table: "services");

            migrationBuilder.DropTable(
                name: "notification_deliveries",
                schema: "washgo");

            migrationBuilder.DropTable(
                name: "service_categories",
                schema: "washgo");

            migrationBuilder.DropTable(
                name: "storage_reference_claims",
                schema: "washgo");

            migrationBuilder.DropIndex(
                name: "IX_services_category_id",
                schema: "washgo",
                table: "services");

            migrationBuilder.DropIndex(
                name: "IX_order_storage_cycles_storage_reference",
                schema: "washgo",
                table: "order_storage_cycles");

            migrationBuilder.DropIndex(
                name: "IX_order_status_history_order_id_sequence",
                schema: "washgo",
                table: "order_status_history");

            migrationBuilder.DropIndex(
                name: "IX_notifications_event_id_user_id",
                schema: "washgo",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "category_id",
                schema: "washgo",
                table: "services");

            migrationBuilder.DropColumn(
                name: "merchant_name",
                schema: "washgo",
                table: "order_storage_cycles");

            migrationBuilder.DropColumn(
                name: "reference_basis",
                schema: "washgo",
                table: "order_storage_cycles");

            migrationBuilder.DropColumn(
                name: "reference_issued_at",
                schema: "washgo",
                table: "order_storage_cycles");

            migrationBuilder.DropColumn(
                name: "storage_reference",
                schema: "washgo",
                table: "order_storage_cycles");

            migrationBuilder.DropColumn(
                name: "amount",
                schema: "washgo",
                table: "order_status_history");

            migrationBuilder.DropColumn(
                name: "assignment_mode",
                schema: "washgo",
                table: "order_status_history");

            migrationBuilder.DropColumn(
                name: "basis",
                schema: "washgo",
                table: "order_status_history");

            migrationBuilder.DropColumn(
                name: "event_type",
                schema: "washgo",
                table: "order_status_history");

            migrationBuilder.DropColumn(
                name: "merchant_id",
                schema: "washgo",
                table: "order_status_history");

            migrationBuilder.DropColumn(
                name: "merchant_name",
                schema: "washgo",
                table: "order_status_history");

            migrationBuilder.DropColumn(
                name: "payment_purpose",
                schema: "washgo",
                table: "order_status_history");

            migrationBuilder.DropColumn(
                name: "previous_shipper_id",
                schema: "washgo",
                table: "order_status_history");

            migrationBuilder.DropColumn(
                name: "return_request_id",
                schema: "washgo",
                table: "order_status_history");

            migrationBuilder.DropColumn(
                name: "sequence",
                schema: "washgo",
                table: "order_status_history");

            migrationBuilder.DropColumn(
                name: "shipper_id",
                schema: "washgo",
                table: "order_status_history");

            migrationBuilder.DropColumn(
                name: "shipper_name",
                schema: "washgo",
                table: "order_status_history");

            migrationBuilder.DropColumn(
                name: "storage_cycle_id",
                schema: "washgo",
                table: "order_status_history");

            migrationBuilder.DropColumn(
                name: "task_id",
                schema: "washgo",
                table: "order_status_history");

            migrationBuilder.DropColumn(
                name: "task_type",
                schema: "washgo",
                table: "order_status_history");

            migrationBuilder.DropColumn(
                name: "category_code_snapshot",
                schema: "washgo",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "category_id_snapshot",
                schema: "washgo",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "category_name_snapshot",
                schema: "washgo",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "processing_type_snapshot",
                schema: "washgo",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "detected_at",
                schema: "washgo",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "due_at",
                schema: "washgo",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "event_id",
                schema: "washgo",
                table: "notifications");

            migrationBuilder.CreateIndex(
                name: "IX_order_status_history_order_id",
                schema: "washgo",
                table: "order_status_history",
                column: "order_id");
        }
    }
}
