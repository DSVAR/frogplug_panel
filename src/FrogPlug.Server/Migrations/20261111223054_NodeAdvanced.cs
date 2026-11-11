using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrogPlug.Server.Migrations
{
    /// <inheritdoc />
    public partial class NodeAdvanced : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HysteriaObfsEnabled",
                table: "Nodes",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "RealityFingerprint",
                table: "Nodes",
                type: "TEXT",
                nullable: false,
                defaultValue: "chrome");

            migrationBuilder.AddColumn<string>(
                name: "XhttpMode",
                table: "Nodes",
                type: "TEXT",
                nullable: false,
                defaultValue: "auto");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HysteriaObfsEnabled",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "RealityFingerprint",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "XhttpMode",
                table: "Nodes");
        }
    }
}
