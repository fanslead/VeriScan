#pragma warning disable IDE0161
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeriScan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeyDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "application_api_keys",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "application_api_keys");
        }
    }
}
