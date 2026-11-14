using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WashGo.EntityFrameworkCore.Migrations;

public partial class AddGuestReviewApproval : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(name: "guest_approved_at", schema: "washgo", table: "order_ratings",
            type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "guest_approved_by", schema: "washgo", table: "order_ratings",
            type: "uuid", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "guest_approved_at", schema: "washgo", table: "order_ratings");
        migrationBuilder.DropColumn(name: "guest_approved_by", schema: "washgo", table: "order_ratings");
    }
}
