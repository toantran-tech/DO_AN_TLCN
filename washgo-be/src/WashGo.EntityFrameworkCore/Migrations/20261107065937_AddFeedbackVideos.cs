using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackVideos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "video_urls",
                schema: "washgo",
                table: "order_ratings",
                type: "text[]",
                nullable: false,
                // Existing ratings/incidents have no videos.
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddColumn<List<string>>(
                name: "video_urls",
                schema: "washgo",
                table: "incidents",
                type: "text[]",
                nullable: false,
                // Existing ratings/incidents have no videos.
                defaultValueSql: "'{}'::text[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "video_urls",
                schema: "washgo",
                table: "order_ratings");

            migrationBuilder.DropColumn(
                name: "video_urls",
                schema: "washgo",
                table: "incidents");
        }
    }
}
