using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddCombinedOrderPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "cancellation_amount",
                schema: "washgo",
                table: "payment_transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "storage_amount",
                schema: "washgo",
                table: "payment_transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "cancellation_fee_basis",
                schema: "washgo",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cancellation_fee_percent",
                schema: "washgo",
                table: "orders",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancellation_amount",
                schema: "washgo",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "storage_amount",
                schema: "washgo",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "cancellation_fee_basis",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "cancellation_fee_percent",
                schema: "washgo",
                table: "orders");
        }
    }
}
