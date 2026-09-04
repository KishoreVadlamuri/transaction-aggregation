namespace TransactionAggregation.Application.Options;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>
    /// Valkey connection string (Redis-compatible protocol). When empty, an in-process distributed memory cache is used.
    /// </summary>
    public string ValkeyConnectionString { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = string.Empty;
}
