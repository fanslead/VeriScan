using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using VeriScan.Api.Authentication;
using VeriScan.Api.Diagnostics;
using VeriScan.Api.Endpoints;
using VeriScan.Api.Health;
using VeriScan.Api.Middleware;
using VeriScan.Api.OpenApi;
using VeriScan.Api.RateLimiting;
using VeriScan.Api.Workers;
using VeriScan.Application.Abstractions;
using VeriScan.Application.Services;
using VeriScan.Infrastructure;
using VeriScan.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var adminAuthentication = builder.Configuration
    .GetSection(AdminJwtOptions.SectionName)
    .Get<AdminJwtOptions>() ?? new AdminJwtOptions();
if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(adminAuthentication.Authority))
{
    throw new InvalidOperationException("生产环境必须配置 Authentication:Admin:Authority。");
}

builder.Services.AddOpenApi(options => options.AddDocumentTransformer<ApiDocumentTransformer>());
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadinessHealthCheck>("postgres", tags: ["ready"]);
builder.Services.AddVeriScanObservability(
    builder.Configuration,
    builder.Environment.ApplicationName);
builder.Services.AddVeriScanRateLimiting(builder.Configuration);
builder.Services.AddValidation();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = ApiKeyAuthenticationDefaults.Scheme;
        options.DefaultChallengeScheme = ApiKeyAuthenticationDefaults.Scheme;
    })
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationDefaults.Scheme,
        _ => { })
    .AddJwtBearer(AdminJwtOptions.Scheme, options =>
    {
        options.Authority = adminAuthentication.Authority;
        if (!string.IsNullOrWhiteSpace(adminAuthentication.MetadataAddress))
        {
            options.MetadataAddress = adminAuthentication.MetadataAddress;
        }
        options.Audience = adminAuthentication.Audience;
        options.RequireHttpsMetadata = adminAuthentication.RequireHttpsMetadata;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "preferred_username",
            RoleClaimType = "role",
            ValidateAudience = adminAuthentication.ValidateAudience
        };
    });
