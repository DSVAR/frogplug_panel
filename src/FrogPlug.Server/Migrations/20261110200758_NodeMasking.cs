using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrogPlug.Server.Migrations
{
    /// <inheritdoc />
    public partial class NodeMasking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "MtprotoFrontingPort",
                table: "Nodes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 8443,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 443);

            migrationBuilder.AddColumn<string>(
                name: "MaskMode",
                table: "Nodes",
                type: "TEXT",
                nullable: false,
                defaultValue: "ForeignSite");

            migrationBuilder.AddColumn<string>(
                name: "MtprotoHost",
                table: "Nodes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RealitySni",
                table: "Nodes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StubTemplate",
                table: "Nodes",
                type: "TEXT",
                nullable: false,
                defaultValue: "personal-blog");

            migrationBuilder.AddColumn<string>(
                name: "StubTitle",
                table: "Nodes",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaskMode",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "MtprotoHost",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "RealitySni",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "StubTemplate",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "StubTitle",
                table: "Nodes");

            migrationBuilder.AlterColumn<int>(
                name: "MtprotoFrontingPort",
                table: "Nodes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 443,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 8443);
        }
    }
}
