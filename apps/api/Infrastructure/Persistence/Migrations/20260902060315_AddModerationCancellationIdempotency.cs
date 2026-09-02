using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeriScan.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddModerationCancellationIdempotency : Migration
{
    private static readonly string[] OperationIndexColumns =
        ["ApplicationId", "TargetRequestId", "Operation", "IdempotencyKeyDigest"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "idempotent_operations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                Operation = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                IdempotencyKeyDigest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                OperationFingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                HttpStatusCode = table.Column<int>(type: "integer", nullable: false),
                ResponseSnapshot = table.Column<string>(type: "jsonb", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_idempotent_operations", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_idempotent_operations_ApplicationId_TargetRequestId_Operati~",
            table: "idempotent_operations",
            columns: OperationIndexColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_idempotent_operations_ExpiresAt",
            table: "idempotent_operations",
            column: "ExpiresAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "idempotent_operations");
    }
}
