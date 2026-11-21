using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalCategoryFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "proposed_category_id",
                schema: "washgo",
                table: "service_change_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_change_requests_proposed_category_id",
                schema: "washgo",
                table: "service_change_requests",
                column: "proposed_category_id");

            // Backfill from the stored proposal content with the same rule as the DTO: the proposed categoryId, else the
            // seed category of a legacy serviceType 1-5; anything else stays NULL (no category to filter on).
            migrationBuilder.Sql(@"
UPDATE washgo.service_change_requests SET proposed_category_id = CASE
    WHEN jsonb_typeof(proposed_data->'categoryId') = 'string'
         AND (proposed_data->>'categoryId') ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
        THEN (proposed_data->>'categoryId')::uuid
    WHEN jsonb_typeof(proposed_data->'serviceType') = 'number' AND (proposed_data->>'serviceType') IN ('1', '2', '3', '4', '5')
        THEN ('ca000000-0000-0000-0000-' || lpad(proposed_data->>'serviceType', 12, '0'))::uuid
END
WHERE proposed_category_id IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_service_change_requests_proposed_category_id",
                schema: "washgo",
                table: "service_change_requests");

            migrationBuilder.DropColumn(
                name: "proposed_category_id",
                schema: "washgo",
                table: "service_change_requests");
        }
    }
}
