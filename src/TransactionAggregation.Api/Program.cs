using Scalar.AspNetCore;
using System.Text.Json.Serialization;
using TransactionAggregation.Api.Endpoints;
using TransactionAggregation.Api.Middlewares;
using TransactionAggregation.Application;
using TransactionAggregation.Application.Options;
using TransactionAggregation.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddOpenApi();

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapScalarApiReference(options =>
{
    options
        .WithTitle("Transaction Aggregation API")
        .WithOpenApiRoutePattern("/openapi/{documentName}.json");
});

app.UseCors();
app.MapApiEndpoints();
app.MapHealthChecks("/health");

startupLogger.LogInformation("API endpoints mapped; listening for requests");

app.Run();