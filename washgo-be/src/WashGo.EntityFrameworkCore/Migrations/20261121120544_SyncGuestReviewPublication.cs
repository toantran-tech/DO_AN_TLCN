using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class SyncGuestReviewPublication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Publication is one scope now: ratings already published for Customers get the guest milestone with the same
            // time/actor. Merchant-only content approvals (no public_approved_at) stay unpublished.
            migrationBuilder.Sql(@"
UPDATE washgo.order_ratings
SET guest_approved_at = public_approved_at, guest_approved_by = public_approved_by
WHERE public_approved_at IS NOT NULL AND guest_approved_at IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
