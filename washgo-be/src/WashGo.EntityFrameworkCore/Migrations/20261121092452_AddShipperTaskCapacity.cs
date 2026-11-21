using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddShipperTaskCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "max_active_tasks_per_shipper",
                schema: "washgo",
                table: "system_settings",
                type: "integer",
                nullable: false,
                // Existing deployments start at the documented default; it is never 0 (no implicit "unlimited").
                defaultValue: 3);

            migrationBuilder.UpdateData(
                schema: "washgo",
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("5e771165-0000-4000-8000-000000000001"),
                column: "max_active_tasks_per_shipper",
                value: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "max_active_tasks_per_shipper",
                schema: "washgo",
                table: "system_settings");
        }
    }
}
