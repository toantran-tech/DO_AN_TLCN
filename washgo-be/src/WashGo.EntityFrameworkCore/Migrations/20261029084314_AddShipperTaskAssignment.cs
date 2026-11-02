using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddShipperTaskAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "assigned_task_type",
                schema: "washgo",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "shipper_assigned_at",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            // Only a deposited legacy order unambiguously represents a collection task.
            // Paid legacy orders require an explicit return assignment.
            migrationBuilder.Sql("UPDATE washgo.orders SET assigned_task_type = 1 WHERE shipper_id IS NOT NULL AND status = 2 AND is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "assigned_task_type",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "shipper_assigned_at",
                schema: "washgo",
                table: "orders");
        }
    }
}
