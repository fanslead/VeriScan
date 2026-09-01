using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace VeriScan.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRuleSetGovernance : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
                name: "IX_word_rules_IsEnabled_Type",
                table: "word_rules");

        migrationBuilder.DropIndex(
            name: "IX_word_rules_Term",
            table: "word_rules");

        migrationBuilder.AddColumn<Guid>(
            name: "RuleSetVersionId",
            table: "word_rules",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.AddColumn<string>(
            name: "PolicyRevision",
            table: "moderation_requests",
            type: "character varying(80)",
            maxLength: 80,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<Guid>(
            name: "RuleSetVersionId",
            table: "applications",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "rule_set_versions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PublicRevisionId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastValidatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastValidatedChecksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                PublishedChecksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_rule_set_versions", x => x.Id);
            });

        migrationBuilder.Sql(
            """
                INSERT INTO rule_set_versions (
                    "Id", "PublicRevisionId", "Name", "Status", "CreatedAt", "UpdatedAt",
                    "LastValidatedAt", "LastValidatedChecksum", "PublishedAt", "PublishedChecksum")
                SELECT
                    '11111111-1111-7111-8111-111111111111'::uuid,
                    'ruleset@legacy-v1',
                    '迁移前基础规则',
                    'Published',
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP,
                    'legacy-migrated',
                    CURRENT_TIMESTAMP,
                    'legacy-migrated'
                WHERE EXISTS (SELECT 1 FROM word_rules);

                UPDATE word_rules
                SET "RuleSetVersionId" = '11111111-1111-7111-8111-111111111111'::uuid
                WHERE "RuleSetVersionId" = '00000000-0000-0000-0000-000000000000'::uuid;

                UPDATE applications
                SET "RuleSetVersionId" = '11111111-1111-7111-8111-111111111111'::uuid
                WHERE "RuleSetVersionId" IS NULL
                  AND EXISTS (
                      SELECT 1 FROM rule_set_versions
                      WHERE "Id" = '11111111-1111-7111-8111-111111111111'::uuid);

                UPDATE moderation_requests
                SET "PolicyRevision" = 'ruleset@legacy-v1'
                WHERE "PolicyRevision" = '';

                ALTER TABLE word_rules ALTER COLUMN "RuleSetVersionId" DROP DEFAULT;
                ALTER TABLE moderation_requests ALTER COLUMN "PolicyRevision" DROP DEFAULT;
                """);

        migrationBuilder.CreateIndex(
            name: "IX_word_rules_RuleSetVersionId_IsEnabled_Type",
            table: "word_rules",
            columns: new[] { "RuleSetVersionId", "IsEnabled", "Type" });

        migrationBuilder.CreateIndex(
            name: "IX_word_rules_RuleSetVersionId_Term",
            table: "word_rules",
            columns: new[] { "RuleSetVersionId", "Term" });

        migrationBuilder.CreateIndex(
            name: "IX_applications_RuleSetVersionId",
            table: "applications",
            column: "RuleSetVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_rule_set_versions_PublicRevisionId",
            table: "rule_set_versions",
            column: "PublicRevisionId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_rule_set_versions_Status_UpdatedAt",
            table: "rule_set_versions",
            columns: new[] { "Status", "UpdatedAt" });

        migrationBuilder.AddForeignKey(
            name: "FK_applications_rule_set_versions_RuleSetVersionId",
            table: "applications",
            column: "RuleSetVersionId",
            principalTable: "rule_set_versions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_word_rules_rule_set_versions_RuleSetVersionId",
            table: "word_rules",
            column: "RuleSetVersionId",
            principalTable: "rule_set_versions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_applications_rule_set_versions_RuleSetVersionId",
            table: "applications");

        migrationBuilder.DropForeignKey(
            name: "FK_word_rules_rule_set_versions_RuleSetVersionId",
            table: "word_rules");

        migrationBuilder.DropTable(
            name: "rule_set_versions");

        migrationBuilder.DropIndex(
            name: "IX_word_rules_RuleSetVersionId_IsEnabled_Type",
            table: "word_rules");

        migrationBuilder.DropIndex(
            name: "IX_word_rules_RuleSetVersionId_Term",
            table: "word_rules");

        migrationBuilder.DropIndex(
            name: "IX_applications_RuleSetVersionId",
            table: "applications");

        migrationBuilder.DropColumn(
            name: "RuleSetVersionId",
            table: "word_rules");

        migrationBuilder.DropColumn(
            name: "PolicyRevision",
            table: "moderation_requests");

        migrationBuilder.DropColumn(
            name: "RuleSetVersionId",
            table: "applications");

        migrationBuilder.CreateIndex(
            name: "IX_word_rules_IsEnabled_Type",
            table: "word_rules",
            columns: new[] { "IsEnabled", "Type" });

        migrationBuilder.CreateIndex(
            name: "IX_word_rules_Term",
            table: "word_rules",
            column: "Term");
    }
}
