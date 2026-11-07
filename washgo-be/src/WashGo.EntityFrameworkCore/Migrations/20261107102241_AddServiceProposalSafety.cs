using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceProposalSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "client_request_id",
                schema: "washgo",
                table: "service_change_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "proposed_name",
                schema: "washgo",
                table: "service_change_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_service_change_requests_merchant_id_client_request_id",
                schema: "washgo",
                table: "service_change_requests",
                columns: new[] { "merchant_id", "client_request_id" },
                unique: true,
                filter: "client_request_id IS NOT NULL");

            // Search column for existing proposals: copy the proposed name out of the JSON.
            migrationBuilder.Sql(@"UPDATE washgo.service_change_requests SET proposed_name = LEFT(COALESCE(proposed_data->>'name', ''), 200);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_service_change_requests_merchant_id_client_request_id",
                schema: "washgo",
                table: "service_change_requests");

            migrationBuilder.DropColumn(
                name: "client_request_id",
                schema: "washgo",
                table: "service_change_requests");

            migrationBuilder.DropColumn(
                name: "proposed_name",
                schema: "washgo",
                table: "service_change_requests");
        }
    }
}
