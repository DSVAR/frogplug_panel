using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrogPlug.Server.Migrations
{
    /// <inheritdoc />
    public partial class OpenFlux : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OpenFluxChannels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NodeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Mode = table.Column<string>(type: "TEXT", nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    VyandexUrl = table.Column<string>(type: "TEXT", nullable: true),
                    YandexUrl = table.Column<string>(type: "TEXT", nullable: true),
                    MailruUrl = table.Column<string>(type: "TEXT", nullable: true),
                    TelemostUrl = table.Column<string>(type: "TEXT", nullable: true),
                    BoardsUrl = table.Column<string>(type: "TEXT", nullable: true),
                    AvitoUrl = table.Column<string>(type: "TEXT", nullable: true),
                    CupsUrl = table.Column<string>(type: "TEXT", nullable: true),
                    CookiesJson = table.Column<string>(type: "TEXT", nullable: true),
                    UseXrayUpstream = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastApplyError = table.Column<string>(type: "TEXT", nullable: true),
                    LastAppliedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenFluxChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenFluxChannels_Nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OpenFluxChannels_NodeId_Name",
                table: "OpenFluxChannels",
                columns: new[] { "NodeId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OpenFluxChannels");
        }
    }
}
