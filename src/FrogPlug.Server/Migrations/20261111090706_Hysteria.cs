using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrogPlug.Server.Migrations
{
    /// <inheritdoc />
    public partial class Hysteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HysteriaEnabled",
                table: "Nodes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "HysteriaPort",
                table: "Nodes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 443);

            migrationBuilder.AddColumn<bool>(
                name: "AllowHysteria",
                table: "ClientUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HysteriaEnabled",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "HysteriaPort",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "AllowHysteria",
                table: "ClientUsers");
        }
    }
}
