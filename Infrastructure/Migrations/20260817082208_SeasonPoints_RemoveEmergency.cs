using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeasonPoints_RemoveEmergency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PointsEmergency",
                table: "PointRules");

            migrationBuilder.DropColumn(
                name: "EmergencyDateUtc",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "EmergencyPoints",
                table: "Matches");

            migrationBuilder.CreateTable(
                name: "SeasonPoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatchId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonPoints_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SeasonPoints_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonPoints_MatchId",
                table: "SeasonPoints",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonPoints_UserId_MatchId",
                table: "SeasonPoints",
                columns: new[] { "UserId", "MatchId" },
                unique: true,
                filter: "\"MatchId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeasonPoints");

            migrationBuilder.AddColumn<int>(
                name: "PointsEmergency",
                table: "PointRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmergencyDateUtc",
                table: "Matches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmergencyPoints",
                table: "Matches",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
