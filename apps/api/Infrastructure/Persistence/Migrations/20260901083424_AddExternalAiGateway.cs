using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeriScan.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddExternalAiGateway : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AiConfigurationRevision",
            table: "moderation_items",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AiFailureCode",
            table: "moderation_items",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "AiInputTokens",
            table: "moderation_items",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "AiOutputTokens",
            table: "moderation_items",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EvidenceText",
            table: "moderation_items",
            type: "jsonb",
            nullable: false,
            defaultValueSql: "'[]'::jsonb");

        migrationBuilder.AddColumn<string>(
            name: "ProviderRequestId",
            table: "moderation_items",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ai_model_configurations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PublicRevisionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Protocol = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                BaseUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                EndpointPath = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                CredentialRef = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                AuthScheme = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ApiVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ApiVersionLocation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                SystemPrompt = table.Column<string>(type: "text", nullable: false),
                DecodingMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                MaxInputTokens = table.Column<int>(type: "integer", nullable: false),
                MaxOutputTokens = table.Column<int>(type: "integer", nullable: false),
                ConnectTimeoutMs = table.Column<int>(type: "integer", nullable: false),
                RequestTimeoutMs = table.Column<int>(type: "integer", nullable: false),
                MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                DataRegion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                RetentionClass = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastTestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastTestSucceeded = table.Column<bool>(type: "boolean", nullable: true),
                LastTestFailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                AdapterContractVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                CanonicalSchemaVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                CanonicalSchemaHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                EffectiveSchemaHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                SchemaTransformerVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ai_model_configurations", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ai_model_configurations_IsActive",
            table: "ai_model_configurations",
            column: "IsActive",
            unique: true,
            filter: "\"IsActive\" = TRUE");

        migrationBuilder.CreateIndex(
            name: "IX_ai_model_configurations_Name",
            table: "ai_model_configurations",
            column: "Name");

        migrationBuilder.CreateIndex(
            name: "IX_ai_model_configurations_PublicRevisionId",
            table: "ai_model_configurations",
            column: "PublicRevisionId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ai_model_configurations");

        migrationBuilder.DropColumn(
            name: "AiConfigurationRevision",
            table: "moderation_items");

        migrationBuilder.DropColumn(
            name: "AiFailureCode",
            table: "moderation_items");

        migrationBuilder.DropColumn(
            name: "AiInputTokens",
            table: "moderation_items");

        migrationBuilder.DropColumn(
            name: "AiOutputTokens",
            table: "moderation_items");

        migrationBuilder.DropColumn(
            name: "EvidenceText",
            table: "moderation_items");

        migrationBuilder.DropColumn(
            name: "ProviderRequestId",
            table: "moderation_items");
    }
}
