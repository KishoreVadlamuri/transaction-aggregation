using System.Diagnostics.Metrics;

namespace TransactionAggregation.Application.Telemetry;

/// <summary>
/// Business and domain metrics for the aggregation API (OpenTelemetry Meter → Prometheus/Grafana).
/// </summary>
public static class AggregationMetrics
{
    public const string MeterName = "TransactionAggregation";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> AggregationRequests =
        Meter.CreateCounter<long>(
            "txn_agg_aggregations",
            unit: "{aggregation}",
            description: "Customer aggregation requests by cache outcome");

    public static readonly Histogram<double> AggregationDurationMs =
        Meter.CreateHistogram<double>(
            "txn_agg_aggregation_duration",
            unit: "ms",
            description: "Wall-clock duration of aggregation computation (cache misses only)");

    public static readonly Counter<long> AggregationTransactions =
        Meter.CreateCounter<long>(
            "txn_agg_aggregation_transactions",
            unit: "{transaction}",
            description: "Transactions included in computed aggregations");

    public static readonly Counter<long> IngestRequests =
        Meter.CreateCounter<long>(
            "txn_agg_ingest_requests",
            unit: "{request}",
            description: "Transaction ingest requests");

    public static readonly Counter<long> IngestedTransactions =
        Meter.CreateCounter<long>(
            "txn_agg_ingested_transactions",
            unit: "{transaction}",
            description: "Transactions persisted by ingest");

    public static void RecordAggregation(bool servedFromCache, double durationMs, int transactionCount)
    {
        AggregationRequests.Add(1, new KeyValuePair<string, object?>("cache", servedFromCache ? "hit" : "miss"));
        if (!servedFromCache)
        {
            AggregationDurationMs.Record(durationMs);
            AggregationTransactions.Add(transactionCount);
        }
    }

    public static void RecordIngest(int transactionCount, int sourceCount)
    {
        IngestRequests.Add(1, new KeyValuePair<string, object?>("sources", sourceCount));
        IngestedTransactions.Add(transactionCount);
    }
}

