using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddRatingContentApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "content_approved_at",
                schema: "washgo",
                table: "order_ratings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "content_approved_by",
                schema: "washgo",
                table: "order_ratings",
                type: "uuid",
                nullable: true);

            // Preserve previously released content only when both review time and reviewer were recorded.
            // Hidden or unreviewed legacy rows remain unapproved. No change to stars or aggregate scores.
            migrationBuilder.Sql("""
                UPDATE washgo.order_ratings
                SET content_approved_at = moderated_at, content_approved_by = moderated_by
                WHERE NOT is_deleted AND NOT is_hidden
                  AND moderated_at IS NOT NULL AND moderated_by IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "content_approved_at",
                schema: "washgo",
                table: "order_ratings");

            migrationBuilder.DropColumn(
                name: "content_approved_by",
                schema: "washgo",
                table: "order_ratings");
        }
    }
}
