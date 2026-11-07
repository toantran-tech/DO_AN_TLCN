using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderWriteSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "revision",
                schema: "washgo",
                table: "orders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "order_operations",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    request = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    result_status = table.Column<int>(type: "integer", nullable: false),
                    result_revision = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_operations", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_operations_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "washgo",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_operations_order_id",
                schema: "washgo",
                table: "order_operations",
                column: "order_id");

            // Every UPDATE of an order (EF, raw SQL, background workers) bumps its revision.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION washgo.bump_order_revision() RETURNS trigger AS $$
BEGIN
    NEW.revision := OLD.revision + 1;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;
CREATE TRIGGER trg_orders_revision BEFORE UPDATE ON washgo.orders
    FOR EACH ROW EXECUTE FUNCTION washgo.bump_order_revision();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_orders_revision ON washgo.orders;
DROP FUNCTION IF EXISTS washgo.bump_order_revision();");

            migrationBuilder.DropTable(
                name: "order_operations",
                schema: "washgo");

            migrationBuilder.DropColumn(
                name: "revision",
                schema: "washgo",
                table: "orders");
        }
    }
}
