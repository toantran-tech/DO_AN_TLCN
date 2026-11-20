using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AdminAccountManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Emails become unique per lower(email) among accounts that are not soft-deleted. Stop (and change nothing)
            // if existing accounts differ only by case/spaces: an operator must resolve them; data is never auto-merged.
            migrationBuilder.Sql(@"
DO $$
DECLARE duplicates integer;
BEGIN
    SELECT count(*) INTO duplicates FROM (
        SELECT lower(btrim(email)) FROM washgo.users GROUP BY 1 HAVING count(*) > 1) d;
    IF duplicates > 0 THEN
        RAISE EXCEPTION 'AdminAccountManagement: % email(s) differ only by case or spaces; resolve them before migrating.', duplicates;
    END IF;
END $$;
UPDATE washgo.users SET email = lower(btrim(email)) WHERE email <> lower(btrim(email));");

            migrationBuilder.DropIndex(
                name: "IX_users_auth_provider_provider_id",
                schema: "washgo",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_email",
                schema: "washgo",
                table: "users");

            migrationBuilder.AddColumn<int>(
                name: "credential_delivery",
                schema: "washgo",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "credential_sent_at",
                schema: "washgo",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delete_reason",
                schema: "washgo",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                schema: "washgo",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by",
                schema: "washgo",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                schema: "washgo",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_protected",
                schema: "washgo",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "revision",
                schema: "washgo",
                table: "users",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "status_changed_at",
                schema: "washgo",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "status_changed_by",
                schema: "washgo",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status_reason",
                schema: "washgo",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reauthenticated_at",
                schema: "washgo",
                table: "refresh_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_permissions",
                schema: "washgo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_permissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_permissions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "washgo",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_created_active",
                schema: "washgo",
                table: "users",
                columns: new[] { "created_at", "id" },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_users_deleted_at",
                schema: "washgo",
                table: "users",
                column: "deleted_at",
                filter: "is_deleted = true");

            migrationBuilder.AddCheckConstraint(
                name: "chk_users_deleted",
                schema: "washgo",
                table: "users",
                sql: "(is_deleted AND deleted_at IS NOT NULL AND NOT is_active) OR (NOT is_deleted AND deleted_at IS NULL AND deleted_by IS NULL AND delete_reason IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_user_permissions_permission",
                schema: "washgo",
                table: "user_permissions",
                column: "permission");

            migrationBuilder.CreateIndex(
                name: "IX_user_permissions_user_id_permission",
                schema: "washgo",
                table: "user_permissions",
                columns: new[] { "user_id", "permission" },
                unique: true);

            migrationBuilder.Sql(@"
-- Shippers deleted before accounts had their own soft delete: their login account becomes a deleted account too
-- (still inactive, never reactivated; history keeps the same user id).
UPDATE washgo.users u
SET is_deleted = true, is_active = false, deleted_at = COALESCE(s.deleted_at, now()), deleted_by = s.deleted_by,
    delete_reason = 'Shipper đã bị xóa trước khi có xóa mềm tài khoản.'
FROM washgo.shippers s
WHERE s.user_id = u.id AND s.is_deleted AND NOT u.is_deleted;

-- Identity is unique only among accounts that are not soft-deleted (a deleted account frees its email/Google identity).
CREATE UNIQUE INDEX ux_users_email_active ON washgo.users (lower(email)) WHERE NOT is_deleted;
CREATE UNIQUE INDEX ux_users_provider_active ON washgo.users (auth_provider, provider_id)
    WHERE NOT is_deleted AND provider_id IS NOT NULL;

-- Revision moves on administrative changes only (not on last_login_at/updated_at/password/credential delivery),
-- so a user signing in does not invalidate an Admin's expectedRevision.
CREATE OR REPLACE FUNCTION washgo.bump_user_revision() RETURNS trigger AS $$
BEGIN
    IF ROW(NEW.role_id, NEW.email, NEW.auth_provider, NEW.provider_id, NEW.full_name, NEW.phone, NEW.avatar, NEW.is_active,
           NEW.is_deleted, NEW.deleted_at, NEW.deleted_by, NEW.delete_reason, NEW.is_protected, NEW.status_changed_at, NEW.status_reason)
       IS DISTINCT FROM
       ROW(OLD.role_id, OLD.email, OLD.auth_provider, OLD.provider_id, OLD.full_name, OLD.phone, OLD.avatar, OLD.is_active,
           OLD.is_deleted, OLD.deleted_at, OLD.deleted_by, OLD.delete_reason, OLD.is_protected, OLD.status_changed_at, OLD.status_reason) THEN
        NEW.revision := OLD.revision + 1;
    ELSE
        NEW.revision := OLD.revision;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;
CREATE TRIGGER trg_users_revision BEFORE UPDATE ON washgo.users
    FOR EACH ROW EXECUTE FUNCTION washgo.bump_user_revision();

-- Existing deployments: the oldest active Admin can manage Admin accounts (others are granted from the account page).
INSERT INTO washgo.user_permissions (id, user_id, permission, granted_at, granted_by)
SELECT gen_random_uuid(), a.id, p.permission, now(), NULL
FROM (SELECT u.id FROM washgo.users u JOIN washgo.roles r ON r.id = u.role_id
      WHERE r.name = 'Admin' AND u.is_active AND NOT u.is_deleted
      ORDER BY u.created_at, u.id LIMIT 1) a
CROSS JOIN (VALUES ('Accounts.ManageAdmins'), ('Accounts.ReadDeleted'), ('Accounts.Restore')) AS p(permission);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_users_revision ON washgo.users;
DROP FUNCTION IF EXISTS washgo.bump_user_revision();
DROP INDEX IF EXISTS washgo.ux_users_email_active;
DROP INDEX IF EXISTS washgo.ux_users_provider_active;");

            migrationBuilder.DropTable(
                name: "user_permissions",
                schema: "washgo");

            migrationBuilder.DropIndex(
                name: "ix_users_created_active",
                schema: "washgo",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_deleted_at",
                schema: "washgo",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "chk_users_deleted",
                schema: "washgo",
                table: "users");

            migrationBuilder.DropColumn(
                name: "credential_delivery",
                schema: "washgo",
                table: "users");

            migrationBuilder.DropColumn(
                name: "credential_sent_at",
                schema: "washgo",
                table: "users");

            migrationBuilder.DropColumn(
                name: "delete_reason",
                schema: "washgo",
                table: "users");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "washgo",
                table: "users");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                schema: "washgo",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                schema: "washgo",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_protected",
                schema: "washgo",
                table: "users");

            migrationBuilder.DropColumn(
                name: "revision",
                schema: "washgo",
                table: "users");

            migrationBuilder.DropColumn(
                name: "status_changed_at",
                schema: "washgo",
                table: "users");

            migrationBuilder.DropColumn(
                name: "status_changed_by",
                schema: "washgo",
                table: "users");

            migrationBuilder.DropColumn(
                name: "status_reason",
                schema: "washgo",
                table: "users");

            migrationBuilder.DropColumn(
                name: "reauthenticated_at",
                schema: "washgo",
                table: "refresh_tokens");

            migrationBuilder.CreateIndex(
                name: "IX_users_auth_provider_provider_id",
                schema: "washgo",
                table: "users",
                columns: new[] { "auth_provider", "provider_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                schema: "washgo",
                table: "users",
                column: "email",
                unique: true);
        }
    }
}
