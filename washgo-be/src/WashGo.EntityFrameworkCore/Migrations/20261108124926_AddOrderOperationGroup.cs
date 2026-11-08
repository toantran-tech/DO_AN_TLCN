using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderOperationGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                schema: "washgo",
                table: "order_operations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_operations_group_id",
                schema: "washgo",
                table: "order_operations",
                column: "group_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_order_operations_group_id",
                schema: "washgo",
                table: "order_operations");

            migrationBuilder.DropColumn(
                name: "group_id",
                schema: "washgo",
                table: "order_operations");
        }
    }
}
