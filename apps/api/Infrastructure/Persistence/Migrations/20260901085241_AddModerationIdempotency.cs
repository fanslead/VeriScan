using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeriScan.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddModerationIdempotency : Migration
{
    private static readonly string[] IdempotencyIndexColumns =
        ["ApplicationId", "IdempotencyKeyDigest"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_moderation_requests_ApplicationId_IdempotencyKeyDigest",
            table: "moderation_requests",
            columns: IdempotencyIndexColumns,
            unique: true,
            filter: "\"IdempotencyKeyDigest\" IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_moderation_requests_ApplicationId_IdempotencyKeyDigest",
            table: "moderation_requests");
    }
}
