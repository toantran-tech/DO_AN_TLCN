using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenSessionKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "session_key",
                schema: "washgo",
                table: "refresh_tokens",
                type: "uuid",
                nullable: true);
            // Existing tokens start their own logical session.
            migrationBuilder.Sql("UPDATE washgo.refresh_tokens SET session_key = id WHERE session_key IS NULL;");
            migrationBuilder.CreateIndex(name: "IX_refresh_tokens_session_key", schema: "washgo", table: "refresh_tokens", column: "session_key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_refresh_tokens_session_key", schema: "washgo", table: "refresh_tokens");
            migrationBuilder.DropColumn(
                name: "session_key",
                schema: "washgo",
                table: "refresh_tokens");
        }
    }
}
