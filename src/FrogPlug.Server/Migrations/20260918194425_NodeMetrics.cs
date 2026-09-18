using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrogPlug.Server.Migrations
{
    /// <inheritdoc />
    public partial class NodeMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CpuPercent",
                table: "Nodes",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "MemoryUsedPercent",
                table: "Nodes",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                table: "ClientUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NodeTrafficDaily",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NodeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    DownBytes = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NodeTrafficDaily", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NodeTrafficDaily_NodeId_Date",
                table: "NodeTrafficDaily",
                columns: new[] { "NodeId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NodeTrafficDaily");

            migrationBuilder.DropColumn(
                name: "CpuPercent",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "MemoryUsedPercent",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "ClientUsers");
        }
    }
}
