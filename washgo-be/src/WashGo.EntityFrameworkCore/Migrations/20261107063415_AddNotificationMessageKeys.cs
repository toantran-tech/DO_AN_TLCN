using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationMessageKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "message_args",
                schema: "washgo",
                table: "notifications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "message_key",
                schema: "washgo",
                table: "notifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id_type",
                schema: "washgo",
                table: "notifications",
                columns: new[] { "user_id", "type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notifications_user_id_type",
                schema: "washgo",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "message_args",
                schema: "washgo",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "message_key",
                schema: "washgo",
                table: "notifications");
        }
    }
}
