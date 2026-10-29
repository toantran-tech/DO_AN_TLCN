using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddActualWeightPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "base_processing_hours",
                schema: "washgo",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "included_weight_kg",
                schema: "washgo",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "weight_surcharge_amount",
                schema: "washgo",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "is_weight_based",
                schema: "washgo",
                table: "order_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "base_processing_hours",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "included_weight_kg",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "weight_surcharge_amount",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "is_weight_based",
                schema: "washgo",
                table: "order_items");
        }
    }
}
