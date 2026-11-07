using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddTermsPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "terms_policies",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terms_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "terms_acceptances",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terms_acceptances", x => x.id);
                    table.ForeignKey(
                        name: "FK_terms_acceptances_terms_policies_policy_id",
                        column: x => x.policy_id,
                        principalSchema: "washgo",
                        principalTable: "terms_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_terms_acceptances_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "washgo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_terms_acceptances_policy_id",
                schema: "washgo",
                table: "terms_acceptances",
                column: "policy_id");

            migrationBuilder.CreateIndex(
                name: "IX_terms_acceptances_user_id_policy_id",
                schema: "washgo",
                table: "terms_acceptances",
                columns: new[] { "user_id", "policy_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_terms_policies_effective_from",
                schema: "washgo",
                table: "terms_policies",
                column: "effective_from");

            migrationBuilder.CreateIndex(
                name: "IX_terms_policies_version",
                schema: "washgo",
                table: "terms_policies",
                column: "version",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "terms_acceptances",
                schema: "washgo");

            migrationBuilder.DropTable(
                name: "terms_policies",
                schema: "washgo");
        }
    }
}
