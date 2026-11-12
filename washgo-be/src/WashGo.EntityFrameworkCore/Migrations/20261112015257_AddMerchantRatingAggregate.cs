using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantRatingAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "public_rating_count",
                schema: "washgo",
                table: "merchants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "public_rating_sum",
                schema: "washgo",
                table: "merchants",
                type: "integer",
                nullable: false,
                defaultValue: 0);
            // Running totals start from the ratings that are public today; the score is recomputed from them once.
            migrationBuilder.Sql(@"
UPDATE washgo.merchants m SET public_rating_sum = COALESCE(r.s, 0), public_rating_count = COALESCE(r.c, 0),
       rating = CASE WHEN COALESCE(r.c, 0) = 0 THEN 5.0 ELSE ROUND(r.s::numeric / r.c, 1) END
FROM (SELECT mm.user_id, SUM(o.overall_stars) s, COUNT(o.id) c
      FROM washgo.merchants mm LEFT JOIN washgo.order_ratings o ON o.merchant_id = mm.user_id AND o.is_hidden = false AND o.is_deleted = false
      GROUP BY mm.user_id) r
WHERE r.user_id = m.user_id;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "public_rating_count",
                schema: "washgo",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "public_rating_sum",
                schema: "washgo",
                table: "merchants");
        }
    }
}
