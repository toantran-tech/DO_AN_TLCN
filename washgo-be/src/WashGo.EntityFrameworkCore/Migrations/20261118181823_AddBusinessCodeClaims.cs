using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessCodeClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "business_code_claims",
                schema: "washgo",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    prefix = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_code_claims", x => x.code);
                });

            migrationBuilder.CreateIndex(
                name: "IX_business_code_claims_prefix",
                schema: "washgo",
                table: "business_code_claims",
                column: "prefix");

            migrationBuilder.CreateIndex(
                name: "IX_business_code_claims_resource_id",
                schema: "washgo",
                table: "business_code_claims",
                column: "resource_id",
                unique: true);

            // Existing codes that already look like the new format stay reserved, so a new record never repeats them.
            // Other legacy/seed codes keep their value and are simply outside the new code space.
            migrationBuilder.Sql("""
                INSERT INTO washgo.business_code_claims(code, prefix, resource_id, issued_at)
                SELECT code, 'SV', id, now() FROM washgo.services WHERE code ~ '^SV[0-9]{4}$'
                UNION ALL SELECT code, 'DM', id, now() FROM washgo.service_categories WHERE code ~ '^DM[0-9]{4}$'
                UNION ALL SELECT code, 'LK', id, now() FROM washgo.lockers WHERE code ~ '^LK[0-9]{4}$'
                UNION ALL SELECT code, 'KV', id, now() FROM washgo.service_areas WHERE code ~ '^KV[0-9]{4}$'
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_code_claims",
                schema: "washgo");
        }
    }
}
