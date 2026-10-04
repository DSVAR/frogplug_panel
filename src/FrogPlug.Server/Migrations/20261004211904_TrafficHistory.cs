using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrogPlug.Server.Migrations
{
    /// <inheritdoc />
    public partial class TrafficHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveFailures",
                table: "Nodes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastNotifiedApplyError",
                table: "Nodes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OfflineNotified",
                table: "Nodes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ExpiredNotified",
                table: "ClientUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OverLimitNotified",
                table: "ClientUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "TrafficHourly",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    DownBytes = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrafficHourly", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserTrafficDaily",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    DownBytes = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTrafficDaily", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrafficHourly_Date",
                table: "TrafficHourly",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTrafficDaily_UserId_Date",
                table: "UserTrafficDaily",
                columns: new[] { "UserId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrafficHourly");

            migrationBuilder.DropTable(
                name: "UserTrafficDaily");

            migrationBuilder.DropColumn(
                name: "ConsecutiveFailures",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "LastNotifiedApplyError",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "OfflineNotified",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "ExpiredNotified",
                table: "ClientUsers");

            migrationBuilder.DropColumn(
                name: "OverLimitNotified",
                table: "ClientUsers");
        }
    }
}
