namespace TransactionAggregation.Application.Options;

public sealed class AggregationOptions
{
    public const string SectionName = "Aggregation";

    /// <summary>
    /// Artificial compute delay used to simulate an expensive aggregation workload.
    /// </summary>
    public int ExpensiveComputationDelayMs { get; set; }

    public string DefaultCurrency { get; set; } = string.Empty;
}
