namespace TransactionAggregation.Application.Options;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public bool Enabled { get; set; } = true;

    public string BootstrapServers { get; set; } = string.Empty;

    public string Topic { get; set; } = string.Empty;

    public string ConsumerGroupId { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;
}
