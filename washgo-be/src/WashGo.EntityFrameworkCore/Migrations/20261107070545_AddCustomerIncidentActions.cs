using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerIncidentActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "client_request_id",
                schema: "washgo",
                table: "incidents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "customer_updated_at",
                schema: "washgo",
                table: "incidents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "withdraw_reason",
                schema: "washgo",
                table: "incidents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "withdrawn_at",
                schema: "washgo",
                table: "incidents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "incident_messages",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sender_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    image_urls = table.Column<List<string>>(type: "text[]", nullable: false),
                    video_urls = table.Column<List<string>>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_incident_messages_incidents_incident_id",
                        column: x => x.incident_id,
                        principalSchema: "washgo",
                        principalTable: "incidents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_incidents_customer_id_client_request_id",
                schema: "washgo",
                table: "incidents",
                columns: new[] { "customer_id", "client_request_id" },
                unique: true,
                filter: "client_request_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_incident_messages_incident_id_created_at",
                schema: "washgo",
                table: "incident_messages",
                columns: new[] { "incident_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incident_messages",
                schema: "washgo");

            migrationBuilder.DropIndex(
                name: "IX_incidents_customer_id_client_request_id",
                schema: "washgo",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "client_request_id",
                schema: "washgo",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "customer_updated_at",
                schema: "washgo",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "withdraw_reason",
                schema: "washgo",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "withdrawn_at",
                schema: "washgo",
                table: "incidents");
        }
    }
}
