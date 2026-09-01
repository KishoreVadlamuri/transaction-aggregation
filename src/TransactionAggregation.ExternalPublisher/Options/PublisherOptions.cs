namespace TransactionAggregation.ExternalPublisher.Options;

public sealed class PublisherOptions
{
    public const string SectionName = "Publisher";

    /// <summary>
    /// When false, the worker idles without publishing.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Seconds between published chunks once the service is running.
    /// </summary>
    public int IntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Number of FinancialTransaction records in each chunk.
    /// </summary>
    public int ChunkSize { get; set; } = 5;

    /// <summary>
    /// Path to the JSON file containing FinancialTransaction records.
    /// Relative paths are resolved from the content root.
    /// </summary>
    public string DataFilePath { get; set; } = string.Empty;
}
