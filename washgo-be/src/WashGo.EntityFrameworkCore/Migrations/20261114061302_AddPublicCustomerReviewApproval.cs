using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicCustomerReviewApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "public_approved_at",
                schema: "washgo",
                table: "order_ratings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "public_approved_by",
                schema: "washgo",
                table: "order_ratings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                schema: "washgo",
                table: "order_ratings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_ratings_public_approved_at_public_id",
                schema: "washgo",
                table: "order_ratings",
                columns: new[] { "public_approved_at", "public_id" });

            migrationBuilder.CreateIndex(
                name: "IX_order_ratings_public_id",
                schema: "washgo",
                table: "order_ratings",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_order_ratings_public_approved_at_public_id",
                schema: "washgo",
                table: "order_ratings");

            migrationBuilder.DropIndex(
                name: "IX_order_ratings_public_id",
                schema: "washgo",
                table: "order_ratings");

            migrationBuilder.DropColumn(
                name: "public_approved_at",
                schema: "washgo",
                table: "order_ratings");

            migrationBuilder.DropColumn(
                name: "public_approved_by",
                schema: "washgo",
                table: "order_ratings");

            migrationBuilder.DropColumn(
                name: "public_id",
                schema: "washgo",
                table: "order_ratings");
        }
    }
}
