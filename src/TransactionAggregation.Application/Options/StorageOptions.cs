namespace TransactionAggregation.Application.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string PostgresConnectionString { get; set; } = string.Empty;
}
