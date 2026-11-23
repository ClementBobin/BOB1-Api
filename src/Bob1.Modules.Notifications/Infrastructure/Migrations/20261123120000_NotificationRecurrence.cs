using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bob1.Modules.Notifications.Infrastructure.Migrations
{
    /// <summary>
    /// Recurring notifications: how often (RecurrenceHours) and when it last went out (LastSentAtUtc).
    /// Existing rows keep Recurring as-is but have no interval, so the recurrence job ignores them until an admin edits them.
    /// </summary>
    public partial class NotificationRecurrence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecurrenceHours", table: "notifications", type: "integer", nullable: true);
            migrationBuilder.AddColumn<DateTime>(
                name: "LastSentAtUtc", table: "notifications", type: "timestamp without time zone", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "LastSentAtUtc", table: "notifications");
            migrationBuilder.DropColumn(name: "RecurrenceHours", table: "notifications");
        }
    }
}
