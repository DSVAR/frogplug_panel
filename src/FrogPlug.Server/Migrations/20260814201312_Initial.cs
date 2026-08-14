using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrogPlug.Server.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Admins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Login = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FailedAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TotpSecret = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Admins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    At = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AdminLogin = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TrafficLimitBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    UsedUploadBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    UsedDownloadBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    VlessUuid = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubscriptionToken = table.Column<string>(type: "TEXT", nullable: false),
                    AllowWhitelist = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowMtproto = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSubscriptionAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Nodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: false),
                    Country = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    Domain = table.Column<string>(type: "TEXT", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    RealityPort = table.Column<int>(type: "INTEGER", nullable: false),
                    RealityPrivateKey = table.Column<string>(type: "TEXT", nullable: false),
                    RealityPublicKey = table.Column<string>(type: "TEXT", nullable: false),
                    RealityShortId = table.Column<string>(type: "TEXT", nullable: false),
                    RealityServerName = table.Column<string>(type: "TEXT", nullable: false),
                    RealityDest = table.Column<string>(type: "TEXT", nullable: false),
                    XhttpPath = table.Column<string>(type: "TEXT", nullable: false),
                    XhttpPort = table.Column<int>(type: "INTEGER", nullable: false),
                    CascadeUuid = table.Column<Guid>(type: "TEXT", nullable: false),
                    AgentUrl = table.Column<string>(type: "TEXT", nullable: false),
                    AgentToken = table.Column<string>(type: "TEXT", nullable: false),
                    AgentCertSha256 = table.Column<string>(type: "TEXT", nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    XrayVersion = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoutingRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchType = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    Target = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutingRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Admins_Login",
                table: "Admins",
                column: "Login",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientUsers_Name",
                table: "ClientUsers",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientUsers_SubscriptionToken",
                table: "ClientUsers",
                column: "SubscriptionToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientUsers_VlessUuid",
                table: "ClientUsers",
                column: "VlessUuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_Name",
                table: "Nodes",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Admins");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ClientUsers");

            migrationBuilder.DropTable(
                name: "Nodes");

            migrationBuilder.DropTable(
                name: "RoutingRules");

            migrationBuilder.DropTable(
                name: "Settings");
        }
    }
}
