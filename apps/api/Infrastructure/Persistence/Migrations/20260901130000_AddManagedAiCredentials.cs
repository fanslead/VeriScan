using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeriScan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(VeriScanDbContext))]
[Migration("20260901130000_AddManagedAiCredentials")]
public sealed class AddManagedAiCredentials : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CredentialCiphertext",
            table: "ai_model_configurations",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CredentialCiphertext",
            table: "ai_model_configurations");
    }
}
