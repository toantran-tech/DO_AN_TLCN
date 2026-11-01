using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "promotion_code",
                schema: "washgo",
                table: "orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "promotions",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    discount_type = table.Column<int>(type: "integer", nullable: false),
                    discount_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    min_order_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    max_discount_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    start_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usage_limit = table.Column<int>(type: "integer", nullable: true),
                    usage_limit_per_customer = table.Column<int>(type: "integer", nullable: true),
                    used_count = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true),
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
                    table.PrimaryKey("PK_promotions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "promotion_usages",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    promotion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_reverted = table.Column<bool>(type: "boolean", nullable: false),
                    reverted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true),
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
                    table.PrimaryKey("PK_promotion_usages", x => x.id);
                    table.ForeignKey(
                        name: "FK_promotion_usages_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "washgo",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_promotion_usages_promotions_promotion_id",
                        column: x => x.promotion_id,
                        principalSchema: "washgo",
                        principalTable: "promotions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_promotion_usages_order_id",
                schema: "washgo",
                table: "promotion_usages",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_promotion_usages_promotion_id_customer_id",
                schema: "washgo",
                table: "promotion_usages",
                columns: new[] { "promotion_id", "customer_id" });

            migrationBuilder.CreateIndex(
                name: "IX_promotions_code",
                schema: "washgo",
                table: "promotions",
                column: "code",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_promotions_is_active",
                schema: "washgo",
                table: "promotions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_promotions_start_at_end_at",
                schema: "washgo",
                table: "promotions",
                columns: new[] { "start_at", "end_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "promotion_usages",
                schema: "washgo");

            migrationBuilder.DropTable(
                name: "promotions",
                schema: "washgo");

            migrationBuilder.DropColumn(
                name: "promotion_code",
                schema: "washgo",
                table: "orders");
        }
    }
}
