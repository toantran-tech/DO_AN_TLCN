using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddRatingModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "moderated_at",
                schema: "washgo",
                table: "order_ratings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "moderated_by",
                schema: "washgo",
                table: "order_ratings",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "moderated_at",
                schema: "washgo",
                table: "order_ratings");

            migrationBuilder.DropColumn(
                name: "moderated_by",
                schema: "washgo",
                table: "order_ratings");
        }
    }
}
