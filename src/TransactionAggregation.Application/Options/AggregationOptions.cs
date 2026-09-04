namespace TransactionAggregation.Application.Options;

public sealed class AggregationOptions
{
    public const string SectionName = "Aggregation";
    public string DefaultCurrency { get; set; } = string.Empty;
    public int CacheTtlSeconds { get; set; }
}
