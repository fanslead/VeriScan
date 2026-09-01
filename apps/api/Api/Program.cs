using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using VeriScan.Api.Authentication;
using VeriScan.Api.Endpoints;
using VeriScan.Api.Middleware;
using VeriScan.Api.OpenApi;
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
});
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, ScopeAuthorizationHandler>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, AdminRoleAuthorizationHandler>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<IModerationService, ModerationService>();
builder.Services.AddScoped<IAdminReadService, AdminReadService>();
builder.Services.AddScoped<IAiConfigurationService, AiConfigurationService>();
builder.Services.AddSingleton<IRuleModerationEngine, RuleModerationEngine>();
builder.Services.AddVeriScanInfrastructure(builder.Configuration);
builder.Services.AddVeriScanExternalAi(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync(CancellationToken.None);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/healthz", () => TypedResults.Ok(new { status = "ok" }))
    .WithName("Health")
    .WithSummary("服务健康检查")
    .Produces(StatusCodes.Status200OK);
app.MapApplicationEndpoints();
app.MapApiKeyEndpoints();
app.MapModerationEndpoints();
app.MapAdminReadEndpoints();
app.MapAiConfigurationEndpoints();

app.Run();

public partial class Program
{
}
