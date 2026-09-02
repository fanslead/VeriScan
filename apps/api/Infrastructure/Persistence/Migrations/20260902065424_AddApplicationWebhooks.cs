using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeriScan.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddApplicationWebhooks : Migration
{
    private static readonly string[] PublicationClaimIndexColumns =
        ["Status", "AvailableAt", "CreatedAt"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "application_webhooks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                EndpointUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                ProviderApplicationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ProviderEndpointId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Revision = table.Column<int>(type: "integer", nullable: false),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                LastTestId = table.Column<Guid>(type: "uuid", nullable: true),
                LastTestRevision = table.Column<int>(type: "integer", nullable: true),
                LastTestOutcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                LastTestHttpStatusCode = table.Column<int>(type: "integer", nullable: true),
                LastTestLatencyMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                LastTestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_application_webhooks", x => x.Id);
                table.ForeignKey(
                    name: "FK_application_webhooks_applications_ApplicationId",
                    column: x => x.ApplicationId,
                    principalTable: "applications",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "webhook_publications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                ApplicationWebhookId = table.Column<Guid>(type: "uuid", nullable: false),
                ConfigurationRevision = table.Column<int>(type: "integer", nullable: false),
                ProviderApplicationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ProviderEndpointId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                EventType = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                DeduplicationKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                AttemptCount = table.Column<int>(type: "integer", nullable: false),
                TestPollCount = table.Column<int>(type: "integer", nullable: false),
                LeaseOwner = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                AvailableAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ProviderMessageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                LastErrorCode = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                ResponseStatusCode = table.Column<int>(type: "integer", nullable: true),
                ResponseLatencyMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_webhook_publications", x => x.Id);
                table.ForeignKey(
                    name: "FK_webhook_publications_application_webhooks_ApplicationWebhoo~",
                    column: x => x.ApplicationWebhookId,
                    principalTable: "application_webhooks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_application_webhooks_ApplicationId",
            table: "application_webhooks",
            column: "ApplicationId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_webhook_publications_ApplicationWebhookId",
            table: "webhook_publications",
            column: "ApplicationWebhookId");

        migrationBuilder.CreateIndex(
            name: "IX_webhook_publications_DeduplicationKey",
            table: "webhook_publications",
            column: "DeduplicationKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_webhook_publications_Status_AvailableAt_CreatedAt",
            table: "webhook_publications",
            columns: PublicationClaimIndexColumns);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "webhook_publications");

        migrationBuilder.DropTable(
            name: "application_webhooks");
    }
}
