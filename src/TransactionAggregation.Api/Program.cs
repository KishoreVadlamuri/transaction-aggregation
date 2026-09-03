using Scalar.AspNetCore;
using System.Text.Json.Serialization;
using TransactionAggregation.Api.Auth;
using TransactionAggregation.Api.Endpoints;
using TransactionAggregation.Api.Middlewares;
using TransactionAggregation.Application;
using TransactionAggregation.Application.Options;
using TransactionAggregation.Infrastructure;
using TransactionAggregation.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration);
builder.Services.AddJwtAuth(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new Microsoft.OpenApi.OpenApiInfo
        {
            Title = "Transaction Aggregation API",
            Version = "v1",
            Description = "Aggregates and categorizes customer financial transactions from multiple mock sources. " +
                          "Protected with JWT auth via a single service account. Login at POST /api/v1/auth/login — " +
                          "any valid token can access all protected endpoints."
        };
        return Task.CompletedTask;
    });
    options.AddDocumentTransformer(AuthServiceCollectionExtensions.CreateJwtOpenApiTransformer());
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

var healthChecks = builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

var storageOptions = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
if (!string.IsNullOrWhiteSpace(storageOptions.PostgresConnectionString))
{
    healthChecks.AddNpgSql(storageOptions.PostgresConnectionString, name: "postgres");
}

var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("TransactionAggregation.Startup");

startupLogger.LogInformation(
    "Starting Transaction Aggregation API ({Environment})",
    app.Environment.EnvironmentName);
startupLogger.LogInformation("API routes mounted under /api/v1/...");
startupLogger.LogInformation(
    "JWT auth: login at /api/v1/auth/login (service account {Username}; password hash from config/Docker env)",
    builder.Configuration["ServiceAccount:Username"]);

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapScalarApiReference(options =>
{
    options
        .WithTitle("Transaction Aggregation API")
        .WithOpenApiRoutePattern("/openapi/{documentName}.json")
        .AddPreferredSecuritySchemes("Bearer")
        .AddHttpAuthentication("Bearer", scheme =>
        {
            // Leave Token empty so Scalar prompts for the JWT from /api/v1/auth/login.
            scheme.Token = string.Empty;
        });
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapApiEndpoints();
app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Redirect("/scalar"))
    .ExcludeFromDescription()
    .AllowAnonymous();

startupLogger.LogInformation("API endpoints mapped under /api/v1/... ; listening for requests");

app.Run();