using Scalar.AspNetCore;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TransactionAggregation.Api;
using TransactionAggregation.Api.Auth;
using TransactionAggregation.Api.Endpoints;
using TransactionAggregation.Api.Middlewares;
using TransactionAggregation.Api.Observability;
using TransactionAggregation.Application;
using TransactionAggregation.Application.Features.Transactions.GetTransactions;
using TransactionAggregation.Application.Options;
using TransactionAggregation.Infrastructure;
using TransactionAggregation.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration);
builder.Services.AddJwtAuth(builder.Configuration);
builder.Services.AddObservability(builder.Configuration, builder.Environment);

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
    options.AddDocumentTransformer(OpenApiDateRangeDefaults.CreateTransformer());

    options.AddSchemaTransformer((schema, context, cancellationToken) =>
    {
        if (context.JsonTypeInfo.Type == typeof(GetTransactionsQuery))
        {
            var (from, to) = OpenApiDateRangeDefaults.ComputeDefaults(DateTimeOffset.UtcNow);
            schema.Example = JsonNode.Parse($$"""
                {
                  "customerId": "customer Id",
                  "from": "{{from}}",
                  "to": "{{to}}"
                }
                """);
        }

        return Task.CompletedTask;
    });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

var healthChecks = builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

var valkeyConnection = builder.Configuration["Cache:ValkeyConnectionString"];
if (!string.IsNullOrWhiteSpace(valkeyConnection))
{
    healthChecks.AddRedis(valkeyConnection, name: "valkey");
}

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
startupLogger.LogInformation(
    "Observability: Prometheus /metrics and optional OTLP (Observability:OtlpEndpoint) for Grafana");

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
app.MapObservabilityEndpoints(app.Configuration);

app.MapGet("/", () => Results.Redirect("/scalar"))
    .ExcludeFromDescription()
    .AllowAnonymous();

startupLogger.LogInformation("API endpoints mapped under /api/v1/... ; listening for requests");

app.Run();