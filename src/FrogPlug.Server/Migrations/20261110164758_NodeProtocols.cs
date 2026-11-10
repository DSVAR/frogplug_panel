using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrogPlug.Server.Migrations
{
    /// <inheritdoc />
    public partial class NodeProtocols : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RealityEnabled",
                table: "Nodes",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "XhttpEnabled",
                table: "Nodes",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RealityEnabled",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "XhttpEnabled",
                table: "Nodes");
        }
    }
}
