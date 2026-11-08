using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentReviewRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "compensation_percent_applied",
                schema: "washgo",
                table: "incidents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "compensation_settings_version",
                schema: "washgo",
                table: "incidents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "revision",
                schema: "washgo",
                table: "incidents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "compensation_percent_applied",
                schema: "washgo",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "compensation_settings_version",
                schema: "washgo",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "revision",
                schema: "washgo",
                table: "incidents");
        }
    }
}
