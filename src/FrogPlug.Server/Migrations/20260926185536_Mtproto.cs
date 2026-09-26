using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrogPlug.Server.Migrations
{
    /// <inheritdoc />
    public partial class Mtproto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MtprotoEnabled",
                table: "Nodes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MtprotoFrontingPort",
                table: "Nodes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 443);

            migrationBuilder.AddColumn<int>(
                name: "MtprotoPort",
                table: "Nodes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2443);

            migrationBuilder.AddColumn<string>(
                name: "MtprotoSecret",
                table: "Nodes",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MtprotoEnabled",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "MtprotoFrontingPort",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "MtprotoPort",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "MtprotoSecret",
                table: "Nodes");
        }
    }
}
