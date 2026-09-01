using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable IDE0161, CA1861

namespace VeriScan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Action",
                table: "word_rules",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "RiskSignal");

            migrationBuilder.AddColumn<string>(
                name: "EvidenceTemplate",
                table: "word_rules",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "word_rules",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MatchMode",
                table: "word_rules",
                type: "character varying(48)",
                maxLength: 48,
                nullable: false,
                defaultValue: "NormalizedContains");

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "word_rules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Scene",
                table: "word_rules",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "word_rules",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizationProfile",
                table: "rule_set_versions",
                type: "character varying(48)",
                maxLength: 48,
                nullable: false,
                defaultValue: "Default");

            migrationBuilder.AddColumn<int>(
                name: "Ordinal",
                table: "moderation_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE word_rules
                SET "Action" = CASE "Type"
                    WHEN 'Black' THEN 'HardReject'
                    WHEN 'White' THEN 'ContextException'
                    ELSE 'RiskSignal'
                END
                WHERE "Action" = 'RiskSignal';
                """);

            migrationBuilder.CreateTable(
                name: "ai_invocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModerationRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModerationItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ConfigurationRevision = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ProviderRequestId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    LatencyMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_invocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "api_request_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApiKeyId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModerationRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    RouteTemplate = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AuthenticationOutcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IdempotencyOutcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HttpStatusCode = table.Column<int>(type: "integer", nullable: false),
                    ItemCount = table.Column<int>(type: "integer", nullable: true),
                    LatencyMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_request_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApiKeyId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Action = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BeforeJson = table.Column<string>(type: "jsonb", nullable: true),
                    AfterJson = table.Column<string>(type: "jsonb", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "combination_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleSetVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TermsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    WindowSize = table.Column<int>(type: "integer", nullable: false),
                    Language = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Scene = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EvidenceTemplate = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_combination_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_combination_rules_rule_set_versions_RuleSetVersionId",
                        column: x => x.RuleSetVersionId,
                        principalTable: "rule_set_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "moderation_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaximumAttempts = table.Column<int>(type: "integer", nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeaseOwner = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moderation_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_moderation_jobs_moderation_requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "moderation_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "outbox_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    AggregateType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastErrorCode = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "regex_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleSetVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Pattern = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    TimeoutMs = table.Column<int>(type: "integer", nullable: false),
                    MaxInputLength = table.Column<int>(type: "integer", nullable: false),
                    EngineMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Language = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Scene = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EvidenceTemplate = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regex_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_regex_rules_rule_set_versions_RuleSetVersionId",
                        column: x => x.RuleSetVersionId,
                        principalTable: "rule_set_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usage_consumed_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerName = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    OutboxEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usage_consumed_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "usage_daily",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BucketStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestCount = table.Column<long>(type: "bigint", nullable: false),
                    IdempotencyReplayCount = table.Column<long>(type: "bigint", nullable: false),
                    ItemCount = table.Column<long>(type: "bigint", nullable: false),
                    PassCount = table.Column<long>(type: "bigint", nullable: false),
                    RejectCount = table.Column<long>(type: "bigint", nullable: false),
                    ReviewCount = table.Column<long>(type: "bigint", nullable: false),
                    AiCallCount = table.Column<long>(type: "bigint", nullable: false),
                    AiFailureCount = table.Column<long>(type: "bigint", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: true),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usage_daily", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "usage_hourly",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BucketStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestCount = table.Column<long>(type: "bigint", nullable: false),
                    IdempotencyReplayCount = table.Column<long>(type: "bigint", nullable: false),
                    ItemCount = table.Column<long>(type: "bigint", nullable: false),
                    PassCount = table.Column<long>(type: "bigint", nullable: false),
                    RejectCount = table.Column<long>(type: "bigint", nullable: false),
                    ReviewCount = table.Column<long>(type: "bigint", nullable: false),
                    AiCallCount = table.Column<long>(type: "bigint", nullable: false),
                    AiFailureCount = table.Column<long>(type: "bigint", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: true),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usage_hourly", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_word_rules_RuleSetVersionId_IsEnabled_Action",
                table: "word_rules",
                columns: new[] { "RuleSetVersionId", "IsEnabled", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_moderation_items_RequestId_Ordinal",
                table: "moderation_items",
                columns: new[] { "RequestId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_invocations_ApiKeyId_CompletedAt",
                table: "ai_invocations",
                columns: new[] { "ApiKeyId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_invocations_ApplicationId_CompletedAt",
                table: "ai_invocations",
                columns: new[] { "ApplicationId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_invocations_ModerationItemId",
                table: "ai_invocations",
                column: "ModerationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_invocations_ModerationItemId_AttemptNumber",
                table: "ai_invocations",
                columns: new[] { "ModerationItemId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_api_request_events_ApiKeyId_OccurredAt",
                table: "api_request_events",
                columns: new[] { "ApiKeyId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_api_request_events_ApplicationId_OccurredAt",
                table: "api_request_events",
                columns: new[] { "ApplicationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_api_request_events_ModerationRequestId",
                table: "api_request_events",
                column: "ModerationRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_ActorId_OccurredAt",
                table: "audit_events",
                columns: new[] { "ActorId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_ApplicationId_OccurredAt",
                table: "audit_events",
                columns: new[] { "ApplicationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_combination_rules_RuleSetVersionId_Category",
                table: "combination_rules",
                columns: new[] { "RuleSetVersionId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_combination_rules_RuleSetVersionId_IsEnabled_Priority",
                table: "combination_rules",
                columns: new[] { "RuleSetVersionId", "IsEnabled", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_moderation_jobs_RequestId",
                table: "moderation_jobs",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_moderation_jobs_Status_AvailableAt_Priority",
                table: "moderation_jobs",
                columns: new[] { "Status", "AvailableAt", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_events_ApplicationId_OccurredAt",
                table: "outbox_events",
                columns: new[] { "ApplicationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_events_PublishedAt_AvailableAt_OccurredAt",
                table: "outbox_events",
                columns: new[] { "PublishedAt", "AvailableAt", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_regex_rules_RuleSetVersionId_IsEnabled_Priority",
                table: "regex_rules",
                columns: new[] { "RuleSetVersionId", "IsEnabled", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_regex_rules_RuleSetVersionId_Pattern_Category",
                table: "regex_rules",
                columns: new[] { "RuleSetVersionId", "Pattern", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_usage_consumed_events_ConsumerName_OutboxEventId",
                table: "usage_consumed_events",
                columns: new[] { "ConsumerName", "OutboxEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usage_daily_ApplicationId_ApiKeyId_BucketStart",
                table: "usage_daily",
                columns: new[] { "ApplicationId", "ApiKeyId", "BucketStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usage_daily_ApplicationId_BucketStart",
                table: "usage_daily",
                columns: new[] { "ApplicationId", "BucketStart" });

            migrationBuilder.CreateIndex(
                name: "IX_usage_hourly_ApplicationId_ApiKeyId_BucketStart",
                table: "usage_hourly",
                columns: new[] { "ApplicationId", "ApiKeyId", "BucketStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usage_hourly_ApplicationId_BucketStart",
                table: "usage_hourly",
                columns: new[] { "ApplicationId", "BucketStart" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_invocations");

            migrationBuilder.DropTable(
                name: "api_request_events");

            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "combination_rules");

            migrationBuilder.DropTable(
                name: "moderation_jobs");

            migrationBuilder.DropTable(
                name: "outbox_events");

            migrationBuilder.DropTable(
                name: "regex_rules");

            migrationBuilder.DropTable(
                name: "usage_consumed_events");

            migrationBuilder.DropTable(
                name: "usage_daily");

            migrationBuilder.DropTable(
                name: "usage_hourly");

            migrationBuilder.DropIndex(
                name: "IX_word_rules_RuleSetVersionId_IsEnabled_Action",
                table: "word_rules");

            migrationBuilder.DropIndex(
                name: "IX_moderation_items_RequestId_Ordinal",
                table: "moderation_items");

            migrationBuilder.DropColumn(
                name: "Action",
                table: "word_rules");

            migrationBuilder.DropColumn(
                name: "EvidenceTemplate",
                table: "word_rules");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "word_rules");

            migrationBuilder.DropColumn(
                name: "MatchMode",
                table: "word_rules");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "word_rules");

            migrationBuilder.DropColumn(
                name: "Scene",
                table: "word_rules");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "word_rules");

            migrationBuilder.DropColumn(
                name: "NormalizationProfile",
                table: "rule_set_versions");

            migrationBuilder.DropColumn(
                name: "Ordinal",
                table: "moderation_items");
        }
    }
}
