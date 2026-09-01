#pragma warning disable IDE0161, CA1861
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeriScan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EnvironmentName = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "word_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Term = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_word_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "application_api_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicKeyId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LastFour = table.Column<string>(type: "character(4)", fixedLength: true, maxLength: 4, nullable: false),
                    SecretDigest = table.Column<byte[]>(type: "bytea", nullable: false),
                    PepperVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ScopesText = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    EnvironmentName = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NotBefore = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_api_keys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_application_api_keys_applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "moderation_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByApiKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IdempotencyKeyDigest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RequestFingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ProcessingStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MachineCompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moderation_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_moderation_requests_applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "moderation_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientItemId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Language = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProcessingStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ReviewSource = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Degraded = table.Column<bool>(type: "boolean", nullable: false),
                    RiskScore = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: true),
                    ScoreSource = table.Column<string>(type: "text", nullable: true),
                    Route = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReasonCodesText = table.Column<string>(type: "jsonb", nullable: false),
                    CategoriesText = table.Column<string>(type: "jsonb", nullable: false),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MachineCompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moderation_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_moderation_items_moderation_requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "moderation_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_application_api_keys_ApplicationId_Status",
                table: "application_api_keys",
                columns: new[] { "ApplicationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_application_api_keys_PublicKeyId",
                table: "application_api_keys",
                column: "PublicKeyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_applications_TenantId_PublicId",
                table: "applications",
                columns: new[] { "TenantId", "PublicId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_moderation_items_ApplicationId_CreatedAt",
                table: "moderation_items",
                columns: new[] { "ApplicationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_moderation_items_RequestId_ClientItemId",
                table: "moderation_items",
                columns: new[] { "RequestId", "ClientItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_moderation_requests_ApplicationId_SubmittedAt",
                table: "moderation_requests",
                columns: new[] { "ApplicationId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_word_rules_IsEnabled_Type",
                table: "word_rules",
                columns: new[] { "IsEnabled", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_word_rules_Term",
                table: "word_rules",
                column: "Term");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_api_keys");

            migrationBuilder.DropTable(
                name: "moderation_items");

            migrationBuilder.DropTable(
                name: "word_rules");

            migrationBuilder.DropTable(
                name: "moderation_requests");

            migrationBuilder.DropTable(
                name: "applications");
        }
    }
}
