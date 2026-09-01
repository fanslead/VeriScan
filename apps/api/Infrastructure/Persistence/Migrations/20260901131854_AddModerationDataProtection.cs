using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable IDE0161

namespace VeriScan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationDataProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthorType",
                table: "moderation_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentHashKeyVersion",
                table: "moderation_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "legacy-sha256");

            migrationBuilder.AddColumn<string>(
                name: "Scene",
                table: "moderation_items",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorType",
                table: "moderation_items");

            migrationBuilder.DropColumn(
                name: "ContentHashKeyVersion",
                table: "moderation_items");

            migrationBuilder.DropColumn(
                name: "Scene",
                table: "moderation_items");
        }
    }
}
