using Scalar.AspNetCore;
using System.Text.Json.Serialization;
using TransactionAggregation.Api.Endpoints;
using TransactionAggregation.Api.Middlewares;
using TransactionAggregation.Application;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();

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

app.MapGet("/", () => Results.Redirect("/scalar"));

startupLogger.LogInformation("API endpoints mapped; listening for requests");

app.Run();