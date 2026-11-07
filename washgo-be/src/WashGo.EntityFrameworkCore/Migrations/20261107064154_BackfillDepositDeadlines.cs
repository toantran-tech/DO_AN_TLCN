using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class BackfillDepositDeadlines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Orders created before the deadline was stored: creation time + the deposit window in force now.
            migrationBuilder.Sql(@"
UPDATE washgo.orders
SET deposit_deadline = to_timestamp(created_on)
    + make_interval(mins => COALESCE((SELECT deposit_window_minutes FROM washgo.system_settings LIMIT 1), 30))
WHERE deposit_deadline IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
