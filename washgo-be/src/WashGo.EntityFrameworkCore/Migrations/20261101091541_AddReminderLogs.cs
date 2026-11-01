using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reminder_logs",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reminder_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reminder_logs_key",
                schema: "washgo",
                table: "reminder_logs",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reminder_logs_order_id",
                schema: "washgo",
                table: "reminder_logs",
                column: "order_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reminder_logs",
                schema: "washgo");
        }
    }
}
