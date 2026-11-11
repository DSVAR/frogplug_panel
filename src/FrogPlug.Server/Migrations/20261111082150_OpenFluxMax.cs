using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrogPlug.Server.Migrations
{
    /// <inheritdoc />
    public partial class OpenFluxMax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaxToken",
                table: "OpenFluxChannels",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MaxUid",
                table: "OpenFluxChannels",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxToken",
                table: "OpenFluxChannels");

            migrationBuilder.DropColumn(
                name: "MaxUid",
                table: "OpenFluxChannels");
        }
    }
}