builder.Services.Configure<AdminJwtOptions>(
    builder.Configuration.GetSection(AdminJwtOptions.SectionName));
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        AdminJwtOptions.Policy,
        policy => policy
            .AddAuthenticationSchemes(AdminJwtOptions.Scheme)
            .RequireAuthenticatedUser()
            .AddRequirements(new AdminRoleRequirement()));
    options.AddPolicy(
        ApiKeyAuthenticationDefaults.SubmitPolicy,
        policy => policy
            .AddAuthenticationSchemes(ApiKeyAuthenticationDefaults.Scheme)
            .RequireAuthenticatedUser()
            .AddRequirements(new ScopeRequirement("moderation:submit")));
    options.AddPolicy(
        ApiKeyAuthenticationDefaults.ReadPolicy,
        policy => policy
            .AddAuthenticationSchemes(ApiKeyAuthenticationDefaults.Scheme)
            .RequireAuthenticatedUser()
            .AddRequirements(new ScopeRequirement("moderation:read")));
    options.AddPolicy(AdminPolicies.Viewer, policy => policy
        .AddAuthenticationSchemes(AdminJwtOptions.Scheme)
        .RequireAuthenticatedUser()
        .AddRequirements(new AdminPermissionRequirement(
            AdminPolicies.ViewerRole,
            AdminPolicies.OperatorRole,
            AdminPolicies.RuleEditorRole,
            AdminPolicies.AiConfigEditorRole,
            AdminPolicies.PublisherRole,
            AdminPolicies.AuditorRole,
            AdminPolicies.PlatformAdminRole)));
    options.AddPolicy(AdminPolicies.Operator, policy => policy
        .AddAuthenticationSchemes(AdminJwtOptions.Scheme)
        .RequireAuthenticatedUser()
        .AddRequirements(new AdminPermissionRequirement(
            AdminPolicies.OperatorRole,
            AdminPolicies.PlatformAdminRole)));
    options.AddPolicy(AdminPolicies.RuleEditor, policy => policy
        .AddAuthenticationSchemes(AdminJwtOptions.Scheme)
        .RequireAuthenticatedUser()
        .AddRequirements(new AdminPermissionRequirement(
            AdminPolicies.RuleEditorRole,
            AdminPolicies.PlatformAdminRole)));
    options.AddPolicy(AdminPolicies.AiConfigEditor, policy => policy
        .AddAuthenticationSchemes(AdminJwtOptions.Scheme)
        .RequireAuthenticatedUser()
        .AddRequirements(new AdminPermissionRequirement(
            AdminPolicies.AiConfigEditorRole,
            AdminPolicies.PlatformAdminRole)));
    options.AddPolicy(AdminPolicies.Publisher, policy => policy
        .AddAuthenticationSchemes(AdminJwtOptions.Scheme)
        .RequireAuthenticatedUser()
        .AddRequirements(new AdminPermissionRequirement(
            AdminPolicies.PublisherRole,
            AdminPolicies.PlatformAdminRole)));
    options.AddPolicy(AdminPolicies.Auditor, policy => policy
        .AddAuthenticationSchemes(AdminJwtOptions.Scheme)
        .RequireAuthenticatedUser()
        .AddRequirements(new AdminPermissionRequirement(
            AdminPolicies.AuditorRole,
            AdminPolicies.PlatformAdminRole)));
    options.AddPolicy(AdminPolicies.PlatformAdmin, policy => policy
        .AddAuthenticationSchemes(AdminJwtOptions.Scheme)
        .RequireAuthenticatedUser()
        .AddRequirements(new AdminPermissionRequirement(AdminPolicies.PlatformAdminRole)));
});
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, ScopeAuthorizationHandler>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, AdminRoleAuthorizationHandler>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, AdminPermissionAuthorizationHandler>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<IModerationService, ModerationService>();
builder.Services.AddScoped<IApplicationWebhookService, ApplicationWebhookService>();
builder.Services.AddScoped<IWebhookPublicationService, WebhookPublicationService>();
builder.Services.AddScoped<IAdminReadService, AdminReadService>();
builder.Services.AddScoped<IApplicationUsageService, ApplicationUsageService>();
builder.Services.AddScoped<IUsageProjectionService, UsageProjectionService>();
builder.Services.AddScoped<IOperationalFactService, OperationalFactService>();
builder.Services.AddScoped<IAuditQueryService, AuditQueryService>();
builder.Services.AddScoped<IAiConfigurationService, AiConfigurationService>();
builder.Services.AddScoped<IRuleSetService, RuleSetService>();
builder.Services.AddSingleton<IRuleModerationEngine, RuleModerationEngine>();
builder.Services.AddOptions<OutboxWorkerOptions>()
    .Bind(builder.Configuration.GetSection(OutboxWorkerOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<WebhookPublicationWorkerOptions>()
    .Bind(builder.Configuration.GetSection(WebhookPublicationWorkerOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddVeriScanInfrastructure(builder.Configuration);
builder.Services.AddVeriScanExternalAi(builder.Configuration);
builder.Services.AddVeriScanWebhooks(builder.Configuration);
builder.Services.AddHostedService<ModerationJobWorker>();
builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddHostedService<WebhookPublicationWorker>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseRouting();
app.UseVeriScanRequestTelemetry();
app.UseStatusCodePages();
app.UseVeriScanIngressConcurrency();
app.UseAuthentication();
app.UseVeriScanRateLimitHeaders();
app.UseRateLimiter();
app.UseAuthorization();

var migrateOnly = args.Contains("--migrate", StringComparer.Ordinal);
if (migrateOnly || app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync(CancellationToken.None);
}

if (migrateOnly)
{
    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/healthz", () => TypedResults.Ok(new { status = "ok" }))
    .WithName("Health")
    .WithSummary("服务健康检查")
    .Produces(StatusCodes.Status200OK);
app.MapHealthChecks("/readyz", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = static async (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString().ToLowerInvariant(),
                description = entry.Value.Description
            })
        });
    }
})
    .WithName("Readiness")
    .WithSummary("服务接流量就绪检查");
app.MapApplicationEndpoints();
app.MapApplicationWebhookEndpoints();
app.MapApiKeyEndpoints();
app.MapModerationEndpoints();
app.MapAdminReadEndpoints();
app.MapApplicationUsageEndpoints();
app.MapAuditEventEndpoints();
app.MapAiConfigurationEndpoints();
app.MapRuleSetEndpoints();

app.Run();

public partial class Program
{
}
