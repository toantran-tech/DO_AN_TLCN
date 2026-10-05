using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityPartnersAndAlignCoreSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_locker_slots_lockers_LockerId",
                schema: "washgo",
                table: "locker_slots");

            migrationBuilder.DropForeignKey(
                name: "FK_order_items_orders_WashOrderId",
                schema: "washgo",
                table: "order_items");

            migrationBuilder.DropForeignKey(
                name: "FK_order_items_services_ServiceId",
                schema: "washgo",
                table: "order_items");

            migrationBuilder.DropForeignKey(
                name: "FK_order_status_history_orders_WashOrderId",
                schema: "washgo",
                table: "order_status_history");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_locker_slots_BoxId",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_locker_slots_ReturnBoxId",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_lockers_LockerId",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_lockers_ReturnLockerId",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_locker_slots_LockerId",
                schema: "washgo",
                table: "locker_slots");

            migrationBuilder.DropColumn(
                name: "BoxNumber",
                schema: "washgo",
                table: "locker_slots");

            migrationBuilder.DropColumn(
                name: "CurrentPinCode",
                schema: "washgo",
                table: "locker_slots");

            migrationBuilder.RenameColumn(
                name: "Unit",
                schema: "washgo",
                table: "services",
                newName: "unit");

            migrationBuilder.RenameColumn(
                name: "Name",
                schema: "washgo",
                table: "services",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Modified",
                schema: "washgo",
                table: "services",
                newName: "modified");

            migrationBuilder.RenameColumn(
                name: "Description",
                schema: "washgo",
                table: "services",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "Created",
                schema: "washgo",
                table: "services",
                newName: "created");

            migrationBuilder.RenameColumn(
                name: "Code",
                schema: "washgo",
                table: "services",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "washgo",
                table: "services",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UnitPrice",
                schema: "washgo",
                table: "services",
                newName: "base_price");

            migrationBuilder.RenameColumn(
                name: "ServiceType",
                schema: "washgo",
                table: "services",
                newName: "category");

            migrationBuilder.RenameColumn(
                name: "SearchVector",
                schema: "washgo",
                table: "services",
                newName: "search_vector");

            migrationBuilder.RenameColumn(
                name: "ModifiedOn",
                schema: "washgo",
                table: "services",
                newName: "modified_on");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                schema: "washgo",
                table: "services",
                newName: "modified_by");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                schema: "washgo",
                table: "services",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                schema: "washgo",
                table: "services",
                newName: "is_active");

            migrationBuilder.RenameColumn(
                name: "EstimatedDurationHours",
                schema: "washgo",
                table: "services",
                newName: "estimated_time");

            migrationBuilder.RenameColumn(
                name: "DeletedOn",
                schema: "washgo",
                table: "services",
                newName: "deleted_on");

            migrationBuilder.RenameColumn(
                name: "DeletedBy",
                schema: "washgo",
                table: "services",
                newName: "deleted_by");

            migrationBuilder.RenameColumn(
                name: "CreatedOn",
                schema: "washgo",
                table: "services",
                newName: "created_on");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                schema: "washgo",
                table: "services",
                newName: "created_by");

            migrationBuilder.RenameIndex(
                name: "IX_services_Code",
                schema: "washgo",
                table: "services",
                newName: "IX_services_code");

            migrationBuilder.RenameIndex(
                name: "IX_services_SearchVector",
                schema: "washgo",
                table: "services",
                newName: "IX_services_search_vector");

            migrationBuilder.RenameColumn(
                name: "Status",
                schema: "washgo",
                table: "orders",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Modified",
                schema: "washgo",
                table: "orders",
                newName: "modified");

            migrationBuilder.RenameColumn(
                name: "Created",
                schema: "washgo",
                table: "orders",
                newName: "created");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "washgo",
                table: "orders",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "TotalAmount",
                schema: "washgo",
                table: "orders",
                newName: "total_amount");

            migrationBuilder.RenameColumn(
                name: "ShipperName",
                schema: "washgo",
                table: "orders",
                newName: "shipper_name");

            migrationBuilder.RenameColumn(
                name: "ShipperId",
                schema: "washgo",
                table: "orders",
                newName: "shipper_id");

            migrationBuilder.RenameColumn(
                name: "SearchVector",
                schema: "washgo",
                table: "orders",
                newName: "search_vector");

            migrationBuilder.RenameColumn(
                name: "ReturnLockerId",
                schema: "washgo",
                table: "orders",
                newName: "return_locker_id");

            migrationBuilder.RenameColumn(
                name: "ReturnBoxNumber",
                schema: "washgo",
                table: "orders",
                newName: "return_box_number");

            migrationBuilder.RenameColumn(
                name: "ReturnBoxId",
                schema: "washgo",
                table: "orders",
                newName: "return_box_id");

            migrationBuilder.RenameColumn(
                name: "QrCodeString",
                schema: "washgo",
                table: "orders",
                newName: "qr_code");

            migrationBuilder.RenameColumn(
                name: "PickupPinCode",
                schema: "washgo",
                table: "orders",
                newName: "pickup_pin_code");

            migrationBuilder.RenameColumn(
                name: "PaymentStatus",
                schema: "washgo",
                table: "orders",
                newName: "payment_status");

            migrationBuilder.RenameColumn(
                name: "PaymentMethod",
                schema: "washgo",
                table: "orders",
                newName: "payment_method");

            migrationBuilder.RenameColumn(
                name: "OrderCode",
                schema: "washgo",
                table: "orders",
                newName: "order_code");

            migrationBuilder.RenameColumn(
                name: "ModifiedOn",
                schema: "washgo",
                table: "orders",
                newName: "modified_on");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                schema: "washgo",
                table: "orders",
                newName: "modified_by");

            migrationBuilder.RenameColumn(
                name: "MerchantName",
                schema: "washgo",
                table: "orders",
                newName: "merchant_name");

            migrationBuilder.RenameColumn(
                name: "MerchantId",
                schema: "washgo",
                table: "orders",
                newName: "merchant_id");

            migrationBuilder.RenameColumn(
                name: "LockerName",
                schema: "washgo",
                table: "orders",
                newName: "locker_name");

            migrationBuilder.RenameColumn(
                name: "LockerId",
                schema: "washgo",
                table: "orders",
                newName: "locker_id");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                schema: "washgo",
                table: "orders",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "FinalAmount",
                schema: "washgo",
                table: "orders",
                newName: "final_amount");

            migrationBuilder.RenameColumn(
                name: "ExpectedDeliveryTime",
                schema: "washgo",
                table: "orders",
                newName: "expected_delivery_time");

            migrationBuilder.RenameColumn(
                name: "DiscountAmount",
                schema: "washgo",
                table: "orders",
                newName: "discount_amount");

            migrationBuilder.RenameColumn(
                name: "DepositPinCode",
                schema: "washgo",
                table: "orders",
                newName: "deposit_pin_code");

            migrationBuilder.RenameColumn(
                name: "DeletedOn",
                schema: "washgo",
                table: "orders",
                newName: "deleted_on");

            migrationBuilder.RenameColumn(
                name: "DeletedBy",
                schema: "washgo",
                table: "orders",
                newName: "deleted_by");

            migrationBuilder.RenameColumn(
                name: "CustomerPhoneNumber",
                schema: "washgo",
                table: "orders",
                newName: "customer_phone_number");

            migrationBuilder.RenameColumn(
                name: "CustomerNote",
                schema: "washgo",
                table: "orders",
                newName: "note");

            migrationBuilder.RenameColumn(
                name: "CustomerName",
                schema: "washgo",
                table: "orders",
                newName: "customer_name");

            migrationBuilder.RenameColumn(
                name: "CustomerId",
                schema: "washgo",
                table: "orders",
                newName: "customer_id");

            migrationBuilder.RenameColumn(
                name: "CreatedOn",
                schema: "washgo",
                table: "orders",
                newName: "created_on");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                schema: "washgo",
                table: "orders",
                newName: "created_by");

            migrationBuilder.RenameColumn(
                name: "BoxNumber",
                schema: "washgo",
                table: "orders",
                newName: "slot_number");

            migrationBuilder.RenameColumn(
                name: "BoxId",
                schema: "washgo",
                table: "orders",
                newName: "locker_slot_id");

            migrationBuilder.RenameIndex(
                name: "IX_orders_SearchVector",
                schema: "washgo",
                table: "orders",
                newName: "IX_orders_search_vector");

            migrationBuilder.RenameIndex(
                name: "IX_orders_ReturnLockerId",
                schema: "washgo",
                table: "orders",
                newName: "IX_orders_return_locker_id");

            migrationBuilder.RenameIndex(
                name: "IX_orders_ReturnBoxId",
                schema: "washgo",
                table: "orders",
                newName: "IX_orders_return_box_id");

            migrationBuilder.RenameIndex(
                name: "IX_orders_OrderCode",
                schema: "washgo",
                table: "orders",
                newName: "IX_orders_order_code");

            migrationBuilder.RenameIndex(
                name: "IX_orders_LockerId",
                schema: "washgo",
                table: "orders",
                newName: "IX_orders_locker_id");

            migrationBuilder.RenameIndex(
                name: "IX_orders_BoxId",
                schema: "washgo",
                table: "orders",
                newName: "IX_orders_locker_slot_id");

            migrationBuilder.RenameColumn(
                name: "Note",
                schema: "washgo",
                table: "order_status_history",
                newName: "note");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "washgo",
                table: "order_status_history",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "WashOrderId",
                schema: "washgo",
                table: "order_status_history",
                newName: "order_id");

            migrationBuilder.RenameColumn(
                name: "Timestamp",
                schema: "washgo",
                table: "order_status_history",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "Status",
                schema: "washgo",
                table: "order_status_history",
                newName: "to_status");

            migrationBuilder.RenameColumn(
                name: "ActorRole",
                schema: "washgo",
                table: "order_status_history",
                newName: "changed_by_type");

            migrationBuilder.RenameColumn(
                name: "ActorName",
                schema: "washgo",
                table: "order_status_history",
                newName: "actor_name");

            migrationBuilder.RenameColumn(
                name: "ActorId",
                schema: "washgo",
                table: "order_status_history",
                newName: "changed_by");

            migrationBuilder.RenameIndex(
                name: "IX_order_status_history_WashOrderId",
                schema: "washgo",
                table: "order_status_history",
                newName: "IX_order_status_history_order_id");

            migrationBuilder.RenameColumn(
                name: "Quantity",
                schema: "washgo",
                table: "order_items",
                newName: "quantity");

            migrationBuilder.RenameColumn(
                name: "Note",
                schema: "washgo",
                table: "order_items",
                newName: "note");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "washgo",
                table: "order_items",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "WashOrderId",
                schema: "washgo",
                table: "order_items",
                newName: "order_id");

            migrationBuilder.RenameColumn(
                name: "UnitPrice",
                schema: "washgo",
                table: "order_items",
                newName: "unit_price");

            migrationBuilder.RenameColumn(
                name: "SubTotal",
                schema: "washgo",
                table: "order_items",
                newName: "total_price");

            migrationBuilder.RenameColumn(
                name: "ServiceName",
                schema: "washgo",
                table: "order_items",
                newName: "service_name");

            migrationBuilder.RenameColumn(
                name: "ServiceId",
                schema: "washgo",
                table: "order_items",
                newName: "service_id");

            migrationBuilder.RenameIndex(
                name: "IX_order_items_WashOrderId",
                schema: "washgo",
                table: "order_items",
                newName: "IX_order_items_order_id");

            migrationBuilder.RenameIndex(
                name: "IX_order_items_ServiceId",
                schema: "washgo",
                table: "order_items",
                newName: "IX_order_items_service_id");

            migrationBuilder.RenameColumn(
                name: "Ward",
                schema: "washgo",
                table: "lockers",
                newName: "ward");

            migrationBuilder.RenameColumn(
                name: "Status",
                schema: "washgo",
                table: "lockers",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Name",
                schema: "washgo",
                table: "lockers",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Modified",
                schema: "washgo",
                table: "lockers",
                newName: "modified");

            migrationBuilder.RenameColumn(
                name: "Longitude",
                schema: "washgo",
                table: "lockers",
                newName: "longitude");

            migrationBuilder.RenameColumn(
                name: "Latitude",
                schema: "washgo",
                table: "lockers",
                newName: "latitude");

            migrationBuilder.RenameColumn(
                name: "District",
                schema: "washgo",
                table: "lockers",
                newName: "district");

            migrationBuilder.RenameColumn(
                name: "Created",
                schema: "washgo",
                table: "lockers",
                newName: "created");

            migrationBuilder.RenameColumn(
                name: "Code",
                schema: "washgo",
                table: "lockers",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "City",
                schema: "washgo",
                table: "lockers",
                newName: "city");

            migrationBuilder.RenameColumn(
                name: "Address",
                schema: "washgo",
                table: "lockers",
                newName: "address");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "washgo",
                table: "lockers",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "TotalBoxes",
                schema: "washgo",
                table: "lockers",
                newName: "total_slots");

            migrationBuilder.RenameColumn(
                name: "SearchVector",
                schema: "washgo",
                table: "lockers",
                newName: "search_vector");

            migrationBuilder.RenameColumn(
                name: "ModifiedOn",
                schema: "washgo",
                table: "lockers",
                newName: "modified_on");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                schema: "washgo",
                table: "lockers",
                newName: "modified_by");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                schema: "washgo",
                table: "lockers",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "DeletedOn",
                schema: "washgo",
                table: "lockers",
                newName: "deleted_on");

            migrationBuilder.RenameColumn(
                name: "DeletedBy",
                schema: "washgo",
                table: "lockers",
                newName: "deleted_by");

            migrationBuilder.RenameColumn(
                name: "CreatedOn",
                schema: "washgo",
                table: "lockers",
                newName: "created_on");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                schema: "washgo",
                table: "lockers",
                newName: "created_by");

            migrationBuilder.RenameIndex(
                name: "IX_lockers_Code",
                schema: "washgo",
                table: "lockers",
                newName: "IX_lockers_code");

            migrationBuilder.RenameIndex(
                name: "IX_lockers_SearchVector",
                schema: "washgo",
                table: "lockers",
                newName: "IX_lockers_search_vector");

            migrationBuilder.RenameColumn(
                name: "Status",
                schema: "washgo",
                table: "locker_slots",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Size",
                schema: "washgo",
                table: "locker_slots",
                newName: "size");

            migrationBuilder.RenameColumn(
                name: "Modified",
                schema: "washgo",
                table: "locker_slots",
                newName: "modified");

            migrationBuilder.RenameColumn(
                name: "Created",
                schema: "washgo",
                table: "locker_slots",
                newName: "created");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "washgo",
                table: "locker_slots",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "SearchVector",
                schema: "washgo",
                table: "locker_slots",
                newName: "search_vector");

            migrationBuilder.RenameColumn(
                name: "ModifiedOn",
                schema: "washgo",
                table: "locker_slots",
                newName: "modified_on");

            migrationBuilder.RenameColumn(
                name: "ModifiedBy",
                schema: "washgo",
                table: "locker_slots",
                newName: "modified_by");

            migrationBuilder.RenameColumn(
                name: "LockerId",
                schema: "washgo",
                table: "locker_slots",
                newName: "locker_id");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                schema: "washgo",
                table: "locker_slots",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "DeletedOn",
                schema: "washgo",
                table: "locker_slots",
                newName: "deleted_on");

            migrationBuilder.RenameColumn(
                name: "DeletedBy",
                schema: "washgo",
                table: "locker_slots",
                newName: "deleted_by");

            migrationBuilder.RenameColumn(
                name: "CreatedOn",
                schema: "washgo",
                table: "locker_slots",
                newName: "created_on");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                schema: "washgo",
                table: "locker_slots",
                newName: "created_by");

            migrationBuilder.RenameIndex(
                name: "IX_locker_slots_SearchVector",
                schema: "washgo",
                table: "locker_slots",
                newName: "IX_locker_slots_search_vector");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "washgo",
                table: "services",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AddColumn<string>(
                name: "allowed_statuses",
                schema: "washgo",
                table: "services",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                schema: "washgo",
                table: "services",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "approved_by",
                schema: "washgo",
                table: "services",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "images",
                schema: "washgo",
                table: "services",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "merchant_id",
                schema: "washgo",
                table: "services",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "price_per_item",
                schema: "washgo",
                table: "services",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "price_per_kg",
                schema: "washgo",
                table: "services",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                schema: "washgo",
                table: "services",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "status",
                schema: "washgo",
                table: "services",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "qr_code",
                schema: "washgo",
                table: "orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "actual_item_count",
                schema: "washgo",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "actual_weight_kg",
                schema: "washgo",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cancel_fee_amount",
                schema: "washgo",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                schema: "washgo",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_at",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cancelled_by",
                schema: "washgo",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cancelled_by_type",
                schema: "washgo",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deposit_deadline",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "estimated_finish_at",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "eta_updated_at",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "finished_at",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "merchant_order_code",
                schema: "washgo",
                table: "orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "overdue_fee_total",
                schema: "washgo",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "payment_deadline",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "pickup_deadline",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "promotion_id",
                schema: "washgo",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "return_deadline",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "storage_fee_total",
                schema: "washgo",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "from_status",
                schema: "washgo",
                table: "order_status_history",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "washgo",
                table: "order_items",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "weight_kg",
                schema: "washgo",
                table: "order_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "washgo",
                table: "lockers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "address",
                schema: "washgo",
                table: "lockers",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(250)",
                oldMaxLength: 250);

            migrationBuilder.AlterColumn<int>(
                name: "total_slots",
                schema: "washgo",
                table: "lockers",
                type: "integer",
                nullable: false,
                defaultValue: 10,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                schema: "washgo",
                table: "lockers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "approved_by",
                schema: "washgo",
                table: "lockers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "available_slots",
                schema: "washgo",
                table: "lockers",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<string>(
                name: "description",
                schema: "washgo",
                table: "lockers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "images",
                schema: "washgo",
                table: "lockers",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "locker_type",
                schema: "washgo",
                table: "lockers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "merchant_id",
                schema: "washgo",
                table: "lockers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                schema: "washgo",
                table: "lockers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "service_area_id",
                schema: "washgo",
                table: "lockers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_used_at",
                schema: "washgo",
                table: "locker_slots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "slot_number",
                schema: "washgo",
                table: "locker_slots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    auth_provider = table.Column<int>(type: "integer", nullable: false),
                    provider_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    avatar = table.Column<string>(type: "jsonb", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.CheckConstraint("chk_users_auth", "(auth_provider = 1 AND password_hash IS NOT NULL) OR (auth_provider <> 1 AND provider_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_users_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "washgo",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "washgo",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    district = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    loyalty_points = table.Column<int>(type: "integer", nullable: false),
                    total_orders = table.Column<int>(type: "integer", nullable: false),
                    total_spent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_customers_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "washgo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "merchants",
                schema: "washgo",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tax_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    business_license = table.Column<string>(type: "jsonb", nullable: true),
                    address = table.Column<string>(type: "text", nullable: false),
                    district = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(10,8)", precision: 10, scale: 8, nullable: false),
                    longitude = table.Column<decimal>(type: "numeric(11,8)", precision: 11, scale: 8, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    opening_hours = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rating = table.Column<decimal>(type: "numeric(2,1)", precision: 2, scale: 1, nullable: false),
                    total_orders = table.Column<int>(type: "integer", nullable: false),
                    total_revenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    deposit_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    deposit_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    contract_start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    contract_end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sla_doc_url = table.Column<string>(type: "text", nullable: true),
                    commission_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    service_radius_km = table.Column<int>(type: "integer", nullable: false),
                    current_workload = table.Column<int>(type: "integer", nullable: false),
                    max_capacity = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchants", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_merchants_users_approved_by",
                        column: x => x.approved_by,
                        principalSchema: "washgo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_merchants_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "washgo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "otp_codes",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    otp_code = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    purpose = table.Column<int>(type: "integer", nullable: false),
                    expiry_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_used = table.Column<bool>(type: "boolean", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_otp_codes", x => x.id);
                    table.ForeignKey(
                        name: "FK_otp_codes_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "washgo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    expiry_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "washgo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_areas",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    district = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(10,8)", precision: 10, scale: 8, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(11,8)", precision: 11, scale: 8, nullable: true),
                    radius_km = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_areas", x => x.id);
                    table.ForeignKey(
                        name: "FK_service_areas_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "washgo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "merchant_images",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    image_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    public_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    secure_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchant_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_merchant_images_merchants_merchant_id",
                        column: x => x.merchant_id,
                        principalSchema: "washgo",
                        principalTable: "merchants",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_change_requests",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: true),
                    request_type = table.Column<int>(type: "integer", nullable: false),
                    proposed_data = table.Column<string>(type: "jsonb", nullable: false),
                    current_data = table.Column<string>(type: "jsonb", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_change_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_service_change_requests_merchants_merchant_id",
                        column: x => x.merchant_id,
                        principalSchema: "washgo",
                        principalTable: "merchants",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_service_change_requests_services_service_id",
                        column: x => x.service_id,
                        principalSchema: "washgo",
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_service_change_requests_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalSchema: "washgo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "shippers",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_type = table.Column<int>(type: "integer", nullable: false),
                    vehicle_plate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    identity_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    identity_images = table.Column<string>(type: "jsonb", nullable: true),
                    total_deliveries = table.Column<int>(type: "integer", nullable: false),
                    rating = table.Column<decimal>(type: "numeric(2,1)", precision: 2, scale: 1, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shippers", x => x.id);
                    table.ForeignKey(
                        name: "FK_shippers_merchants_merchant_id",
                        column: x => x.merchant_id,
                        principalSchema: "washgo",
                        principalTable: "merchants",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shippers_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "washgo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "merchant_service_areas",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_area_id = table.Column<Guid>(type: "uuid", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchant_service_areas", x => x.id);
                    table.ForeignKey(
                        name: "FK_merchant_service_areas_merchants_merchant_id",
                        column: x => x.merchant_id,
                        principalSchema: "washgo",
                        principalTable: "merchants",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_merchant_service_areas_service_areas_service_area_id",
                        column: x => x.service_area_id,
                        principalSchema: "washgo",
                        principalTable: "service_areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_merchant_service_areas_users_assigned_by",
                        column: x => x.assigned_by,
                        principalSchema: "washgo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_services_approved_by",
                schema: "washgo",
                table: "services",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "IX_services_merchant_id",
                schema: "washgo",
                table: "services",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "IX_services_status",
                schema: "washgo",
                table: "services",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_orders_cancelled_by",
                schema: "washgo",
                table: "orders",
                column: "cancelled_by");

            migrationBuilder.CreateIndex(
                name: "IX_orders_customer_id",
                schema: "washgo",
                table: "orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_merchant_id",
                schema: "washgo",
                table: "orders",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_merchant_order_code",
                schema: "washgo",
                table: "orders",
                column: "merchant_order_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_promotion_id",
                schema: "washgo",
                table: "orders",
                column: "promotion_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_shipper_id",
                schema: "washgo",
                table: "orders",
                column: "shipper_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_status",
                schema: "washgo",
                table: "orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_lockers_approved_by",
                schema: "washgo",
                table: "lockers",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "IX_lockers_latitude_longitude",
                schema: "washgo",
                table: "lockers",
                columns: new[] { "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_lockers_merchant_id",
                schema: "washgo",
                table: "lockers",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "IX_lockers_service_area_id",
                schema: "washgo",
                table: "lockers",
                column: "service_area_id");

            migrationBuilder.CreateIndex(
                name: "IX_lockers_status",
                schema: "washgo",
                table: "lockers",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_locker_slots_locker_id_slot_number",
                schema: "washgo",
                table: "locker_slots",
                columns: new[] { "locker_id", "slot_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_locker_slots_status",
                schema: "washgo",
                table: "locker_slots",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_merchant_images_merchant_id",
                schema: "washgo",
                table: "merchant_images",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "IX_merchant_service_areas_assigned_by",
                schema: "washgo",
                table: "merchant_service_areas",
                column: "assigned_by");

            migrationBuilder.CreateIndex(
                name: "IX_merchant_service_areas_merchant_id_service_area_id",
                schema: "washgo",
                table: "merchant_service_areas",
                columns: new[] { "merchant_id", "service_area_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_merchant_service_areas_service_area_id",
                schema: "washgo",
                table: "merchant_service_areas",
                column: "service_area_id");

            migrationBuilder.CreateIndex(
                name: "IX_merchants_approved_by",
                schema: "washgo",
                table: "merchants",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "IX_merchants_city",
                schema: "washgo",
                table: "merchants",
                column: "city");

            migrationBuilder.CreateIndex(
                name: "IX_merchants_district",
                schema: "washgo",
                table: "merchants",
                column: "district");

            migrationBuilder.CreateIndex(
                name: "IX_merchants_latitude_longitude",
                schema: "washgo",
                table: "merchants",
                columns: new[] { "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_merchants_status",
                schema: "washgo",
                table: "merchants",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_merchants_tax_code",
                schema: "washgo",
                table: "merchants",
                column: "tax_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_otp_codes_email",
                schema: "washgo",
                table: "otp_codes",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "IX_otp_codes_email_purpose_is_used",
                schema: "washgo",
                table: "otp_codes",
                columns: new[] { "email", "purpose", "is_used" });

            migrationBuilder.CreateIndex(
                name: "IX_otp_codes_expiry_date",
                schema: "washgo",
                table: "otp_codes",
                column: "expiry_date");

            migrationBuilder.CreateIndex(
                name: "IX_otp_codes_purpose",
                schema: "washgo",
                table: "otp_codes",
                column: "purpose");

            migrationBuilder.CreateIndex(
                name: "IX_otp_codes_user_id",
                schema: "washgo",
                table: "otp_codes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token",
                schema: "washgo",
                table: "refresh_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id",
                schema: "washgo",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_name",
                schema: "washgo",
                table: "roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_areas_code",
                schema: "washgo",
                table: "service_areas",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_areas_created_by",
                schema: "washgo",
                table: "service_areas",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_service_areas_district",
                schema: "washgo",
                table: "service_areas",
                column: "district");

            migrationBuilder.CreateIndex(
                name: "IX_service_areas_is_active",
                schema: "washgo",
                table: "service_areas",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_service_change_requests_merchant_id",
                schema: "washgo",
                table: "service_change_requests",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_change_requests_reviewed_by",
                schema: "washgo",
                table: "service_change_requests",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_service_change_requests_service_id",
                schema: "washgo",
                table: "service_change_requests",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_change_requests_status",
                schema: "washgo",
                table: "service_change_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_shippers_merchant_id",
                schema: "washgo",
                table: "shippers",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "IX_shippers_status",
                schema: "washgo",
                table: "shippers",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_shippers_user_id",
                schema: "washgo",
                table: "shippers",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_auth_provider_provider_id",
                schema: "washgo",
                table: "users",
                columns: new[] { "auth_provider", "provider_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                schema: "washgo",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_role_id",
                schema: "washgo",
                table: "users",
                column: "role_id");

            migrationBuilder.AddForeignKey(
                name: "FK_locker_slots_lockers_locker_id",
                schema: "washgo",
                table: "locker_slots",
                column: "locker_id",
                principalSchema: "washgo",
                principalTable: "lockers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_lockers_merchants_merchant_id",
                schema: "washgo",
                table: "lockers",
                column: "merchant_id",
                principalSchema: "washgo",
                principalTable: "merchants",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_lockers_service_areas_service_area_id",
                schema: "washgo",
                table: "lockers",
                column: "service_area_id",
                principalSchema: "washgo",
                principalTable: "service_areas",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_lockers_users_approved_by",
                schema: "washgo",
                table: "lockers",
                column: "approved_by",
                principalSchema: "washgo",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_orders_order_id",
                schema: "washgo",
                table: "order_items",
                column: "order_id",
                principalSchema: "washgo",
                principalTable: "orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_services_service_id",
                schema: "washgo",
                table: "order_items",
                column: "service_id",
                principalSchema: "washgo",
                principalTable: "services",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_status_history_orders_order_id",
                schema: "washgo",
                table: "order_status_history",
                column: "order_id",
                principalSchema: "washgo",
                principalTable: "orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_customers_customer_id",
                schema: "washgo",
                table: "orders",
                column: "customer_id",
                principalSchema: "washgo",
                principalTable: "customers",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_locker_slots_locker_slot_id",
                schema: "washgo",
                table: "orders",
                column: "locker_slot_id",
                principalSchema: "washgo",
                principalTable: "locker_slots",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_locker_slots_return_box_id",
                schema: "washgo",
                table: "orders",
                column: "return_box_id",
                principalSchema: "washgo",
                principalTable: "locker_slots",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_lockers_locker_id",
                schema: "washgo",
                table: "orders",
                column: "locker_id",
                principalSchema: "washgo",
                principalTable: "lockers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_lockers_return_locker_id",
                schema: "washgo",
                table: "orders",
                column: "return_locker_id",
                principalSchema: "washgo",
                principalTable: "lockers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_merchants_merchant_id",
                schema: "washgo",
                table: "orders",
                column: "merchant_id",
                principalSchema: "washgo",
                principalTable: "merchants",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_shippers_shipper_id",
                schema: "washgo",
                table: "orders",
                column: "shipper_id",
                principalSchema: "washgo",
                principalTable: "shippers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_users_cancelled_by",
                schema: "washgo",
                table: "orders",
                column: "cancelled_by",
                principalSchema: "washgo",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_services_merchants_merchant_id",
                schema: "washgo",
                table: "services",
                column: "merchant_id",
                principalSchema: "washgo",
                principalTable: "merchants",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_services_users_approved_by",
                schema: "washgo",
                table: "services",
                column: "approved_by",
                principalSchema: "washgo",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_locker_slots_lockers_locker_id",
                schema: "washgo",
                table: "locker_slots");

            migrationBuilder.DropForeignKey(
                name: "FK_lockers_merchants_merchant_id",
                schema: "washgo",
                table: "lockers");

            migrationBuilder.DropForeignKey(
                name: "FK_lockers_service_areas_service_area_id",
                schema: "washgo",
                table: "lockers");

            migrationBuilder.DropForeignKey(
                name: "FK_lockers_users_approved_by",
                schema: "washgo",
                table: "lockers");

            migrationBuilder.DropForeignKey(
                name: "FK_order_items_orders_order_id",
                schema: "washgo",
                table: "order_items");

            migrationBuilder.DropForeignKey(
                name: "FK_order_items_services_service_id",
                schema: "washgo",
                table: "order_items");

            migrationBuilder.DropForeignKey(
                name: "FK_order_status_history_orders_order_id",
                schema: "washgo",
                table: "order_status_history");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_customers_customer_id",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_locker_slots_locker_slot_id",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_locker_slots_return_box_id",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_lockers_locker_id",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_lockers_return_locker_id",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_merchants_merchant_id",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_shippers_shipper_id",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_users_cancelled_by",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_services_merchants_merchant_id",
                schema: "washgo",
                table: "services");

            migrationBuilder.DropForeignKey(
                name: "FK_services_users_approved_by",
                schema: "washgo",
                table: "services");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "washgo");

            migrationBuilder.DropTable(
                name: "merchant_images",
                schema: "washgo");

            migrationBuilder.DropTable(
                name: "merchant_service_areas",
                schema: "washgo");

            migrationBuilder.DropTable(
                name: "otp_codes",
                schema: "washgo");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "washgo");

            migrationBuilder.DropTable(
                name: "service_change_requests",
                schema: "washgo");

            migrationBuilder.DropTable(
                name: "shippers",
                schema: "washgo");

            migrationBuilder.DropTable(
                name: "service_areas",
                schema: "washgo");

            migrationBuilder.DropTable(
                name: "merchants",
                schema: "washgo");

            migrationBuilder.DropTable(
                name: "users",
                schema: "washgo");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "washgo");

            migrationBuilder.DropIndex(
                name: "IX_services_approved_by",
                schema: "washgo",
                table: "services");

            migrationBuilder.DropIndex(
                name: "IX_services_merchant_id",
                schema: "washgo",
                table: "services");

            migrationBuilder.DropIndex(
                name: "IX_services_status",
                schema: "washgo",
                table: "services");

            migrationBuilder.DropIndex(
                name: "IX_orders_cancelled_by",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_customer_id",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_merchant_id",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_merchant_order_code",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_promotion_id",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_shipper_id",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_status",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_lockers_approved_by",
                schema: "washgo",
                table: "lockers");

            migrationBuilder.DropIndex(
                name: "IX_lockers_latitude_longitude",
                schema: "washgo",
                table: "lockers");

            migrationBuilder.DropIndex(
                name: "IX_lockers_merchant_id",
                schema: "washgo",
                table: "lockers");

            migrationBuilder.DropIndex(
                name: "IX_lockers_service_area_id",
                schema: "washgo",
                table: "lockers");

            migrationBuilder.DropIndex(
                name: "IX_lockers_status",
                schema: "washgo",
                table: "lockers");

            migrationBuilder.DropIndex(
                name: "IX_locker_slots_locker_id_slot_number",
                schema: "washgo",
                table: "locker_slots");

            migrationBuilder.DropIndex(
                name: "IX_locker_slots_status",
                schema: "washgo",
                table: "locker_slots");

            migrationBuilder.DropColumn(
                name: "allowed_statuses",
                schema: "washgo",
                table: "services");

            migrationBuilder.DropColumn(
                name: "approved_at",
                schema: "washgo",
                table: "services");

            migrationBuilder.DropColumn(
                name: "approved_by",
                schema: "washgo",
                table: "services");

            migrationBuilder.DropColumn(
                name: "images",
                schema: "washgo",
                table: "services");

            migrationBuilder.DropColumn(
                name: "merchant_id",
                schema: "washgo",
                table: "services");

            migrationBuilder.DropColumn(
                name: "price_per_item",
                schema: "washgo",
                table: "services");

            migrationBuilder.DropColumn(
                name: "price_per_kg",
                schema: "washgo",
                table: "services");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                schema: "washgo",
                table: "services");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "washgo",
                table: "services");

            migrationBuilder.DropColumn(
                name: "actual_item_count",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "actual_weight_kg",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "cancel_fee_amount",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "cancelled_by",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "cancelled_by_type",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "deposit_deadline",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "estimated_finish_at",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "eta_updated_at",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "finished_at",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "merchant_order_code",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "overdue_fee_total",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "payment_deadline",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "pickup_deadline",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "promotion_id",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "return_deadline",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "storage_fee_total",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "from_status",
                schema: "washgo",
                table: "order_status_history");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "washgo",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "weight_kg",
                schema: "washgo",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "approved_at",
                schema: "washgo",
                table: "lockers");

            migrationBuilder.DropColumn(
                name: "approved_by",
                schema: "washgo",
                table: "lockers");

            migrationBuilder.DropColumn(
                name: "available_slots",
                schema: "washgo",
                table: "lockers");

            migrationBuilder.DropColumn(
                name: "description",
                schema: "washgo",
                table: "lockers");

            migrationBuilder.DropColumn(
                name: "images",
                schema: "washgo",
                table: "lockers");

            migrationBuilder.DropColumn(
                name: "locker_type",
                schema: "washgo",
                table: "lockers");

            migrationBuilder.DropColumn(
                name: "merchant_id",
                schema: "washgo",
                table: "lockers");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                schema: "washgo",
                table: "lockers");

            migrationBuilder.DropColumn(
                name: "service_area_id",
                schema: "washgo",
                table: "lockers");

            migrationBuilder.DropColumn(
                name: "last_used_at",
                schema: "washgo",
                table: "locker_slots");

            migrationBuilder.DropColumn(
                name: "slot_number",
                schema: "washgo",
                table: "locker_slots");

            migrationBuilder.RenameColumn(
                name: "unit",
                schema: "washgo",
                table: "services",
                newName: "Unit");

            migrationBuilder.RenameColumn(
                name: "name",
                schema: "washgo",
                table: "services",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "modified",
                schema: "washgo",
                table: "services",
                newName: "Modified");

            migrationBuilder.RenameColumn(
                name: "description",
                schema: "washgo",
                table: "services",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "created",
                schema: "washgo",
                table: "services",
                newName: "Created");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "washgo",
                table: "services",
                newName: "Code");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "washgo",
                table: "services",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "search_vector",
                schema: "washgo",
                table: "services",
                newName: "SearchVector");

            migrationBuilder.RenameColumn(
                name: "modified_on",
                schema: "washgo",
                table: "services",
                newName: "ModifiedOn");

            migrationBuilder.RenameColumn(
                name: "modified_by",
                schema: "washgo",
                table: "services",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                schema: "washgo",
                table: "services",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "is_active",
                schema: "washgo",
                table: "services",
                newName: "IsActive");

            migrationBuilder.RenameColumn(
                name: "estimated_time",
                schema: "washgo",
                table: "services",
                newName: "EstimatedDurationHours");

            migrationBuilder.RenameColumn(
                name: "deleted_on",
                schema: "washgo",
                table: "services",
                newName: "DeletedOn");

            migrationBuilder.RenameColumn(
                name: "deleted_by",
                schema: "washgo",
                table: "services",
                newName: "DeletedBy");

            migrationBuilder.RenameColumn(
                name: "created_on",
                schema: "washgo",
                table: "services",
                newName: "CreatedOn");

            migrationBuilder.RenameColumn(
                name: "created_by",
                schema: "washgo",
                table: "services",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "category",
                schema: "washgo",
                table: "services",
                newName: "ServiceType");

            migrationBuilder.RenameColumn(
                name: "base_price",
                schema: "washgo",
                table: "services",
                newName: "UnitPrice");

            migrationBuilder.RenameIndex(
                name: "IX_services_code",
                schema: "washgo",
                table: "services",
                newName: "IX_services_Code");

            migrationBuilder.RenameIndex(
                name: "IX_services_search_vector",
                schema: "washgo",
                table: "services",
                newName: "IX_services_SearchVector");

            migrationBuilder.RenameColumn(
                name: "status",
                schema: "washgo",
                table: "orders",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "modified",
                schema: "washgo",
                table: "orders",
                newName: "Modified");

            migrationBuilder.RenameColumn(
                name: "created",
                schema: "washgo",
                table: "orders",
                newName: "Created");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "washgo",
                table: "orders",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "total_amount",
                schema: "washgo",
                table: "orders",
                newName: "TotalAmount");

            migrationBuilder.RenameColumn(
                name: "slot_number",
                schema: "washgo",
                table: "orders",
                newName: "BoxNumber");

            migrationBuilder.RenameColumn(
                name: "shipper_name",
                schema: "washgo",
                table: "orders",
                newName: "ShipperName");

            migrationBuilder.RenameColumn(
                name: "shipper_id",
                schema: "washgo",
                table: "orders",
                newName: "ShipperId");

            migrationBuilder.RenameColumn(
                name: "search_vector",
                schema: "washgo",
                table: "orders",
                newName: "SearchVector");

            migrationBuilder.RenameColumn(
                name: "return_locker_id",
                schema: "washgo",
                table: "orders",
                newName: "ReturnLockerId");

            migrationBuilder.RenameColumn(
                name: "return_box_number",
                schema: "washgo",
                table: "orders",
                newName: "ReturnBoxNumber");

            migrationBuilder.RenameColumn(
                name: "return_box_id",
                schema: "washgo",
                table: "orders",
                newName: "ReturnBoxId");

            migrationBuilder.RenameColumn(
                name: "qr_code",
                schema: "washgo",
                table: "orders",
                newName: "QrCodeString");

            migrationBuilder.RenameColumn(
                name: "pickup_pin_code",
                schema: "washgo",
                table: "orders",
                newName: "PickupPinCode");

            migrationBuilder.RenameColumn(
                name: "payment_status",
                schema: "washgo",
                table: "orders",
                newName: "PaymentStatus");

            migrationBuilder.RenameColumn(
                name: "payment_method",
                schema: "washgo",
                table: "orders",
                newName: "PaymentMethod");

            migrationBuilder.RenameColumn(
                name: "order_code",
                schema: "washgo",
                table: "orders",
                newName: "OrderCode");

            migrationBuilder.RenameColumn(
                name: "note",
                schema: "washgo",
                table: "orders",
                newName: "CustomerNote");

            migrationBuilder.RenameColumn(
                name: "modified_on",
                schema: "washgo",
                table: "orders",
                newName: "ModifiedOn");

            migrationBuilder.RenameColumn(
                name: "modified_by",
                schema: "washgo",
                table: "orders",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "merchant_name",
                schema: "washgo",
                table: "orders",
                newName: "MerchantName");

            migrationBuilder.RenameColumn(
                name: "merchant_id",
                schema: "washgo",
                table: "orders",
                newName: "MerchantId");

            migrationBuilder.RenameColumn(
                name: "locker_slot_id",
                schema: "washgo",
                table: "orders",
                newName: "BoxId");

            migrationBuilder.RenameColumn(
                name: "locker_name",
                schema: "washgo",
                table: "orders",
                newName: "LockerName");

            migrationBuilder.RenameColumn(
                name: "locker_id",
                schema: "washgo",
                table: "orders",
                newName: "LockerId");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                schema: "washgo",
                table: "orders",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "final_amount",
                schema: "washgo",
                table: "orders",
                newName: "FinalAmount");

            migrationBuilder.RenameColumn(
                name: "expected_delivery_time",
                schema: "washgo",
                table: "orders",
                newName: "ExpectedDeliveryTime");

            migrationBuilder.RenameColumn(
                name: "discount_amount",
                schema: "washgo",
                table: "orders",
                newName: "DiscountAmount");

            migrationBuilder.RenameColumn(
                name: "deposit_pin_code",
                schema: "washgo",
                table: "orders",
                newName: "DepositPinCode");

            migrationBuilder.RenameColumn(
                name: "deleted_on",
                schema: "washgo",
                table: "orders",
                newName: "DeletedOn");

            migrationBuilder.RenameColumn(
                name: "deleted_by",
                schema: "washgo",
                table: "orders",
                newName: "DeletedBy");

            migrationBuilder.RenameColumn(
                name: "customer_phone_number",
                schema: "washgo",
                table: "orders",
                newName: "CustomerPhoneNumber");

            migrationBuilder.RenameColumn(
                name: "customer_name",
                schema: "washgo",
                table: "orders",
                newName: "CustomerName");

            migrationBuilder.RenameColumn(
                name: "customer_id",
                schema: "washgo",
                table: "orders",
                newName: "CustomerId");

            migrationBuilder.RenameColumn(
                name: "created_on",
                schema: "washgo",
                table: "orders",
                newName: "CreatedOn");

            migrationBuilder.RenameColumn(
                name: "created_by",
                schema: "washgo",
                table: "orders",
                newName: "CreatedBy");

            migrationBuilder.RenameIndex(
                name: "IX_orders_search_vector",
                schema: "washgo",
                table: "orders",
                newName: "IX_orders_SearchVector");

            migrationBuilder.RenameIndex(
                name: "IX_orders_return_locker_id",
                schema: "washgo",
                table: "orders",
                newName: "IX_orders_ReturnLockerId");

            migrationBuilder.RenameIndex(
                name: "IX_orders_return_box_id",
                schema: "washgo",
                table: "orders",
                newName: "IX_orders_ReturnBoxId");

            migrationBuilder.RenameIndex(
                name: "IX_orders_order_code",
                schema: "washgo",
                table: "orders",
                newName: "IX_orders_OrderCode");

            migrationBuilder.RenameIndex(
                name: "IX_orders_locker_slot_id",
                schema: "washgo",
                table: "orders",
                newName: "IX_orders_BoxId");

            migrationBuilder.RenameIndex(
                name: "IX_orders_locker_id",
                schema: "washgo",
                table: "orders",
                newName: "IX_orders_LockerId");

            migrationBuilder.RenameColumn(
                name: "note",
                schema: "washgo",
                table: "order_status_history",
                newName: "Note");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "washgo",
                table: "order_status_history",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "to_status",
                schema: "washgo",
                table: "order_status_history",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "order_id",
                schema: "washgo",
                table: "order_status_history",
                newName: "WashOrderId");

            migrationBuilder.RenameColumn(
                name: "created_at",
                schema: "washgo",
                table: "order_status_history",
                newName: "Timestamp");

            migrationBuilder.RenameColumn(
                name: "changed_by_type",
                schema: "washgo",
                table: "order_status_history",
                newName: "ActorRole");

            migrationBuilder.RenameColumn(
                name: "changed_by",
                schema: "washgo",
                table: "order_status_history",
                newName: "ActorId");

            migrationBuilder.RenameColumn(
                name: "actor_name",
                schema: "washgo",
                table: "order_status_history",
                newName: "ActorName");

            migrationBuilder.RenameIndex(
                name: "IX_order_status_history_order_id",
                schema: "washgo",
                table: "order_status_history",
                newName: "IX_order_status_history_WashOrderId");

            migrationBuilder.RenameColumn(
                name: "quantity",
                schema: "washgo",
                table: "order_items",
                newName: "Quantity");

            migrationBuilder.RenameColumn(
                name: "note",
                schema: "washgo",
                table: "order_items",
                newName: "Note");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "washgo",
                table: "order_items",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "unit_price",
                schema: "washgo",
                table: "order_items",
                newName: "UnitPrice");

            migrationBuilder.RenameColumn(
                name: "total_price",
                schema: "washgo",
                table: "order_items",
                newName: "SubTotal");

            migrationBuilder.RenameColumn(
                name: "service_name",
                schema: "washgo",
                table: "order_items",
                newName: "ServiceName");

            migrationBuilder.RenameColumn(
                name: "service_id",
                schema: "washgo",
                table: "order_items",
                newName: "ServiceId");

            migrationBuilder.RenameColumn(
                name: "order_id",
                schema: "washgo",
                table: "order_items",
                newName: "WashOrderId");

            migrationBuilder.RenameIndex(
                name: "IX_order_items_service_id",
                schema: "washgo",
                table: "order_items",
                newName: "IX_order_items_ServiceId");

            migrationBuilder.RenameIndex(
                name: "IX_order_items_order_id",
                schema: "washgo",
                table: "order_items",
                newName: "IX_order_items_WashOrderId");

            migrationBuilder.RenameColumn(
                name: "ward",
                schema: "washgo",
                table: "lockers",
                newName: "Ward");

            migrationBuilder.RenameColumn(
                name: "status",
                schema: "washgo",
                table: "lockers",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "name",
                schema: "washgo",
                table: "lockers",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "modified",
                schema: "washgo",
                table: "lockers",
                newName: "Modified");

            migrationBuilder.RenameColumn(
                name: "longitude",
                schema: "washgo",
                table: "lockers",
                newName: "Longitude");

            migrationBuilder.RenameColumn(
                name: "latitude",
                schema: "washgo",
                table: "lockers",
                newName: "Latitude");

            migrationBuilder.RenameColumn(
                name: "district",
                schema: "washgo",
                table: "lockers",
                newName: "District");

            migrationBuilder.RenameColumn(
                name: "created",
                schema: "washgo",
                table: "lockers",
                newName: "Created");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "washgo",
                table: "lockers",
                newName: "Code");

            migrationBuilder.RenameColumn(
                name: "city",
                schema: "washgo",
                table: "lockers",
                newName: "City");

            migrationBuilder.RenameColumn(
                name: "address",
                schema: "washgo",
                table: "lockers",
                newName: "Address");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "washgo",
                table: "lockers",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "total_slots",
                schema: "washgo",
                table: "lockers",
                newName: "TotalBoxes");

            migrationBuilder.RenameColumn(
                name: "search_vector",
                schema: "washgo",
                table: "lockers",
                newName: "SearchVector");

            migrationBuilder.RenameColumn(
                name: "modified_on",
                schema: "washgo",
                table: "lockers",
                newName: "ModifiedOn");

            migrationBuilder.RenameColumn(
                name: "modified_by",
                schema: "washgo",
                table: "lockers",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                schema: "washgo",
                table: "lockers",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "deleted_on",
                schema: "washgo",
                table: "lockers",
                newName: "DeletedOn");

            migrationBuilder.RenameColumn(
                name: "deleted_by",
                schema: "washgo",
                table: "lockers",
                newName: "DeletedBy");

            migrationBuilder.RenameColumn(
                name: "created_on",
                schema: "washgo",
                table: "lockers",
                newName: "CreatedOn");

            migrationBuilder.RenameColumn(
                name: "created_by",
                schema: "washgo",
                table: "lockers",
                newName: "CreatedBy");

            migrationBuilder.RenameIndex(
                name: "IX_lockers_code",
                schema: "washgo",
                table: "lockers",
                newName: "IX_lockers_Code");

            migrationBuilder.RenameIndex(
                name: "IX_lockers_search_vector",
                schema: "washgo",
                table: "lockers",
                newName: "IX_lockers_SearchVector");

            migrationBuilder.RenameColumn(
                name: "status",
                schema: "washgo",
                table: "locker_slots",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "size",
                schema: "washgo",
                table: "locker_slots",
                newName: "Size");

            migrationBuilder.RenameColumn(
                name: "modified",
                schema: "washgo",
                table: "locker_slots",
                newName: "Modified");

            migrationBuilder.RenameColumn(
                name: "created",
                schema: "washgo",
                table: "locker_slots",
                newName: "Created");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "washgo",
                table: "locker_slots",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "search_vector",
                schema: "washgo",
                table: "locker_slots",
                newName: "SearchVector");

            migrationBuilder.RenameColumn(
                name: "modified_on",
                schema: "washgo",
                table: "locker_slots",
                newName: "ModifiedOn");

            migrationBuilder.RenameColumn(
                name: "modified_by",
                schema: "washgo",
                table: "locker_slots",
                newName: "ModifiedBy");

            migrationBuilder.RenameColumn(
                name: "locker_id",
                schema: "washgo",
                table: "locker_slots",
                newName: "LockerId");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                schema: "washgo",
                table: "locker_slots",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "deleted_on",
                schema: "washgo",
                table: "locker_slots",
                newName: "DeletedOn");

            migrationBuilder.RenameColumn(
                name: "deleted_by",
                schema: "washgo",
                table: "locker_slots",
                newName: "DeletedBy");

            migrationBuilder.RenameColumn(
                name: "created_on",
                schema: "washgo",
                table: "locker_slots",
                newName: "CreatedOn");

            migrationBuilder.RenameColumn(
                name: "created_by",
                schema: "washgo",
                table: "locker_slots",
                newName: "CreatedBy");

            migrationBuilder.RenameIndex(
                name: "IX_locker_slots_search_vector",
                schema: "washgo",
                table: "locker_slots",
                newName: "IX_locker_slots_SearchVector");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "washgo",
                table: "services",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "QrCodeString",
                schema: "washgo",
                table: "orders",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "washgo",
                table: "lockers",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                schema: "washgo",
                table: "lockers",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "TotalBoxes",
                schema: "washgo",
                table: "lockers",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 10);

            migrationBuilder.AddColumn<string>(
                name: "BoxNumber",
                schema: "washgo",
                table: "locker_slots",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CurrentPinCode",
                schema: "washgo",
                table: "locker_slots",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_locker_slots_LockerId",
                schema: "washgo",
                table: "locker_slots",
                column: "LockerId");

            migrationBuilder.AddForeignKey(
                name: "FK_locker_slots_lockers_LockerId",
                schema: "washgo",
                table: "locker_slots",
                column: "LockerId",
                principalSchema: "washgo",
                principalTable: "lockers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_orders_WashOrderId",
                schema: "washgo",
                table: "order_items",
                column: "WashOrderId",
                principalSchema: "washgo",
                principalTable: "orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_services_ServiceId",
                schema: "washgo",
                table: "order_items",
                column: "ServiceId",
                principalSchema: "washgo",
                principalTable: "services",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_status_history_orders_WashOrderId",
                schema: "washgo",
                table: "order_status_history",
                column: "WashOrderId",
                principalSchema: "washgo",
                principalTable: "orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_locker_slots_BoxId",
                schema: "washgo",
                table: "orders",
                column: "BoxId",
                principalSchema: "washgo",
                principalTable: "locker_slots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_locker_slots_ReturnBoxId",
                schema: "washgo",
                table: "orders",
                column: "ReturnBoxId",
                principalSchema: "washgo",
                principalTable: "locker_slots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_lockers_LockerId",
                schema: "washgo",
                table: "orders",
                column: "LockerId",
                principalSchema: "washgo",
                principalTable: "lockers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_lockers_ReturnLockerId",
                schema: "washgo",
                table: "orders",
                column: "ReturnLockerId",
                principalSchema: "washgo",
                principalTable: "lockers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
