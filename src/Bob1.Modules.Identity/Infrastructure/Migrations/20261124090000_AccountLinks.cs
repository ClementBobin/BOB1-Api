using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bob1.Modules.Identity.Infrastructure.Migrations
{
    /// <summary>One-time links an admin hands to a person to reset a password or change an email (only the token hash is stored).</summary>
    public partial class AccountLinks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_links", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_links_TokenHash", table: "account_links", column: "TokenHash", unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_account_links_UserId_Purpose", table: "account_links", columns: new[] { "UserId", "Purpose" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "account_links");
        }
    }
}
