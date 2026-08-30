namespace TransactionAggregation.Application.Options;

public sealed class AggregationOptions
{
    public const string SectionName = "Aggregation";

    /// <summary>
    /// Artificial compute delay used to simulate an expensive aggregation workload.
    /// </summary>
    public int ExpensiveComputationDelayMs { get; set; } = 1500;

    public string DefaultCurrency { get; set; } = "ZAR";
}
