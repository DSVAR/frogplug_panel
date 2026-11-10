using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrogPlug.Server.Migrations
{
    /// <inheritdoc />
    public partial class WhitelistEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WhitelistExitNodeId",
                table: "Nodes",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WhitelistExitNodeId",
                table: "Nodes");
        }
    }
}
