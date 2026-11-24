using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bob1.Modules.Identity.Infrastructure.Migrations
{
    /// <summary>Devices enrolled for biometric sign-in (only the SHA-256 hash of each device token is stored).</summary>
    public partial class BiometricCredentials : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "biometric_credentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastUsedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_biometric_credentials", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_biometric_credentials_TokenHash", table: "biometric_credentials", column: "TokenHash", unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_biometric_credentials_UserId", table: "biometric_credentials", column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "biometric_credentials");
        }
    }
}
