using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddOverdueStorageTransfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "moved_to_storage_at",
                schema: "washgo",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "storage_location",
                schema: "washgo",
                table: "orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "storage_reference",
                schema: "washgo",
                table: "orders",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "moved_to_storage_at",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "storage_location",
                schema: "washgo",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "storage_reference",
                schema: "washgo",
                table: "orders");
        }
    }
}
