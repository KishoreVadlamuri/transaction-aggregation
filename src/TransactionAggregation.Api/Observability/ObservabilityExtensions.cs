using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TransactionAggregation.Application.Telemetry;

namespace TransactionAggregation.Api.Observability;

public static class ObservabilityExtensions
{
    public const string ServiceName = "transaction-aggregation-api";

    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var options = configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>()
            ?? new ObservabilityOptions();

        if (!options.Enabled)
        {
            return services;
        }

        var resource = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: ServiceName,
                serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0")
            .AddAttributes(
            [
                new KeyValuePair<string, object>("deployment.environment", environment.EnvironmentName)
            ]);

        var otlpEndpoint = options.OtlpEndpoint;

        services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resource)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(AggregationMetrics.MeterName);

                if (options.PrometheusEnabled)
                {
                    metrics.AddPrometheusExporter();
                }

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    metrics.AddOtlpExporter(otlp => ApplyOtlp(otlp, options));
                }
            })
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resource)
                    .AddAspNetCoreInstrumentation(instrumentation =>
                    {
                        instrumentation.Filter = context =>
                            !context.Request.Path.StartsWithSegments("/health")
                            && !context.Request.Path.StartsWithSegments("/metrics");
                    })
                    .AddHttpClientInstrumentation()
                    .AddSource(ServiceName);

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(otlp => ApplyOtlp(otlp, options));
                }
            });

        return services;
    }

    public static WebApplication MapObservabilityEndpoints(this WebApplication app, IConfiguration configuration)
    {
        var options = configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>()
            ?? new ObservabilityOptions();

        if (options.Enabled && options.PrometheusEnabled)
        {
            app.MapPrometheusScrapingEndpoint("/metrics")
                .AllowAnonymous()
                .DisableHttpMetrics();
        }

        return app;
    }

    private static void ApplyOtlp(OpenTelemetry.Exporter.OtlpExporterOptions otlp, ObservabilityOptions options)
    {
        otlp.Endpoint = new Uri(options.OtlpEndpoint!);
        if (options.OtlpProtocol.Equals("http/protobuf", StringComparison.OrdinalIgnoreCase))
        {
            otlp.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
        }
    }
}

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>Master switch for OpenTelemetry metrics and traces.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Expose Prometheus scrape endpoint at /metrics.</summary>
    public bool PrometheusEnabled { get; set; } = true;

    /// <summary>
    /// Optional OTLP collector/Tempo endpoint (e.g. http://tempo:4317 for gRPC).
    /// When set, metrics and traces are also exported via OTLP.
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>OTLP protocol: grpc (default) or http/protobuf.</summary>
    public string OtlpProtocol { get; set; } = "grpc";
}

