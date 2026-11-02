using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddRatingsAndIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "incidents",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    merchant_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    type = table.Column<int>(type: "integer", nullable: false),
                    severity = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    image_urls = table.Column<List<string>>(type: "text[]", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    resolution_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    responsible_party = table.Column<int>(type: "integer", nullable: true),
                    compensation_level = table.Column<int>(type: "integer", nullable: false),
                    item_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    compensation_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    compensation_status = table.Column<int>(type: "integer", nullable: false),
                    compensation_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    compensation_paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_incidents", x => x.id);
                    table.ForeignKey(
                        name: "FK_incidents_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "washgo",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_ratings",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    merchant_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    overall_stars = table.Column<int>(type: "integer", nullable: false),
                    quality_stars = table.Column<int>(type: "integer", nullable: true),
                    speed_stars = table.Column<int>(type: "integer", nullable: true),
                    attitude_stars = table.Column<int>(type: "integer", nullable: true),
                    packaging_stars = table.Column<int>(type: "integer", nullable: true),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    image_urls = table.Column<List<string>>(type: "text[]", nullable: false),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false),
                    hidden_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    hidden_by = table.Column<Guid>(type: "uuid", nullable: true),
                    hidden_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_order_ratings", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_ratings_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "washgo",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_incidents_customer_id",
                schema: "washgo",
                table: "incidents",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_merchant_id",
                schema: "washgo",
                table: "incidents",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_order_id",
                schema: "washgo",
                table: "incidents",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_status_severity",
                schema: "washgo",
                table: "incidents",
                columns: new[] { "status", "severity" });

            migrationBuilder.CreateIndex(
                name: "IX_order_ratings_merchant_id_is_hidden",
                schema: "washgo",
                table: "order_ratings",
                columns: new[] { "merchant_id", "is_hidden" });

            migrationBuilder.CreateIndex(
                name: "IX_order_ratings_order_id",
                schema: "washgo",
                table: "order_ratings",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_ratings_overall_stars",
                schema: "washgo",
                table: "order_ratings",
                column: "overall_stars");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incidents",
                schema: "washgo");

            migrationBuilder.DropTable(
                name: "order_ratings",
                schema: "washgo");
        }
    }
}
