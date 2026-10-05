using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class InitialWashGoSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "washgo");

            migrationBuilder.CreateTable(
                name: "lockers",
                schema: "washgo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Address = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Ward = table.Column<string>(type: "text", nullable: true),
                    District = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalBoxes = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedOn = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "EXTRACT(EPOCH FROM now())::bigint"),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedOn = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "EXTRACT(EPOCH FROM now())::bigint"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedOn = table.Column<long>(type: "bigint", nullable: true),
                    SearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true),
                    Created = table.Column<string>(type: "jsonb", nullable: true),
                    Modified = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lockers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "services",
                schema: "washgo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ServiceType = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EstimatedDurationHours = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedOn = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "EXTRACT(EPOCH FROM now())::bigint"),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedOn = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "EXTRACT(EPOCH FROM now())::bigint"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedOn = table.Column<long>(type: "bigint", nullable: true),
                    SearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true),
                    Created = table.Column<string>(type: "jsonb", nullable: true),
                    Modified = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_services", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "locker_slots",
                schema: "washgo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LockerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoxNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Size = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentPinCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedOn = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "EXTRACT(EPOCH FROM now())::bigint"),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedOn = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "EXTRACT(EPOCH FROM now())::bigint"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedOn = table.Column<long>(type: "bigint", nullable: true),
                    SearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true),
                    Created = table.Column<string>(type: "jsonb", nullable: true),
                    Modified = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locker_slots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_locker_slots_lockers_LockerId",
                        column: x => x.LockerId,
                        principalSchema: "washgo",
                        principalTable: "lockers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                schema: "washgo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CustomerPhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LockerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    BoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoxNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReturnLockerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReturnBoxId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReturnBoxNumber = table.Column<string>(type: "text", nullable: true),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: true),
                    MerchantName = table.Column<string>(type: "text", nullable: true),
                    ShipperId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShipperName = table.Column<string>(type: "text", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FinalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PaymentStatus = table.Column<int>(type: "integer", nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: false),
                    DepositPinCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PickupPinCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    QrCodeString = table.Column<string>(type: "text", nullable: false),
                    ExpectedDeliveryTime = table.Column<long>(type: "bigint", nullable: false),
                    CustomerNote = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedOn = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "EXTRACT(EPOCH FROM now())::bigint"),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedOn = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "EXTRACT(EPOCH FROM now())::bigint"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedOn = table.Column<long>(type: "bigint", nullable: true),
                    SearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true),
                    Created = table.Column<string>(type: "jsonb", nullable: true),
                    Modified = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_orders_locker_slots_BoxId",
                        column: x => x.BoxId,
                        principalSchema: "washgo",
                        principalTable: "locker_slots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_locker_slots_ReturnBoxId",
                        column: x => x.ReturnBoxId,
                        principalSchema: "washgo",
                        principalTable: "locker_slots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_lockers_LockerId",
                        column: x => x.LockerId,
                        principalSchema: "washgo",
                        principalTable: "lockers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_lockers_ReturnLockerId",
                        column: x => x.ReturnLockerId,
                        principalSchema: "washgo",
                        principalTable: "lockers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                schema: "washgo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WashOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SubTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_items_orders_WashOrderId",
                        column: x => x.WashOrderId,
                        principalSchema: "washgo",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_order_items_services_ServiceId",
                        column: x => x.ServiceId,
                        principalSchema: "washgo",
                        principalTable: "services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_status_history",
                schema: "washgo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WashOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ActorRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Timestamp = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_status_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_status_history_orders_WashOrderId",
                        column: x => x.WashOrderId,
                        principalSchema: "washgo",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_locker_slots_LockerId",
                schema: "washgo",
                table: "locker_slots",
                column: "LockerId");

            migrationBuilder.CreateIndex(
                name: "IX_locker_slots_SearchVector",
                schema: "washgo",
                table: "locker_slots",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_lockers_Code",
                schema: "washgo",
                table: "lockers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lockers_SearchVector",
                schema: "washgo",
                table: "lockers",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_ServiceId",
                schema: "washgo",
                table: "order_items",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_WashOrderId",
                schema: "washgo",
                table: "order_items",
                column: "WashOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_order_status_history_WashOrderId",
                schema: "washgo",
                table: "order_status_history",
                column: "WashOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_BoxId",
                schema: "washgo",
                table: "orders",
                column: "BoxId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_LockerId",
                schema: "washgo",
                table: "orders",
                column: "LockerId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_OrderCode",
                schema: "washgo",
                table: "orders",
                column: "OrderCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_ReturnBoxId",
                schema: "washgo",
                table: "orders",
                column: "ReturnBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_ReturnLockerId",
                schema: "washgo",
                table: "orders",
                column: "ReturnLockerId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_SearchVector",
                schema: "washgo",
                table: "orders",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_services_Code",
                schema: "washgo",
                table: "services",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_services_SearchVector",
                schema: "washgo",
                table: "services",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_items",
                schema: "washgo");

            migrationBuilder.DropTable(
                name: "order_status_history",
                schema: "washgo");

            migrationBuilder.DropTable(
                name: "services",
                schema: "washgo");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "washgo");

            migrationBuilder.DropTable(
                name: "locker_slots",
                schema: "washgo");

            migrationBuilder.DropTable(
                name: "lockers",
                schema: "washgo");
        }
    }
}
