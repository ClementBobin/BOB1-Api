using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bob1.Modules.Notifications.Infrastructure.Migrations
{
    /// <summary>
    /// Splits the old per-user "notifications" table in two:
    ///   notifications       = one row per notification sent (content, options, dedupe key, admin visibility)
    ///   user_notifications  = one thin row per recipient (delivery + read state)
    /// Existing rows are grouped back into one notification per send, so no history is lost.
    /// </summary>
    public partial class NotificationContent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. The old table becomes user_notifications (rename its PK and index too: names are unique per schema).
            migrationBuilder.Sql(@"ALTER TABLE notifications RENAME TO user_notifications;");
            migrationBuilder.Sql(@"ALTER TABLE user_notifications RENAME CONSTRAINT ""PK_notifications"" TO ""PK_user_notifications"";");
            migrationBuilder.Sql(@"ALTER INDEX ""IX_notifications_UserId_CreatedAtUtc"" RENAME TO ""IX_user_notifications_UserId_CreatedAtUtc"";");

            // 2. New content table.
            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DataJson = table.Column<string>(type: "text", nullable: true),
                    DedupeKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Recurring = table.Column<bool>(type: "boolean", nullable: false),
                    Startup = table.Column<bool>(type: "boolean", nullable: false),
                    VisibleInAdmin = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "NotificationId", table: "user_notifications", type: "uuid", nullable: true);

            // 3. Backfill: one notification per (dedupe key) or, without one, per identical send (same content, same second).
            migrationBuilder.Sql(@"
CREATE TEMP TABLE _un AS
SELECT u.*,
       CASE WHEN ""DataJson"" IS NULL THEN NULL ELSE ""DataJson""::jsonb ->> 'dedupe' END AS dedupe,
       CASE WHEN ""DataJson"" IS NULL THEN 'broadcast' ELSE COALESCE(""DataJson""::jsonb ->> 'type', 'broadcast') END AS ntype
FROM user_notifications u;

ALTER TABLE _un ADD COLUMN gk text;
UPDATE _un SET gk = COALESCE('d:' || dedupe,
    'c:' || md5(concat_ws('|', ""Title"", ""Body"", COALESCE(""DataJson"", ''), date_trunc('second', ""CreatedAtUtc"")::text,
                          COALESCE(""ExpiresAtUtc""::text, ''), ""Recurring""::text, ""Startup""::text)));

CREATE TEMP TABLE _ng AS
SELECT DISTINCT ON (gk) gk, gen_random_uuid() AS gid, ntype, ""Title"", ""Body"", ""DataJson"", dedupe,
       ""CreatedAtUtc"", ""ExpiresAtUtc"", ""Recurring"", ""Startup""
FROM _un ORDER BY gk, ""CreatedAtUtc"";

-- Legacy rows cannot tell an audit copy from the real notice: audiences (broadcast, match notices) are shown,
-- reminders (dedupe key), personal notices and '[Copie]' copies are hidden.
INSERT INTO notifications (""Id"", ""Type"", ""Title"", ""Body"", ""DataJson"", ""DedupeKey"", ""CreatedByUserId"", ""CreatedAtUtc"",
                           ""ExpiresAtUtc"", ""Recurring"", ""Startup"", ""VisibleInAdmin"")
SELECT gid, ntype, ""Title"", ""Body"", ""DataJson"", dedupe, NULL, ""CreatedAtUtc"", ""ExpiresAtUtc"", ""Recurring"", ""Startup"",
       (dedupe IS NULL AND ""Title"" NOT LIKE '[Copie]%' AND (ntype = 'broadcast' OR ntype LIKE 'match\_%'))
FROM _ng;

UPDATE user_notifications u SET ""NotificationId"" = g.gid
FROM _un x JOIN _ng g ON g.gk = x.gk
WHERE u.""Id"" = x.""Id"";

DROP TABLE _un; DROP TABLE _ng;

-- A user cannot hold the same notification twice (a double-click used to create two identical rows).
DELETE FROM user_notifications a USING user_notifications b
WHERE a.""NotificationId"" = b.""NotificationId"" AND a.""UserId"" = b.""UserId"" AND a.""Id"" > b.""Id"";
");

            // 4. The content now lives only on notifications.
            migrationBuilder.AlterColumn<Guid>(
                name: "NotificationId", table: "user_notifications", type: "uuid", nullable: false,
                oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
            migrationBuilder.DropColumn(name: "Title", table: "user_notifications");
            migrationBuilder.DropColumn(name: "Body", table: "user_notifications");
            migrationBuilder.DropColumn(name: "DataJson", table: "user_notifications");
            migrationBuilder.DropColumn(name: "ExpiresAtUtc", table: "user_notifications");
            migrationBuilder.DropColumn(name: "Recurring", table: "user_notifications");
            migrationBuilder.DropColumn(name: "Startup", table: "user_notifications");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_DedupeKey", table: "notifications", column: "DedupeKey",
                unique: true, filter: "\"DedupeKey\" IS NOT NULL");
            migrationBuilder.CreateIndex(
                name: "IX_notifications_VisibleInAdmin_CreatedAtUtc", table: "notifications",
                columns: new[] { "VisibleInAdmin", "CreatedAtUtc" });
            migrationBuilder.CreateIndex(
                name: "IX_user_notifications_NotificationId_UserId", table: "user_notifications",
                columns: new[] { "NotificationId", "UserId" }, unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_user_notifications_notifications_NotificationId", table: "user_notifications",
                column: "NotificationId", principalTable: "notifications", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_user_notifications_notifications_NotificationId", table: "user_notifications");
            migrationBuilder.DropIndex(name: "IX_user_notifications_NotificationId_UserId", table: "user_notifications");

            migrationBuilder.AddColumn<string>(name: "Title", table: "user_notifications", type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "Body", table: "user_notifications", type: "character varying(1000)", maxLength: 1000, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "DataJson", table: "user_notifications", type: "text", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "ExpiresAtUtc", table: "user_notifications", type: "timestamp without time zone", nullable: true);
            migrationBuilder.AddColumn<bool>(name: "Recurring", table: "user_notifications", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(name: "Startup", table: "user_notifications", type: "boolean", nullable: false, defaultValue: false);

            migrationBuilder.Sql(@"
UPDATE user_notifications u
SET ""Title"" = n.""Title"", ""Body"" = n.""Body"", ""DataJson"" = n.""DataJson"", ""ExpiresAtUtc"" = n.""ExpiresAtUtc"",
    ""Recurring"" = n.""Recurring"", ""Startup"" = n.""Startup""
FROM notifications n WHERE n.""Id"" = u.""NotificationId"";");

            migrationBuilder.DropColumn(name: "NotificationId", table: "user_notifications");
            migrationBuilder.DropTable(name: "notifications");

            migrationBuilder.Sql(@"ALTER TABLE user_notifications RENAME TO notifications;");
            migrationBuilder.Sql(@"ALTER TABLE notifications RENAME CONSTRAINT ""PK_user_notifications"" TO ""PK_notifications"";");
            migrationBuilder.Sql(@"ALTER INDEX ""IX_user_notifications_UserId_CreatedAtUtc"" RENAME TO ""IX_notifications_UserId_CreatedAtUtc"";");
        }
    }
}
