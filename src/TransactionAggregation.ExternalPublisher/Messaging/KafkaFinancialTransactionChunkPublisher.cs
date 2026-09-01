using System.Diagnostics;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.ExternalPublisher.Options;

namespace TransactionAggregation.ExternalPublisher.Messaging;

public interface IFinancialTransactionChunkPublisher
{
    Task PublishChunkAsync(IReadOnlyList<FinancialTransaction> transactions, CancellationToken cancellationToken);
}

public sealed class KafkaFinancialTransactionChunkPublisher : IFinancialTransactionChunkPublisher, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaFinancialTransactionChunkPublisher> _logger;
    private readonly IProducer<string, string>? _producer;
    private readonly bool _enabled;

    public KafkaFinancialTransactionChunkPublisher(
        IOptions<KafkaOptions> options,
        ILogger<KafkaFinancialTransactionChunkPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
        _enabled = _options.Enabled;

        if (!_enabled)
        {
            _logger.LogInformation(
                "Kafka producer disabled for external publisher (Topic={Topic})",
                _options.Topic);
            return;
        }

        var config = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            ClientId = _options.ClientId,
            Acks = Acks.All,
            MessageTimeoutMs = 5000,
            SocketTimeoutMs = 5000
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
        _logger.LogInformation(
            "Kafka producer initialized for external publisher (BootstrapServers={BootstrapServers}, Topic={Topic}, ClientId={ClientId})",
            _options.BootstrapServers,
            _options.Topic,
            _options.ClientId);
    }

    public async Task PublishChunkAsync(
        IReadOnlyList<FinancialTransaction> transactions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        if (transactions.Count == 0)
        {
            _logger.LogWarning("PublishChunkAsync called with an empty chunk; nothing to publish");
            return;
        }

        if (!_enabled || _producer is null)
        {
            _logger.LogInformation(
                "Kafka disabled. Would publish {Count} FinancialTransaction records to {Topic}",
                transactions.Count,
                _options.Topic);

            foreach (var transaction in transactions)
            {
                _logger.LogDebug(
                    "Skipped publish for transaction {TransactionId} customer {CustomerId} amount {Amount}",
                    transaction.Id,
                    transaction.CustomerId,
                    transaction.TransactionAmount);
            }

            return;
        }

        var published = 0;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogDebug(
            "Publishing chunk of {Count} FinancialTransaction records to topic {Topic}",
            transactions.Count,
            _options.Topic);

        foreach (var transaction in transactions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var payload = JsonSerializer.Serialize(transaction, JsonOptions);
            var message = new Message<string, string>
            {
                Key = transaction.CustomerId,
                Value = payload
            };

            try
            {
                var result = await _producer.ProduceAsync(_options.Topic, message, cancellationToken);
                published++;
                _logger.LogDebug(
                    "Published transaction {TransactionId} for {CustomerId} to {Topic} @ {Offset}",
                    transaction.Id,
                    transaction.CustomerId,
                    result.Topic,
                    result.Offset);
            }
            catch (ProduceException<string, string> ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to publish transaction {TransactionId} for {CustomerId}; aborting remaining records in chunk ({Published}/{Total} sent)",
                    transaction.Id,
                    transaction.CustomerId,
                    published,
                    transactions.Count);
                break;
            }
            catch (KafkaException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Kafka unavailable while publishing FinancialTransaction chunk ({Published}/{Total} sent)",
                    published,
                    transactions.Count);
                break;
            }
        }

        _logger.LogDebug("Flushing Kafka producer after chunk publish");
        _producer.Flush(TimeSpan.FromSeconds(5));
        stopwatch.Stop();

        _logger.LogInformation(
            "External publisher sent {Published}/{Total} FinancialTransaction records to topic {Topic} in {ElapsedMs}ms",
            published,
            transactions.Count,
            _options.Topic,
            stopwatch.ElapsedMilliseconds);

        if (published < transactions.Count)
        {
            _logger.LogWarning(
                "Chunk publish incomplete: {Failed} of {Total} records were not sent",
                transactions.Count - published,
                transactions.Count);
        }
    }

    public void Dispose()
    {
        _logger.LogDebug("Disposing Kafka external publisher producer");
        _producer?.Dispose();
    }
}
