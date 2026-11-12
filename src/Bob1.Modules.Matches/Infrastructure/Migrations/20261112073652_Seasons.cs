using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bob1.Modules.Matches.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Seasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Season = table.Column<string>(type: "TEXT", maxLength: 9, nullable: false),
                    DivisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    HomeTeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AwayTeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VenueId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_matches_AwayTeamId",
                table: "matches",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_matches_DivisionId",
                table: "matches",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_matches_HomeTeamId",
                table: "matches",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_matches_Season_StartsAtUtc",
                table: "matches",
                columns: new[] { "Season", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_matches_StartsAtUtc",
                table: "matches",
                column: "StartsAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "matches");
        }
    }
}
